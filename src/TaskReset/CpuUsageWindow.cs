// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.

namespace TaskReset;

/// <summary>
/// Eine Messung: durchschnittliche CPU-Last des Prozesses über ein Messintervall.
/// </summary>
/// <param name="End">Zeitpunkt der Messung (Ende des Intervalls).</param>
/// <param name="Duration">Länge des Intervalls.</param>
/// <param name="Percent">CPU-Last in Prozent der Gesamt-CPU (0 bis 100).</param>
/// <param name="ProcessCount">Anzahl der gefundenen Prozessinstanzen.</param>
public readonly record struct CpuSample(DateTimeOffset End, TimeSpan Duration, double Percent, int ProcessCount)
{
    public DateTimeOffset Start => End - Duration;
}

/// <summary>
/// Gleitendes Zeitfenster über CPU-Messungen mit zeitgewichtetem Durchschnitt.
/// </summary>
public sealed class CpuUsageWindow
{
    private readonly LinkedList<CpuSample> _samples = new();
    private DateTimeOffset? _coverageStart;

    public CpuUsageWindow(TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window));
        Window = window;
    }

    public TimeSpan Window { get; }

    /// <summary>Zeitpunkt der letzten Messung.</summary>
    public DateTimeOffset? LastEnd => _samples.Last?.Value.End;

    /// <summary>True, sobald seit dem letzten Zurücksetzen ein volles Zeitfenster gemessen wurde.</summary>
    public bool IsFull => _coverageStart is { } start && LastEnd is { } end && end - start >= Window;

    /// <summary>Wie viel Zeit seit dem letzten Zurücksetzen bereits gemessen wurde (höchstens das Zeitfenster).</summary>
    public TimeSpan Coverage
    {
        get
        {
            if (_coverageStart is not { } start || LastEnd is not { } end)
                return TimeSpan.Zero;
            var covered = end - start;
            return covered > Window ? Window : covered;
        }
    }

    public void Add(CpuSample sample)
    {
        if (sample.Duration <= TimeSpan.Zero)
            return;
        if (LastEnd is { } last && sample.End <= last)
            return; // Uhr zurückgestellt oder doppelte Messung: ignorieren.

        _coverageStart ??= sample.Start;
        _samples.AddLast(sample);

        var cutoff = sample.End - Window;
        while (_samples.First is { } first && first.Value.End <= cutoff)
            _samples.RemoveFirst();
    }

    /// <summary>Zeitgewichteter Durchschnitt über das Zeitfenster bis zur letzten Messung, oder null ohne Messungen.</summary>
    public double? Average
    {
        get
        {
            if (LastEnd is not { } end)
                return null;

            var windowStart = end - Window;
            double weighted = 0;
            double totalSeconds = 0;

            foreach (var s in _samples)
            {
                var from = s.Start > windowStart ? s.Start : windowStart;
                var seconds = (s.End - from).TotalSeconds;
                if (seconds <= 0)
                    continue;
                weighted += s.Percent * seconds;
                totalSeconds += seconds;
            }

            return totalSeconds > 0 ? weighted / totalSeconds : null;
        }
    }

    public void Reset()
    {
        _samples.Clear();
        _coverageStart = null;
    }
}
