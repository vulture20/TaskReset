// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
namespace TaskReset;

/// <summary>
/// Hintergrunddienst: misst im festen Takt, wertet aus und startet bei Bedarf den Dienst neu.
/// </summary>
public sealed class Worker : BackgroundService
{
    private static readonly TimeSpan StatusLogInterval = TimeSpan.FromMinutes(15);

    private readonly WatchdogOptions _options;
    private readonly ICpuSampler _sampler;
    private readonly IServiceRestarter _restarter;
    private readonly TimeProvider _time;
    private readonly ILogger<Worker> _logger;
    private readonly WatchdogEngine _engine;
    private bool _processMissingLogged;
    private DateTimeOffset _nextStatusLog;

    public Worker(WatchdogOptions options, ICpuSampler sampler, IServiceRestarter restarter, TimeProvider time, ILogger<Worker> logger)
    {
        _options = options;
        _sampler = sampler;
        _restarter = restarter;
        _time = time;
        _logger = logger;
        _engine = new WatchdogEngine(options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(EventIds.Started,
            "TaskReset gestartet. Überwacht Prozess {ProcessName}, startet Dienst {ServiceName} neu, " +
            "wenn die CPU-Last im Schnitt über {Window} min mindestens {Threshold} % beträgt. " +
            "Messintervall {Interval} s, Pause nach Neustart {Cooldown} min.",
            _options.ProcessName, _options.ServiceName, _options.AverageWindowMinutes, _options.CpuThresholdPercent,
            _options.SampleIntervalSeconds, _options.CooldownMinutes);

        _sampler.Sample(); // Ausgangswert
        _nextStatusLog = _time.GetUtcNow() + StatusLogInterval;

        using var timer = new PeriodicTimer(_options.SampleInterval, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await TickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation(EventIds.Stopped, "TaskReset beendet.");
    }

    internal async Task TickAsync(CancellationToken stoppingToken)
    {
        var sample = _sampler.Sample();
        var now = _time.GetUtcNow();

        if (sample is { ProcessCount: 0 } && !_processMissingLogged)
        {
            _logger.LogWarning(EventIds.ProcessNotFound, "Prozess {ProcessName} läuft derzeit nicht.", _options.ProcessName);
            _processMissingLogged = true;
        }
        else if (sample is { ProcessCount: > 0 })
        {
            _processMissingLogged = false;
        }

        var decision = _engine.Evaluate(now, sample);

        switch (decision.Action)
        {
            case WatchdogAction.Restart:
                await RestartAsync(decision.Average!.Value, stoppingToken);
                break;

            case WatchdogAction.CooldownEnded:
                _logger.LogInformation(EventIds.CooldownEnded,
                    "Pause beendet. Die Messung über {Window} min beginnt von vorn.", _options.AverageWindowMinutes);
                break;

            default:
                if (now >= _nextStatusLog)
                {
                    _nextStatusLog = now + StatusLogInterval;
                    LogStatus(decision, sample);
                }
                else
                {
                    _logger.LogDebug("Messung: aktuell {Current:F1} %, Durchschnitt {Average:F1} %, Zustand {State}.",
                        sample?.Percent, decision.Average, decision.Action);
                }
                break;
        }
    }

    private void LogStatus(WatchdogDecision decision, CpuSample? sample)
    {
        if (decision.Action == WatchdogAction.Cooldown)
        {
            _logger.LogInformation(EventIds.Status, "Status: Pause nach Neustart läuft bis {Until:u}.", _engine.CooldownUntil);
            return;
        }

        _logger.LogInformation(EventIds.Status,
            "Status: Prozess {ProcessName} ({Count} Instanz(en)), Durchschnitt {Average:F1} % über {Coverage:F1} min, Schwellenwert {Threshold} %.",
            _options.ProcessName, sample?.ProcessCount ?? 0, decision.Average ?? 0, _engine.Coverage.TotalMinutes, _options.CpuThresholdPercent);
    }

    private async Task RestartAsync(double average, CancellationToken stoppingToken)
    {
        _logger.LogWarning(EventIds.ThresholdExceeded,
            "Prozess {ProcessName} hat im Schnitt der letzten {Window} min {Average:F1} % CPU belastet (Schwellenwert {Threshold} %). " +
            "Dienst {ServiceName} wird neu gestartet.",
            _options.ProcessName, _options.AverageWindowMinutes, average, _options.CpuThresholdPercent, _options.ServiceName);

        try
        {
            await _restarter.RestartAsync(stoppingToken);
            _logger.LogInformation(EventIds.Restarted,
                "Dienst {ServiceName} wurde neu gestartet. Nächste Prüfung frühestens in {Cooldown} min.",
                _options.ServiceName, _options.CooldownMinutes);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(EventIds.RestartFailed, ex,
                "Neustart von Dienst {ServiceName} ist fehlgeschlagen. Nächster Versuch frühestens in {Cooldown} min.",
                _options.ServiceName, _options.CooldownMinutes);
        }
        finally
        {
            _engine.NotifyRestarted(_time.GetUtcNow());
            _sampler.Sample(); // neuer Ausgangswert nach dem Neustart
        }
    }
}
