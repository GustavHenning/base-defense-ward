# Creates (or refreshes) the isolated "dev" r2modman profile used for all testing.
# It copies ONLY the BepInEx core/patchers/doorstop files from a source profile; no mods, no configs.
# Your other profiles and the game folder are never modified.
param(
    [string]$ProfileName = "dev",
    [string]$SourceProfile = "",   # profile to take BepInEx core from; auto-picks the newest 5.4.23+ if empty
    [string]$ProfilesRoot = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles"
)
$ErrorActionPreference = "Stop"
$dev = Join-Path $ProfilesRoot $ProfileName

if (-not $SourceProfile) {
    $cands = Get-ChildItem $ProfilesRoot -Directory | Where-Object { $_.Name -ne $ProfileName -and (Test-Path "$($_.FullName)\BepInEx\core\BepInEx.Preloader.dll") } |
        ForEach-Object { [pscustomobject]@{ Name=$_.Name; Ver=[version](Get-Item "$($_.FullName)\BepInEx\core\BepInEx.Preloader.dll").VersionInfo.FileVersion } } |
        Sort-Object Ver -Descending
    if (-not $cands) { throw "No profile with a BepInEx core found under $ProfilesRoot. Install BepInExPack_Valheim in any r2modman profile first." }
    $SourceProfile = $cands[0].Name
    if ($cands[0].Ver -lt [version]"5.4.23") { Write-Warning "Newest BepInEx core is $($cands[0].Ver); Unity 6 builds of Valheim need 5.4.23+." }
}
$src = Join-Path $ProfilesRoot $SourceProfile
Write-Host "Source profile: $SourceProfile -> $dev"

New-Item -ItemType Directory -Force "$dev\BepInEx\plugins","$dev\BepInEx\config","$dev\BepInEx\patchers" | Out-Null
foreach ($d in "BepInEx\core","BepInEx\patchers","doorstop_libs") {
    if (Test-Path "$src\$d") { Remove-Item "$dev\$d" -Recurse -Force -ErrorAction SilentlyContinue; Copy-Item "$src\$d" "$dev\$d" -Recurse }
}
foreach ($f in "winhttp.dll","doorstop_config.ini") { if (Test-Path "$src\$f") { Copy-Item "$src\$f" "$dev\$f" -Force } }
if (-not (Test-Path "$dev\BepInEx\config\BepInEx.cfg") -and (Test-Path "$src\BepInEx\config\BepInEx.cfg")) { Copy-Item "$src\BepInEx\config\BepInEx.cfg" "$dev\BepInEx\config\" }
if (-not (Test-Path "$dev\mods.yml")) { "profileName: $ProfileName`nmods: []`n" | Set-Content "$dev\mods.yml" -Encoding utf8 }

Write-Host ("BepInEx core " + (Get-Item "$dev\BepInEx\core\BepInEx.Preloader.dll").VersionInfo.FileVersion + " ready in $dev")
