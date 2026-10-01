param([string]$GameDir = 'D:\SteamLibrary\steamapps\common\Monster Train 2', [string]$DotnetDir = "$PSScriptRoot/../dotnet")
$ErrorActionPreference = 'Stop'
$buildDir = Join-Path $PSScriptRoot 'bin/Release/netstandard2.1'
$testDir = Join-Path $PSScriptRoot 'bin/content-runtime'
New-Item -ItemType Directory -Force $testDir | Out-Null
$sdk = Get-ChildItem "$DotnetDir/sdk" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$argsList = Get-Content -LiteralPath "$buildDir/compile.rsp" | Where-Object { $_ -notmatch '\-out:|\-target:|[\\/]src[\\/]' }
$argsList += '"-target:exe"', ('"-out:' + $testDir + '/ContentRuntimeRegression.dll"'), ('"' + $PSScriptRoot + '/tests/ContentRuntimeRegression.cs"')
$argsList | Set-Content -LiteralPath "$testDir/test.rsp" -Encoding UTF8
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -LiteralPath "$testDir/ContentRuntimeRegression.runtimeconfig.json" -Encoding UTF8
& "$DotnetDir/dotnet.exe" "$($sdk.FullName)/Roslyn/bincore/csc.dll" -noconfig "@$testDir/test.rsp"
if ($LASTEXITCODE -ne 0) { throw 'Content runtime test compilation failed.' }
& "$DotnetDir/dotnet.exe" "$testDir/ContentRuntimeRegression.dll" $GameDir $buildDir "$PSScriptRoot/json"
if ($LASTEXITCODE -ne 0) { throw 'Content runtime regression failed.' }
