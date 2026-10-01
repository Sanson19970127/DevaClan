param([string]$GameDir = 'D:\SteamLibrary\steamapps\common\Monster Train 2')
$ErrorActionPreference = 'Stop'
try {
    if (!(Test-Path -LiteralPath (Join-Path $GameDir 'MonsterTrain2.exe'))) {
        $GameDir = Read-Host 'Enter the folder containing MonsterTrain2.exe'
    }
    $GameDir = [IO.Path]::GetFullPath($GameDir.Trim('"'))
    if (!(Test-Path -LiteralPath (Join-Path $GameDir 'MonsterTrain2.exe'))) { throw 'MonsterTrain2.exe was not found in the selected folder.' }
    if (Get-Process -Name MonsterTrain2 -ErrorAction SilentlyContinue) { throw 'Close Monster Train 2 before installing.' }
    $pluginsDir=Join-Path $GameDir 'BepInEx/plugins'
    if (!(Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx/core/BepInEx.dll'))) { throw 'Install BepInEx 5 first. See README.' }
    foreach($dependency in @('TrainworksReloaded.Base.dll','Conductor.dll')) {
        if (!(Get-ChildItem -LiteralPath $pluginsDir -Filter $dependency -Recurse -File -ErrorAction SilentlyContinue)) { throw "Missing dependency: $dependency. See README." }
    }
    $source=Join-Path $PSScriptRoot 'BepInEx/plugins/DevaClan'
    if (!(Test-Path -LiteralPath (Join-Path $source 'DevaClan.dll'))) { throw 'Extract the entire ZIP before running the installer.' }
    $dest=[IO.Path]::GetFullPath((Join-Path $pluginsDir 'DevaClan'))
    if (!$dest.StartsWith(([IO.Path]::GetFullPath($pluginsDir)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid destination path.' }
    $backup=Join-Path $GameDir ('BepInEx/DevaClan-backups/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    if (Test-Path -LiteralPath $dest) {
        New-Item -ItemType Directory -Force $backup | Out-Null
        Move-Item -LiteralPath $dest -Destination (Join-Path $backup 'DevaClan')
    }
    $legacy=Join-Path $pluginsDir 'TianRen.Plugin.dll'
    if (Test-Path -LiteralPath $legacy) {
        New-Item -ItemType Directory -Force $backup | Out-Null
        Move-Item -LiteralPath $legacy -Destination (Join-Path $backup 'TianRen.Plugin.dll')
    }
    Copy-Item -LiteralPath $source -Destination $pluginsDir -Recurse
    Write-Host 'Deva Clan 0.2.9 installed. Gameplay bugfix candidate; complete the in-game acceptance checklist.'
    Write-Host 'Language follows the game setting: English / Simplified Chinese.'
    Write-Host ('Previous versions, if any, are preserved at: '+$backup)
} catch { Write-Error $_; exit 1 }
