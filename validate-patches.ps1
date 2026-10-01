param([string]$GameDir = 'D:/SteamLibrary/steamapps/common/Monster Train 2')
$ErrorActionPreference='Stop'
Add-Type -LiteralPath "$GameDir/BepInEx/core/Mono.Cecil.dll"
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$GameDir/MonsterTrain2_Data/Managed/Assembly-CSharp.dll")
$framework=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$GameDir/BepInEx/plugins/Trainworks/TrainworksReloaded.Base.dll")
$mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot 'bin/Release/netstandard2.1/DevaClan.dll'))
$failures=[System.Collections.Generic.List[string]]::new()
$checked=0
foreach($t in $mod.MainModule.Types) {
    $patch=@($t.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'})
    if(!$patch.Count){continue}
    $a=$patch[0].ConstructorArguments
    $targetType=@($game.MainModule.Types)+@($framework.MainModule.Types) | Where-Object {$_.FullName -eq $a[0].Value.FullName}
    $methods=@($targetType.Methods | Where-Object {$_.Name -eq $a[1].Value})
    if ($a.Count -gt 2) {
        $signature = ($a[2].Value | ForEach-Object {$_.Value.FullName}) -join ','
        $methods = @($methods | Where-Object {(($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join ',') -eq $signature})
    }
    if($methods.Count -ne 1){$failures.Add("Ambiguous/missing patch target $($t.FullName): $($methods.Count)");continue}
    $m=$methods[0]
    foreach($hook in $t.Methods | Where-Object {$_.Name -in @('Prefix','Postfix','Finalizer')}) {
        foreach($p in $hook.Parameters) {
            if($p.Name -match '^___') {
                if(!($targetType.Fields | Where-Object {$_.Name -eq $p.Name.Substring(3)})){$failures.Add("Missing injected field $($t.Name).$($p.Name)")}
            } elseif($p.Name -match '^__'){continue}
            elseif($p.Index -eq 0 -and $p.ParameterType.FullName -eq $hook.ReturnType.FullName -and $hook.ReturnType.FullName -eq $m.ReturnType.FullName){continue}
            elseif(!($m.Parameters | Where-Object {$_.Name -eq $p.Name})){$failures.Add("Missing patched argument $($t.Name).$($p.Name)")}
        }
    }
    $checked++
}
$game.Dispose();$framework.Dispose();$mod.Dispose()
if($failures.Count){$failures | Write-Output;throw 'Patch metadata validation failed.'}
Write-Output "PASS: $checked Harmony targets, named arguments, injected fields and coroutine return signatures match the installed game."
