# Changelog

Alle nennenswerten Änderungen an TaskReset. Das Format folgt [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), die Versionsnummern folgen [Semantic Versioning](https://semver.org/lang/de/).

All notable changes to TaskReset. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] – 2026-11-14

### Neu

- Erste Version: Windows-Dienst, der die CPU-Last eines Prozesses misst und den zugehörigen Dienst neu startet, wenn der Durchschnitt über ein Zeitfenster (Standard 5 Minuten) den Schwellenwert (Standard 95 %) erreicht.
- Konfigurierbare Pause nach jedem Neustart, in der weder gemessen noch neu gestartet wird.
- Hartes Beenden von Dienst- und überwachtem Prozess, wenn der Dienst nicht innerhalb des Timeouts stoppt.
- Meldungen in der Windows-Ereignisanzeige (Anwendungsprotokoll, Quelle `TaskReset`).
- Skripte zum Installieren und Deinstallieren des Dienstes.

### Added

- First version: Windows service that measures the CPU load of a process and restarts the associated service when the average over a time window (default 5 minutes) reaches the threshold (default 95%).
- Configurable pause after each restart, during which nothing is measured or restarted.
- Hard kill of the service process and the monitored process if the service does not stop within the timeout.
- Messages in the Windows Event Log (Application log, source `TaskReset`).
- Scripts to install and uninstall the service.

[1.0.0]: https://github.com/vulture20/TaskReset/releases/tag/v1.0.0
