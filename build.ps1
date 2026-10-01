param([string]$GameDir = 'D:\SteamLibrary\steamapps\common\Monster Train 2', [string]$DotnetDir = "$PSScriptRoot/../dotnet")
$ErrorActionPreference = 'Stop'
$outDir = Join-Path $PSScriptRoot 'bin/Release/netstandard2.1'
New-Item -ItemType Directory -Force $outDir | Out-Null
$sdk = Get-ChildItem "$DotnetDir/sdk" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$pack = Get-ChildItem "$DotnetDir/packs/NETStandard.Library.Ref" -Directory | Select-Object -First 1
$argsList = @('-nostdlib+','-target:library','-langversion:latest','-optimize+','-deterministic+',"-out:$outDir/DevaClan.dll")
$refs = @(Get-ChildItem "$($pack.FullName)/ref/netstandard2.1/*.dll")
$refs += Get-ChildItem "$GameDir/MonsterTrain2_Data/Managed/*.dll" | Where-Object { $_.Name -notmatch '^(System|mscorlib|netstandard)' }
$refs += Get-Item "$GameDir/BepInEx/core/BepInEx.dll", "$GameDir/BepInEx/core/0Harmony.dll"
$refs += Get-ChildItem "$GameDir/BepInEx/plugins/Trainworks" -Recurse -Filter '*.dll'
$refs += Get-ChildItem "$GameDir/BepInEx/plugins/Conductor" -Recurse -Filter 'Conductor.dll'
$argsList += $refs | ForEach-Object { '-r:' + $_.FullName }
$argsList += Get-ChildItem "$PSScriptRoot/src/*.cs" | ForEach-Object { $_.FullName }
$responseFile = Join-Path $outDir 'compile.rsp'
$argsList | ForEach-Object { '"' + $_ + '"' } | Set-Content -Encoding UTF8 $responseFile
& "$DotnetDir/dotnet.exe" "$($sdk.FullName)/Roslyn/bincore/csc.dll" -noconfig "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
