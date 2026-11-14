#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installiert TaskReset als Windows-Dienst und startet ihn.
    Installs TaskReset as a Windows service and starts it.

.DESCRIPTION
    Legt die Ereignisquelle "TaskReset" im Anwendungsprotokoll an, registriert den Dienst
    mit automatischem Start unter dem Konto LocalSystem, richtet einen Neustart bei Absturz ein
    und startet den Dienst. Vorher bitte die appsettings.json neben der TaskReset.exe anpassen.

.PARAMETER ExePath
    Pfad zur TaskReset.exe. Standard: TaskReset.exe im Ordner dieses Skripts.

.PARAMETER NoStart
    Dienst nur installieren, nicht starten.

.EXAMPLE
    .\install.ps1
#>
[CmdletBinding()]
param(
    [string] $ExePath = (Join-Path $PSScriptRoot 'TaskReset.exe'),
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'
$serviceName = 'TaskReset'
$displayName = 'TaskReset'
$description = 'Überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst bei Dauerlast neu.'
$eventSource = 'TaskReset'

if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "TaskReset.exe nicht gefunden: $ExePath"
}
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path

$config = Join-Path (Split-Path $ExePath) 'appsettings.json'
if (-not (Test-Path -LiteralPath $config)) {
    throw "appsettings.json nicht gefunden: $config"
}

# Die framework-abhängige exe ist klein und braucht die .NET 10 Runtime, die self-contained-Variante nicht.
if ((Get-Item -LiteralPath $ExePath).Length -lt 20MB) {
    $runtimes = @()
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $runtimes = @(& dotnet --list-runtimes 2>$null | Where-Object { $_ -match '^Microsoft\.NETCore\.App 10\.' })
    }
    if ($runtimes.Count -eq 0) {
        throw ("Diese TaskReset.exe ist die framework-abhängige Variante und braucht die .NET 10 Runtime (x64), " +
               "die auf diesem Rechner fehlt. Entweder die Runtime installieren " +
               "(https://dotnet.microsoft.com/download/dotnet/10.0) oder das self-contained-ZIP verwenden.")
    }
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    throw "Der Dienst $serviceName ist bereits installiert. Zum Aktualisieren erst .\uninstall.ps1 ausführen."
}

if (-not [System.Diagnostics.EventLog]::SourceExists($eventSource)) {
    [System.Diagnostics.EventLog]::CreateEventSource($eventSource, 'Application')
    Write-Host "Ereignisquelle $eventSource angelegt."
}

New-Service -Name $serviceName `
    -BinaryPathName "`"$ExePath`"" `
    -DisplayName $displayName `
    -Description $description `
    -StartupType Automatic | Out-Null
Write-Host "Dienst $serviceName installiert ($ExePath)."

# Bei einem Absturz nach 1 Minute neu starten, Fehlerzähler nach einem Tag zurücksetzen.
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Warning "Wiederherstellungsoptionen konnten nicht gesetzt werden (sc.exe: $LASTEXITCODE)." }

if (-not $NoStart) {
    $startTime = Get-Date
    try {
        Start-Service -Name $serviceName -ErrorAction Stop
    }
    catch {
        Write-Warning "Der Dienst $serviceName ist installiert, ließ sich aber nicht starten: $($_.Exception.Message)"
        Start-Sleep -Seconds 2
        $events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $startTime.AddSeconds(-5) } -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -in $eventSource, '.NET Runtime', 'Application Error' } |
            Select-Object -First 5
        if ($events) {
            Write-Host "Letzte Meldungen in der Ereignisanzeige:"
            $events | Format-List TimeCreated, ProviderName, Id, Message | Out-String -Width 200 | Write-Host
        }
        else {
            Write-Host "Keine passenden Meldungen in der Ereignisanzeige. Zum Testen die exe direkt starten: & `"$ExePath`""
        }
        Write-Host "Nach dem Beheben der Ursache: Start-Service $serviceName"
        exit 1
    }
    Write-Host "Dienst $serviceName gestartet. Meldungen stehen in der Ereignisanzeige unter Windows-Protokolle > Anwendung, Quelle $eventSource."
}
