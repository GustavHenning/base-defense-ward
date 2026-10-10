# Install a packaged mod zip into an r2modman profile exactly the way a manual user would: unpack it into
# BepInEx\plugins\<Team-Name>\. Creates the profile (BepInEx core copied from an existing one) if needed.
# Usage: install-to-profile.ps1 -Zip dist\BaseDefenseWard-0.1.0.zip [-ProfileName release-test] [-Launch]
#   -Launch starts the game with that profile (no dev harness, no auto-start) and waits for the plugin to load.
# Only the named profile is touched. Never point this at your everyday profile unless you mean to install there.
param(
    [Parameter(Mandatory = $true)][string]$Zip,
    [string]$ProfileName = "release-test",
    [string]$Team = "",
    [switch]$Launch,
    [switch]$Clean,
    [string]$GameDir = $(if ($env:VALHEIM_DIR) { $env:VALHEIM_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Valheim" }),
    [string]$ProfilesRoot = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$Zip = Resolve-Path $Zip
$profile = Join-Path $ProfilesRoot $ProfileName
if ($Clean -and (Test-Path $profile)) { Remove-Item $profile -Recurse -Force }
if (-not (Test-Path "$profile\BepInEx\core\BepInEx.Preloader.dll")) {
    & (Join-Path $root "skills\valheim-dev-harness\scripts\setup-profile.ps1") -ProfileName $ProfileName -ProfilesRoot $ProfilesRoot
}

$tmp = Join-Path $env:TEMP "modinstall-$([guid]::NewGuid())"
Expand-Archive $Zip $tmp
$manifest = Get-Content "$tmp\manifest.json" -Raw | ConvertFrom-Json
$folder = if ($Team) { "$Team-$($manifest.name)" } else { $manifest.name }
$dest = "$profile\BepInEx\plugins\$folder"
Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $dest | Out-Null
# r2modman flattens plugins\ into the mod folder; do the same so the layout matches a mod-manager install.
if (Test-Path "$tmp\plugins") { Copy-Item "$tmp\plugins\*" $dest -Recurse }
Copy-Item "$tmp\manifest.json", "$tmp\README.md", "$tmp\icon.png" $dest -ErrorAction SilentlyContinue
if (Test-Path "$tmp\config") { New-Item -ItemType Directory -Force "$profile\BepInEx\config" | Out-Null; Copy-Item "$tmp\config\*" "$profile\BepInEx\config" -Recurse -Force }
Remove-Item $tmp -Recurse -Force
Write-Host "Installed $($manifest.name) $($manifest.version_number) into $dest"
Get-ChildItem $dest | ForEach-Object { Write-Host "  $($_.Name)" }

if ($Launch) {
    $log = "$profile\BepInEx\LogOutput.log"
    Remove-Item $log -ErrorAction SilentlyContinue
    $argList = @("--doorstop-enabled", "true", "--doorstop-target-assembly", "$profile\BepInEx\core\BepInEx.Preloader.dll",
                 "-console", "-window-mode", "borderless", "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0")
    Start-Process -FilePath (Join-Path $GameDir "valheim.exe") -ArgumentList $argList -WorkingDirectory $GameDir
    Write-Host "Launched with profile $ProfileName; waiting for the plugin to load (log: $log)"
    $loaded = $false
    for ($i = 0; $i -lt 36; $i++) {
        Start-Sleep 5
        # BepInEx logs the plugin's display name ("Loading [Base Defense Ward 0.1.0]"); compare without spaces.
        $lines = @(Get-Content $log -ErrorAction SilentlyContinue | Where-Object { $_ -match "Loading \[" }) -replace " ", ""
        if ($lines -match [regex]::Escape("Loading[$($manifest.name)$($manifest.version_number)]")) { $loaded = $true; break }
    }
    $errors = @(Get-Content $log -ErrorAction SilentlyContinue | Select-String "\[Error" | Where-Object { $_ -notmatch "Unity Log\] Failed to" })
    Write-Host ("plugin loaded=" + $loaded + " errors=" + $errors.Count)
    $errors | Select-Object -First 5
    Stop-Process -Name valheim -Force -ErrorAction SilentlyContinue
    if (-not $loaded -or $errors.Count) { exit 1 }
}
