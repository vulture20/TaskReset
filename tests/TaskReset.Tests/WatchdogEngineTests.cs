// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
namespace TaskReset.Tests;

public class WatchdogEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static WatchdogOptions Options(double cooldownMinutes = 30) => new()
    {
        ServiceName = "Svc",
        ProcessName = "proc",
        CpuThresholdPercent = 95,
        AverageWindowMinutes = 5,
        SampleIntervalSeconds = 5,
        CooldownMinutes = cooldownMinutes,
    };

    /// <summary>Füttert die Engine im 5-Sekunden-Takt und liefert die letzte Entscheidung.</summary>
    private static WatchdogDecision Feed(WatchdogEngine engine, ref DateTimeOffset now, TimeSpan duration, double percent)
    {
        WatchdogDecision last = default;
        var end = now + duration;
        while (now < end)
        {
            now = now.AddSeconds(5);
            last = engine.Evaluate(now, new CpuSample(now, TimeSpan.FromSeconds(5), percent, 1));
            if (last.Action == WatchdogAction.Restart)
                return last;
        }
        return last;
    }

    [Fact]
    public void No_restart_before_window_is_full()
    {
        var engine = new WatchdogEngine(Options());
        var now = T0;
        var decision = Feed(engine, ref now, TimeSpan.FromMinutes(4.9), 100);
        Assert.Equal(WatchdogAction.Collecting, decision.Action);
    }

    [Fact]
    public void Restart_when_average_reaches_threshold_over_full_window()
    {
        var engine = new WatchdogEngine(Options());
        var now = T0;
        var decision = Feed(engine, ref now, TimeSpan.FromMinutes(6), 99);
        Assert.Equal(WatchdogAction.Restart, decision.Action);
        Assert.Equal(T0.AddMinutes(5), now);
        Assert.Equal(99, decision.Average!.Value, 6);
    }

    [Fact]
    public void Short_spikes_do_not_trigger_restart()
    {
        var engine = new WatchdogEngine(Options());
        var now = T0;
        Feed(engine, ref now, TimeSpan.FromMinutes(4), 40);
        var decision = Feed(engine, ref now, TimeSpan.FromMinutes(1), 100);
        Assert.Equal(WatchdogAction.BelowThreshold, decision.Action);
    }

    [Fact]
    public void Below_threshold_keeps_running()
    {
        var engine = new WatchdogEngine(Options());
        var now = T0;
        var decision = Feed(engine, ref now, TimeSpan.FromMinutes(30), 94.9);
        Assert.Equal(WatchdogAction.BelowThreshold, decision.Action);
    }

    [Fact]
    public void Cooldown_blocks_restarts_and_measurement_starts_over_afterwards()
    {
        var engine = new WatchdogEngine(Options(cooldownMinutes: 30));
        var now = T0;
        Assert.Equal(WatchdogAction.Restart, Feed(engine, ref now, TimeSpan.FromMinutes(6), 100).Action);

        engine.NotifyRestarted(now);
        Assert.Equal(now.AddMinutes(30), engine.CooldownUntil);

        var restartTime = now;
        var decision = Feed(engine, ref now, TimeSpan.FromMinutes(29.9), 100);
        Assert.Equal(WatchdogAction.Cooldown, decision.Action);

        // Pause endet, danach muss erst wieder ein volles Fenster gemessen werden.
        now = restartTime.AddMinutes(30) - TimeSpan.FromSeconds(5);
        decision = Feed(engine, ref now, TimeSpan.FromSeconds(5), 100);
        Assert.Equal(WatchdogAction.CooldownEnded, decision.Action);
        Assert.Null(engine.CooldownUntil);

        decision = Feed(engine, ref now, TimeSpan.FromMinutes(4.9), 100);
        Assert.Equal(WatchdogAction.Collecting, decision.Action);

        decision = Feed(engine, ref now, TimeSpan.FromMinutes(1), 100);
        Assert.Equal(WatchdogAction.Restart, decision.Action);
    }

    [Fact]
    public void Zero_cooldown_only_resets_the_window()
    {
        var engine = new WatchdogEngine(Options(cooldownMinutes: 0));
        var now = T0;
        Feed(engine, ref now, TimeSpan.FromMinutes(6), 100);
        engine.NotifyRestarted(now);
        Assert.Null(engine.CooldownUntil);

        var decision = Feed(engine, ref now, TimeSpan.FromSeconds(5), 100);
        Assert.Equal(WatchdogAction.Collecting, decision.Action);
    }

    [Fact]
    public void Missing_samples_are_tolerated()
    {
        var engine = new WatchdogEngine(Options());
        var decision = engine.Evaluate(T0, null);
        Assert.Equal(WatchdogAction.Collecting, decision.Action);
        Assert.Null(decision.Average);
    }
}
