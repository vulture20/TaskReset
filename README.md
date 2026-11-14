<p align="center">
  <b>🇬🇧 English version: <a href="#taskreset-english">see below</a></b>
</p>

# TaskReset

[![CI](https://github.com/vulture20/TaskReset/actions/workflows/ci.yml/badge.svg)](https://github.com/vulture20/TaskReset/actions/workflows/ci.yml)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

TaskReset ist ein kleiner Windows-Dienst, der einen Prozess überwacht. Belastet der Prozess die CPU im Durchschnitt der letzten 5 Minuten mit mindestens 95 %, startet TaskReset den Windows-Dienst neu, zu dem der Prozess gehört. Danach legt TaskReset eine Pause ein, bevor erneut gemessen und gegebenenfalls wieder neu gestartet wird. Alle Werte sind einstellbar.

## So funktioniert es

1. Alle `SampleIntervalSeconds` (Standard: 5 s) misst TaskReset die verbrauchte Prozessorzeit aller Prozesse mit dem Namen `ProcessName`. Laufen mehrere Instanzen, wird ihre Last addiert.
2. Die CPU-Last ist auf alle Kerne bezogen: 100 % heißt, der ganze Rechner ist ausgelastet. Das entspricht der Anzeige im Task-Manager.
3. Aus den Messungen bildet TaskReset einen gleitenden, zeitgewichteten Durchschnitt über `AverageWindowMinutes` (Standard: 5 min). Ausgewertet wird erst, wenn ein volles Zeitfenster gemessen wurde.
4. Erreicht der Durchschnitt `CpuThresholdPercent` (Standard: 95 %), wird der Dienst `ServiceName` neu gestartet:
   - Zuerst wird der Dienst normal gestoppt. TaskReset wartet dabei bis zu `StopTimeoutSeconds`.
   - Stoppt er nicht rechtzeitig, werden der Dienstprozess und der überwachte Prozess samt Unterprozessen hart beendet.
   - Läuft der überwachte Prozess nach dem Stoppen noch, wird er ebenfalls beendet.
   - Dann wird der Dienst gestartet. Abhängige Dienste, die vorher liefen, werden wieder gestartet.
5. Danach folgt die Pause von `CooldownMinutes` (Standard: 30 min). In dieser Zeit wird weder gemessen noch neu gestartet. Die Pause gilt auch, wenn der Neustart fehlgeschlagen ist. Nach der Pause beginnt die Messung von vorn.

## Installation

Voraussetzungen: Windows 10/11 oder Windows Server (x64) und Administratorrechte.

Unter [Releases](../../releases) gibt es je Version zwei ZIP-Pakete. Beide enthalten `TaskReset.exe`, `appsettings.json`, `install.ps1`, `uninstall.ps1`, `README.md`, `CHANGELOG.md` und `LICENSE`:

| Paket | Größe | Voraussetzung |
|---|---|---|
| `TaskReset-v<Version>-win-x64-framework-dependent.zip` | klein | **.NET 10 Runtime (x64)** |
| `TaskReset-v<Version>-win-x64-self-contained.zip` | groß (Runtime enthalten) | keine |

1. ZIP in einen festen Ordner entpacken, zum Beispiel `C:\Program Files\TaskReset`.
2. `appsettings.json` anpassen (siehe [Konfiguration](#konfiguration)).
3. PowerShell als Administrator öffnen und im Ordner ausführen:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\install.ps1
   ```

Das Skript legt die Ereignisquelle `TaskReset` an, registriert den Dienst `TaskReset` mit automatischem Start unter dem Konto LocalSystem, richtet einen Neustart bei Absturz ein und startet den Dienst. Mit `-NoStart` wird er nur installiert.

### Deinstallation

```powershell
.\uninstall.ps1                      # Dienst stoppen und entfernen
.\uninstall.ps1 -RemoveEventSource   # zusätzlich die Ereignisquelle entfernen
```

### Konfiguration ändern

Die `appsettings.json` wird nur beim Start gelesen. Nach einer Änderung den Dienst neu starten:

```powershell
Restart-Service TaskReset
```

## Konfiguration

Abschnitt `TaskReset` in der `appsettings.json` neben der `TaskReset.exe`:

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `ServiceName` | – | Dienstname (nicht Anzeigename) des Dienstes, der neu gestartet wird. Steht in den Diensteigenschaften unter „Dienstname“. |
| `ProcessName` | – | Name des überwachten Prozesses **ohne** `.exe`, zum Beispiel `MeinProzess`. |
| `CpuThresholdPercent` | `95` | Schwellenwert in Prozent der Gesamt-CPU (größer 0 bis 100). |
| `AverageWindowMinutes` | `5` | Zeitfenster des Durchschnitts in Minuten. |
| `SampleIntervalSeconds` | `5` | Abstand zwischen zwei Messungen in Sekunden (mindestens 1). |
| `CooldownMinutes` | `30` | Pause nach einem Neustart in Minuten. `0` heißt keine Pause, aber die Messung beginnt trotzdem von vorn. |
| `StopTimeoutSeconds` | `60` | So lange wird auf das saubere Stoppen gewartet, danach wird hart beendet. |
| `StartTimeoutSeconds` | `60` | So lange wird gewartet, bis der Dienst nach dem Start läuft. |

Beispiel:

```json
{
  "TaskReset": {
    "ServiceName": "MeinDienst",
    "ProcessName": "MeinProzess",
    "CpuThresholdPercent": 95,
    "AverageWindowMinutes": 5,
    "SampleIntervalSeconds": 5,
    "CooldownMinutes": 30,
    "StopTimeoutSeconds": 60,
    "StartTimeoutSeconds": 60
  }
}
```

Ist die Konfiguration ungültig, schreibt TaskReset die Fehler in die Ereignisanzeige (Ereignis-ID 4001) und beendet sich.

## Ereignisanzeige

TaskReset schreibt in **Windows-Protokolle > Anwendung**, Quelle **TaskReset**:

| ID | Ebene | Bedeutung |
|---|---|---|
| 1000 | Informationen | Dienst gestartet, mit den aktiven Einstellungen |
| 1001 | Informationen | Dienst beendet |
| 2000 | Warnung | Schwellenwert überschritten, Neustart beginnt |
| 2001 | Informationen | Dienst erfolgreich neu gestartet, Pause beginnt |
| 2002 | Informationen | Pause beendet, Messung beginnt von vorn |
| 3000 | Warnung | Überwachter Prozess läuft nicht |
| 3001 | Warnung | Prozess wurde hart beendet |
| 3002 | Warnung | Abhängiger Dienst konnte nicht wieder gestartet werden |
| 4000 | Fehler | Neustart fehlgeschlagen |
| 4001 | Fehler | Ungültige Konfiguration |
| 5000 | Informationen | Status alle 15 Minuten (aktueller Durchschnitt) |

Filtern zum Beispiel mit:

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'TaskReset' } -MaxEvents 20
```

## Selbst bauen

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build
dotnet test
.\scripts\publish.ps1                 # framework-abhängig nach .\publish
.\scripts\publish.ps1 -SelfContained  # mit Runtime
```

`publish.ps1` legt `TaskReset.exe`, `appsettings.json`, `install.ps1` und `uninstall.ps1` in den Ordner `publish`.

Zum Ausprobieren ohne Installation lässt sich TaskReset auch in einer Administrator-Konsole im Vordergrund starten: `dotnet run --project src/TaskReset`. Die Meldungen erscheinen dann zusätzlich in der Konsole.

Ein neues Release entsteht durch einen Tag der Form `vX.Y.Z`. Dafür muss es in `CHANGELOG.md` einen Abschnitt `## [X.Y.Z]` geben.

## Lizenz

Copyright © 2026 Thorsten Schröpel

Dieses Programm ist freie Software unter der [GNU Affero General Public License v3.0](LICENSE) oder einer späteren Version.

---

<a id="taskreset-english"></a>

# TaskReset (English)

TaskReset is a small Windows service that monitors a process. If the process has loaded the CPU with at least 95% on average over the last 5 minutes, TaskReset restarts the Windows service the process belongs to. Afterwards TaskReset pauses before it measures again and possibly restarts again. All values are configurable.

## How it works

1. Every `SampleIntervalSeconds` (default: 5 s) TaskReset measures the processor time used by all processes named `ProcessName`. If several instances are running, their load is added up.
2. The CPU load refers to all cores: 100% means the whole machine is busy. This matches what Task Manager shows.
3. From the samples TaskReset computes a sliding, time-weighted average over `AverageWindowMinutes` (default: 5 min). Nothing is evaluated until a full window has been measured.
4. If the average reaches `CpuThresholdPercent` (default: 95%), the service `ServiceName` is restarted:
   - First the service is stopped normally. TaskReset waits up to `StopTimeoutSeconds`.
   - If it does not stop in time, the service process and the monitored process, including child processes, are killed.
   - If the monitored process is still running after the stop, it is killed as well.
   - Then the service is started. Dependent services that were running before are started again.
5. Then comes the pause of `CooldownMinutes` (default: 30 min). During this time nothing is measured or restarted. The pause also applies if the restart failed. After the pause, measuring starts over.

## Installation

Requirements: Windows 10/11 or Windows Server (x64) and administrator rights.

[Releases](../../releases) has two ZIP packages per version. Both contain `TaskReset.exe`, `appsettings.json`, `install.ps1`, `uninstall.ps1`, `README.md`, `CHANGELOG.md` and `LICENSE`:

| Package | Size | Requirement |
|---|---|---|
| `TaskReset-v<version>-win-x64-framework-dependent.zip` | small | **.NET 10 Runtime (x64)** |
| `TaskReset-v<version>-win-x64-self-contained.zip` | large (runtime included) | none |

1. Unzip into a permanent folder, for example `C:\Program Files\TaskReset`.
2. Edit `appsettings.json` (see [Configuration](#configuration)).
3. Open PowerShell as administrator and run in that folder:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\install.ps1
   ```

The script creates the event source `TaskReset`, registers the service `TaskReset` with automatic start under the LocalSystem account, sets up a restart on crash and starts the service. With `-NoStart` it is only installed.

### Uninstall

```powershell
.\uninstall.ps1                      # stop and remove the service
.\uninstall.ps1 -RemoveEventSource   # also remove the event source
```

### Changing the configuration

`appsettings.json` is only read at startup. Restart the service after a change:

```powershell
Restart-Service TaskReset
```

## Configuration

Section `TaskReset` in the `appsettings.json` next to `TaskReset.exe`:

| Setting | Default | Meaning |
|---|---|---|
| `ServiceName` | – | Service name (not display name) of the service to restart. Shown in the service properties as "Service name". |
| `ProcessName` | – | Name of the monitored process **without** `.exe`, for example `MyProcess`. |
| `CpuThresholdPercent` | `95` | Threshold in percent of total CPU (greater than 0, up to 100). |
| `AverageWindowMinutes` | `5` | Time window of the average in minutes. |
| `SampleIntervalSeconds` | `5` | Interval between two samples in seconds (at least 1). |
| `CooldownMinutes` | `30` | Pause after a restart in minutes. `0` means no pause, but measuring still starts over. |
| `StopTimeoutSeconds` | `60` | How long to wait for a clean stop before killing. |
| `StartTimeoutSeconds` | `60` | How long to wait for the service to be running after the start. |

If the configuration is invalid, TaskReset writes the errors to the Event Log (event ID 4001) and exits.

## Event Log

TaskReset writes to **Windows Logs > Application**, source **TaskReset**:

| ID | Level | Meaning |
|---|---|---|
| 1000 | Information | Service started, with the active settings |
| 1001 | Information | Service stopped |
| 2000 | Warning | Threshold exceeded, restart begins |
| 2001 | Information | Service restarted successfully, pause begins |
| 2002 | Information | Pause over, measuring starts over |
| 3000 | Warning | Monitored process is not running |
| 3001 | Warning | A process was killed |
| 3002 | Warning | A dependent service could not be started again |
| 4000 | Error | Restart failed |
| 4001 | Error | Invalid configuration |
| 5000 | Information | Status every 15 minutes (current average) |

## Building

Requirement: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build
dotnet test
.\scripts\publish.ps1                 # framework-dependent into .\publish
.\scripts\publish.ps1 -SelfContained  # with runtime
```

To try TaskReset without installing it, run it in the foreground from an administrator console: `dotnet run --project src/TaskReset`. Messages then also appear in the console.

A new release is created by a tag of the form `vX.Y.Z`. `CHANGELOG.md` must contain a section `## [X.Y.Z]` for it.

## License

Copyright © 2026 Thorsten Schröpel

This program is free software under the [GNU Affero General Public License v3.0](LICENSE) or any later version.
