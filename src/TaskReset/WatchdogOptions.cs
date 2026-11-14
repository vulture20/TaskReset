// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.

namespace TaskReset;

/// <summary>
/// Einstellungen aus dem Abschnitt "TaskReset" der appsettings.json.
/// </summary>
public sealed class WatchdogOptions
{
    public const string SectionName = "TaskReset";

    /// <summary>Name des Windows-Dienstes, der neu gestartet wird (Dienstname, nicht Anzeigename).</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Name des überwachten Prozesses ohne ".exe". Laufen mehrere Instanzen, wird ihre Last addiert.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Schwellenwert in Prozent der Gesamt-CPU des Rechners (alle Kerne = 100 %).</summary>
    public double CpuThresholdPercent { get; set; } = 95;

    /// <summary>Zeitfenster für den gleitenden Durchschnitt in Minuten.</summary>
    public double AverageWindowMinutes { get; set; } = 5;

    /// <summary>Abstand zwischen zwei Messungen in Sekunden.</summary>
    public double SampleIntervalSeconds { get; set; } = 5;

    /// <summary>Pause nach einem Neustart in Minuten, in der weder gemessen noch neu gestartet wird.</summary>
    public double CooldownMinutes { get; set; } = 30;

    /// <summary>So lange wird auf ein sauberes Beenden des Dienstes gewartet, danach wird hart beendet.</summary>
    public double StopTimeoutSeconds { get; set; } = 60;

    /// <summary>So lange wird nach dem Start gewartet, bis der Dienst den Status "Wird ausgeführt" meldet.</summary>
    public double StartTimeoutSeconds { get; set; } = 60;

    public TimeSpan AverageWindow => TimeSpan.FromMinutes(AverageWindowMinutes);
    public TimeSpan SampleInterval => TimeSpan.FromSeconds(SampleIntervalSeconds);
    public TimeSpan Cooldown => TimeSpan.FromMinutes(CooldownMinutes);
    public TimeSpan StopTimeout => TimeSpan.FromSeconds(StopTimeoutSeconds);
    public TimeSpan StartTimeout => TimeSpan.FromSeconds(StartTimeoutSeconds);

    /// <summary>Prüft die Einstellungen und liefert alle gefundenen Fehler.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ServiceName))
            errors.Add($"{SectionName}:{nameof(ServiceName)} muss gesetzt sein.");
        else if (string.Equals(ServiceName.Trim(), "TaskReset", StringComparison.OrdinalIgnoreCase))
            errors.Add($"{SectionName}:{nameof(ServiceName)} darf nicht TaskReset selbst sein.");

        if (string.IsNullOrWhiteSpace(ProcessName))
            errors.Add($"{SectionName}:{nameof(ProcessName)} muss gesetzt sein.");
        else if (ProcessName.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            errors.Add($"{SectionName}:{nameof(ProcessName)} bitte ohne \".exe\" angeben.");

        if (CpuThresholdPercent is <= 0 or > 100)
            errors.Add($"{SectionName}:{nameof(CpuThresholdPercent)} muss größer 0 und höchstens 100 sein.");

        if (AverageWindowMinutes <= 0)
            errors.Add($"{SectionName}:{nameof(AverageWindowMinutes)} muss größer 0 sein.");

        if (SampleIntervalSeconds < 1)
            errors.Add($"{SectionName}:{nameof(SampleIntervalSeconds)} muss mindestens 1 sein.");
        else if (AverageWindowMinutes > 0 && SampleInterval > AverageWindow)
            errors.Add($"{SectionName}:{nameof(SampleIntervalSeconds)} darf nicht größer als das Zeitfenster sein.");

        if (CooldownMinutes < 0)
            errors.Add($"{SectionName}:{nameof(CooldownMinutes)} darf nicht negativ sein.");

        if (StopTimeoutSeconds < 1)
            errors.Add($"{SectionName}:{nameof(StopTimeoutSeconds)} muss mindestens 1 sein.");

        if (StartTimeoutSeconds < 1)
            errors.Add($"{SectionName}:{nameof(StartTimeoutSeconds)} muss mindestens 1 sein.");

        return errors;
    }
}
