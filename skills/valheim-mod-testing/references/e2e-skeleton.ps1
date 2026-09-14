# Skeleton for mods/<mod>/tests/e2e.ps1. Copy, then replace the "phase" blocks with the mod's checks (TESTING.md).
# Exit code 0 = every check passed. Screenshots go to .out\ at the repo root. Only the isolated dev profile and
# the throwaway DevTest saves are touched.
param([switch]$KeepRunning)
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")          # repo root
$harness = Join-Path $root "skills/valheim-dev-harness"
$vh = Join-Path $harness "scripts/vh.ps1"
$out = Join-Path $root ".out"; New-Item -ItemType Directory -Force $out | Out-Null
$profile = if ($env:VALHEIM_DEV_PROFILE) { $env:VALHEIM_DEV_PROFILE } else { "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\dev" }
$log = "$profile\BepInEx\LogOutput.log"
$worlds = "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\worlds_local"
$modCsproj = "mods\<mod>\<Mod>.csproj"
$modConfig = "<plugin guid>.cfg"
$fail = @()
function Check($name, $ok) { if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name"; $script:fail += $name } }
function V($line) { try { & $vh $line } catch { "ERROR: $_" } }
function Log($pattern) { Get-Content $log -ErrorAction SilentlyContinue | Select-String $pattern }
function Boot {
    Remove-Item "$profile\BepInEx\config\$modConfig" -ErrorAction SilentlyContinue          # always default config
    Get-ChildItem $worlds -Filter "DevTest*" -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse   # fresh world
    & (Join-Path $harness "scripts/run-dev.ps1") | Out-Null
    for ($i = 0; $i -lt 40; $i++) { Start-Sleep 5; $s = V "state"; if ($s -match "player=True") { return $true } }
    return $false
}
function Stop-Game { try { V "quit" | Out-Null } catch {}; Start-Sleep 4; Stop-Process -Name valheim -Force -ErrorAction SilentlyContinue }

# ---- build ----
Stop-Game
foreach ($p in $modCsproj, "skills/valheim-dev-harness/DevHarness/DevHarness.csproj") {
    $b = dotnet build (Join-Path $root $p) -c Release 2>&1 | Select-String "error CS|Build succeeded"
    if ($b -match "error CS") { $b; Write-Host "FAIL build $p"; exit 1 }
}

# ---- phase 1 ----
Write-Host "`n== Phase 1: registration"
Check "boots into world" (Boot)
Check "prefab registered" ((V "state <Prefab>") -match "prefab <Prefab>=True")
V "god" | Out-Null
# ... V "mod cfg <key> <fast value>", V "place <Prefab> 0 5", Check ... , V "screenshot $out\<name>.png"

# ---- wrap up ----
$errs = Log "\[Error" | Where-Object { $_ -notmatch "Unity Log\] Failed to" }
Check "no errors in log" ($errs.Count -eq 0); if ($errs) { $errs | Select-Object -First 5 }
if (-not $KeepRunning) { Stop-Game }
if ($fail.Count) { Write-Host "`nFAILED ($($fail.Count)): $($fail -join '; ')"; exit 1 }
Write-Host "`nALL PASSED"; exit 0
