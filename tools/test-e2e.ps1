# End-to-end test: build mod + harness, launch the game into the test world, exercise the ward, screenshot, quit.
# Exit code 0 = all checks passed. Screenshots land in tools\out\ (git-ignored).
param([switch]$KeepRunning, [int]$BootSeconds = 55)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$vh = Join-Path $PSScriptRoot "vh.ps1"
$out = Join-Path $PSScriptRoot "out"; New-Item -ItemType Directory -Force $out | Out-Null
$profile = if ($env:VALHEIM_DEV_PROFILE) { $env:VALHEIM_DEV_PROFILE } else { "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\dev" }
$log = "$profile\BepInEx\LogOutput.log"
$fail = @()
function Check($name, $ok) { if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name"; $script:fail += $name } }

# Kill a leftover instance, build, launch.
try { & $vh "quit" | Out-Null; Start-Sleep 5 } catch {}
Stop-Process -Name valheim -Force -ErrorAction SilentlyContinue
foreach ($p in "mods\base-defense-ward\BaseDefenseWard.csproj","tools\DevHarness\DevHarness.csproj") {
    $b = dotnet build (Join-Path $root $p) -c Release 2>&1 | Select-String "error CS|Build succeeded"
    if ($b -match "error CS") { $b; throw "build failed: $p" }
}
& (Join-Path $PSScriptRoot "run-dev.ps1")
Start-Sleep $BootSeconds

$state = & $vh "state"
Check "in-world (player spawned)" ($state -match "player=True")
Check "ward prefab registered" ($state -match "wardPrefab=True")
Check "ward in Hammer table" ((& $vh "pieces basedefenseward") -match "^BaseDefenseWard")
& $vh "god" | Out-Null
& $vh "console skiptime 1200" | Out-Null; Start-Sleep 2
Check "placed ward" ((& $vh "place BaseDefenseWard 0 4") -match "zdo=True")
Start-Sleep 2
# Initial enabled state depends on the saved world, so toggle twice and check we saw both states.
$t = @(& $vh "toggle") + @(& $vh "toggle")
$off = ($t | Where-Object { $_ -match "-> False; enabledEffect active=False" }).Count -eq 1
$on  = ($t | Where-Object { $_ -match "-> True; enabledEffect active=True" }).Count -eq 1
$blue = ($t | Where-Object { $_ -match "BaseDefenseWardGlow color=RGBA\(0\.200, 0\.500, 1\.000" }).Count -eq 2
Check "toggle off disables glow" $off
Check "toggle on enables blue glow" ($on -and $blue)
if (-not ($off -and $on -and $blue)) { $t }
# Leave it enabled for the screenshot.
if (($t | Select-Object -Last 3 | Select-Object -First 1) -match "-> False") { & $vh "toggle" | Out-Null }
Start-Sleep 2
& $vh "screenshot $out\ward_on.png" | Out-Null; Start-Sleep 3
Check "screenshot written" (Test-Path "$out\ward_on.png")
$errs = Get-Content $log | Select-String "Error|Exception" | Where-Object { $_ -notmatch "Unity Log" }
Check "no plugin errors in log" (-not $errs)
if ($errs) { $errs | Select-Object -First 10 }

if (-not $KeepRunning) { try { & $vh "quit" | Out-Null } catch {} }
if ($fail.Count) { Write-Host "`nFAILED: $($fail -join ', ')"; exit 1 }
Write-Host "`nALL PASSED"; exit 0
