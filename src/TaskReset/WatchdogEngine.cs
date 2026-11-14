// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.

namespace TaskReset;

public enum WatchdogAction
{
    /// <summary>Noch nicht genug Messwerte für ein volles Zeitfenster.</summary>
    Collecting,

    /// <summary>Durchschnitt liegt unter dem Schwellenwert.</summary>
    BelowThreshold,

    /// <summary>Durchschnitt liegt über dem Schwellenwert: Dienst neu starten.</summary>
    Restart,

    /// <summary>Pause nach einem Neustart läuft noch.</summary>
    Cooldown,

    /// <summary>Die Pause ist gerade abgelaufen, die Messung beginnt von vorn.</summary>
    CooldownEnded,
}

public readonly record struct WatchdogDecision(WatchdogAction Action, double? Average);

/// <summary>
/// Entscheidungslogik ohne Abhängigkeit zu Windows: nimmt Messungen entgegen und entscheidet, ob neu gestartet wird.
/// </summary>
public sealed class WatchdogEngine
{
    private readonly WatchdogOptions _options;
    private readonly CpuUsageWindow _window;

    public WatchdogEngine(WatchdogOptions options)
    {
        _options = options;
        _window = new CpuUsageWindow(options.AverageWindow);
    }

    /// <summary>Ende der aktuellen Pause, oder null, wenn keine Pause läuft.</summary>
    public DateTimeOffset? CooldownUntil { get; private set; }

    public TimeSpan Coverage => _window.Coverage;

    public WatchdogDecision Evaluate(DateTimeOffset now, CpuSample? sample)
    {
        if (CooldownUntil is { } until)
        {
            if (now < until)
                return new WatchdogDecision(WatchdogAction.Cooldown, null);

            // Pause vorbei: Messungen, die noch in die Pause hineinreichen, verwerfen wir.
            CooldownUntil = null;
            _window.Reset();
            return new WatchdogDecision(WatchdogAction.CooldownEnded, null);
        }

        if (sample is { } s)
            _window.Add(s);

        var average = _window.Average;
        if (!_window.IsFull || average is null)
            return new WatchdogDecision(WatchdogAction.Collecting, average);

        return average.Value >= _options.CpuThresholdPercent
            ? new WatchdogDecision(WatchdogAction.Restart, average)
            : new WatchdogDecision(WatchdogAction.BelowThreshold, average);
    }

    /// <summary>Nach einem Neustartversuch aufrufen (auch wenn er fehlgeschlagen ist): startet die Pause.</summary>
    public void NotifyRestarted(DateTimeOffset now)
    {
        _window.Reset();
        CooldownUntil = _options.Cooldown > TimeSpan.Zero ? now + _options.Cooldown : null;
    }
}
