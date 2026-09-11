$asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path "SenangRetails.Shared/EBIDM.dll"))
try {
    $types = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    $types = $_.Exception.Types | Where-Object { $_ -ne $null }
}
Write-Host "Total types: $($types.Count)"
$types | Select-Object -First 30 | ForEach-Object { Write-Host $_.FullName }
