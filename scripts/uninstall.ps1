#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Stoppt und entfernt den Windows-Dienst TaskReset.
    Stops and removes the TaskReset Windows service.

.PARAMETER RemoveEventSource
    Entfernt zusätzlich die Ereignisquelle "TaskReset" aus dem Anwendungsprotokoll.

.EXAMPLE
    .\uninstall.ps1
#>
[CmdletBinding()]
param(
    [switch] $RemoveEventSource
)

$ErrorActionPreference = 'Stop'
$serviceName = 'TaskReset'
$eventSource = 'TaskReset'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        Write-Host "Dienst $serviceName gestoppt."
    }
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe delete ist fehlgeschlagen ($LASTEXITCODE)." }
    Write-Host "Dienst $serviceName entfernt."
}
else {
    Write-Host "Dienst $serviceName ist nicht installiert."
}

if ($RemoveEventSource -and [System.Diagnostics.EventLog]::SourceExists($eventSource)) {
    [System.Diagnostics.EventLog]::DeleteEventSource($eventSource)
    Write-Host "Ereignisquelle $eventSource entfernt."
}
