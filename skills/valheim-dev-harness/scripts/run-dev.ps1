# Launch Valheim directly (Steam must be running) with the isolated dev profile. No r2modman GUI needed.
# The game folder's winhttp.dll is Doorstop 4.x, hence --doorstop-enabled / --doorstop-target-assembly.
param(
    [string]$GameDir = $(if ($env:VALHEIM_DIR) { $env:VALHEIM_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Valheim" }),
    [string]$Profile = $(if ($env:VALHEIM_DEV_PROFILE) { $env:VALHEIM_DEV_PROFILE } else { "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\dev" }),
    [switch]$NoAutoStart
)
$exe = Join-Path $GameDir "valheim.exe"
$argList = @("--doorstop-enabled","true","--doorstop-target-assembly","$Profile\BepInEx\core\BepInEx.Preloader.dll",
             "-console","-window-mode","borderless","-screen-width","1280","-screen-height","720","-screen-fullscreen","0")
if ($NoAutoStart) { $argList += "-noautostart" }
Remove-Item "$Profile\BepInEx\LogOutput.log" -ErrorAction SilentlyContinue
Start-Process -FilePath $exe -ArgumentList $argList -WorkingDirectory $GameDir
Write-Host "Launched. Log: $Profile\BepInEx\LogOutput.log"
