<#
.SYNOPSIS
    Baut TaskReset als einzelne exe und legt alles für die Installation in einen Ordner.
    Builds TaskReset as a single exe and puts everything needed for installation into one folder.

.PARAMETER Output
    Zielordner. Standard: publish im Repository.

.PARAMETER SelfContained
    Die .NET-Runtime mit einpacken (größere exe, keine Voraussetzungen).
#>
[CmdletBinding()]
param(
    [string] $Output = (Join-Path $PSScriptRoot '..\publish'),
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\TaskReset\TaskReset.csproj'
$sc = if ($SelfContained) { 'true' } else { 'false' }

dotnet publish $project -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=$sc -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen ($LASTEXITCODE)." }

Copy-Item (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'uninstall.ps1') $Output -Force
Write-Host "Fertig: $((Resolve-Path $Output).Path)"
Write-Host "Dort appsettings.json anpassen und als Administrator .\install.ps1 ausführen."
