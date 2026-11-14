// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.

using System.Diagnostics;

namespace TaskReset;

public interface ICpuSampler
{
    /// <summary>Misst die CPU-Last seit dem letzten Aufruf. Liefert beim ersten Aufruf null (nur Ausgangswert).</summary>
    CpuSample? Sample();
}

/// <summary>
/// Misst die CPU-Last aller Prozesse mit dem konfigurierten Namen über die verbrauchte Prozessorzeit.
/// Das Ergebnis ist auf alle logischen Kerne normiert, 100 % heißt also: der ganze Rechner ist ausgelastet.
/// </summary>
public sealed class ProcessCpuSampler : ICpuSampler
{
    private readonly string _processName;
    private readonly ILogger<ProcessCpuSampler> _logger;
    private readonly TimeProvider _time;
    private readonly int _processorCount = Environment.ProcessorCount;
    private Dictionary<int, (DateTime StartTime, TimeSpan Cpu)> _previous = new();
    private long? _previousTimestamp;

    public ProcessCpuSampler(string processName, ILogger<ProcessCpuSampler> logger, TimeProvider time)
    {
        _processName = processName;
        _logger = logger;
        _time = time;
    }

    public CpuSample? Sample()
    {
        var timestamp = _time.GetTimestamp();
        var current = new Dictionary<int, (DateTime StartTime, TimeSpan Cpu)>();
        var cpuDelta = TimeSpan.Zero;

        foreach (var process in Process.GetProcessesByName(_processName))
        {
            using (process)
            {
                try
                {
                    var startTime = process.StartTime;
                    var cpu = process.TotalProcessorTime;
                    current[process.Id] = (startTime, cpu);

                    // Nur Prozesse zählen, die wir schon beim letzten Mal gesehen haben (gleiche PID und Startzeit).
                    if (_previous.TryGetValue(process.Id, out var prev) && prev.StartTime == startTime && cpu >= prev.Cpu)
                        cpuDelta += cpu - prev.Cpu;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Prozess wurde zwischenzeitlich beendet oder ist nicht zugreifbar.
                    _logger.LogDebug(ex, "Prozess {ProcessId} konnte nicht gelesen werden.", process.Id);
                }
            }
        }

        var previousTimestamp = _previousTimestamp;
        _previous = current;
        _previousTimestamp = timestamp;

        if (previousTimestamp is null)
            return null;

        var elapsed = _time.GetElapsedTime(previousTimestamp.Value, timestamp);
        if (elapsed <= TimeSpan.Zero)
            return null;

        var percent = cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * _processorCount) * 100.0;
        percent = Math.Clamp(percent, 0, 100);

        return new CpuSample(_time.GetUtcNow(), elapsed, percent, current.Count);
    }
}
