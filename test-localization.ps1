param([string]$GameDir = 'D:\SteamLibrary\steamapps\common\Monster Train 2', [string]$DotnetDir = "$PSScriptRoot/../dotnet")
$ErrorActionPreference = 'Stop'
$buildDir = Join-Path $PSScriptRoot 'bin/Release/netstandard2.1'
$testDir = Join-Path $PSScriptRoot 'bin/localization-test'
New-Item -ItemType Directory -Force $testDir | Out-Null
$sdk = Get-ChildItem "$DotnetDir/sdk" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$argsList = Get-Content -LiteralPath "$buildDir/compile.rsp" | Where-Object { $_ -notmatch '\-out:|\-target:|[\\/]src[\\/]' }
$argsList += '"-target:exe"', ('"-out:' + $testDir + '/LocalizationRegression.dll"'), ('"' + $PSScriptRoot + '/tests/LocalizationRegression.cs"')
$argsList | Set-Content -LiteralPath "$testDir/test.rsp" -Encoding UTF8
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath "$testDir/LocalizationRegression.runtimeconfig.json" -Encoding UTF8
& "$DotnetDir/dotnet.exe" "$($sdk.FullName)/Roslyn/bincore/csc.dll" -noconfig "@$testDir/test.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Localization test compilation failed.' }
& "$DotnetDir/dotnet.exe" "$testDir/LocalizationRegression.dll" $GameDir $buildDir
if ($LASTEXITCODE -ne 0) { throw 'Localization regression failed.' }
