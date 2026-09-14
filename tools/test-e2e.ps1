# End-to-end test against the real game: builds mod + harness, launches into the throwaway DevTest world and
# drives every feature over the debug socket. Exit code 0 = all checks passed. Screenshots land in tools\out\.
# Timers are shortened through the mod's runtime config; nothing outside the dev profile / DevTest saves is touched.
param([switch]$KeepRunning)
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$vh = Join-Path $PSScriptRoot "vh.ps1"
$out = Join-Path $PSScriptRoot "out"; New-Item -ItemType Directory -Force $out | Out-Null
$profile = if ($env:VALHEIM_DEV_PROFILE) { $env:VALHEIM_DEV_PROFILE } else { "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\dev" }
$log = "$profile\BepInEx\LogOutput.log"
$worlds = "$env:USERPROFILE\AppData\LocalLow\IronGate\Valheim\worlds_local"
$fail = @()
function Check($name, $ok) { if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name"; $script:fail += $name } }
function V($line) { try { & $vh $line } catch { "ERROR: $_" } }
function Log($pattern) { Get-Content $log -ErrorAction SilentlyContinue | Select-String $pattern }
function Boot {
    # Always start from the mod's default config so stale values from earlier builds can't leak into the test.
    Remove-Item "$profile\BepInEx\config\com.night.basedefenseward.cfg" -ErrorAction SilentlyContinue
    # Fresh throwaway world every boot (the harness recreates DevTest); only the test world is touched.
    Get-ChildItem $worlds -Filter "DevTest*" -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
    & (Join-Path $PSScriptRoot "run-dev.ps1") | Out-Null
    for ($i = 0; $i -lt 40; $i++) { Start-Sleep 5; $s = V "state"; if ($s -match "player=True") { return $true } }
    return $false
}
function Stop-Game { try { V "quit" | Out-Null } catch {}; Start-Sleep 4; Stop-Process -Name valheim -Force -ErrorAction SilentlyContinue }
function FastConfig {
    foreach ($kv in "CountdownMinutes 0.1","SpawnRadius 30","WaveTimeLimitMinutes 3","PlayerHuntRange 15","DeleteWorldOnLoss false","BuildDeadlineMinutes 0") { V "mod cfg $kv" | Out-Null }
}

# ---- build ----
Stop-Game
foreach ($p in "mods\base-defense-ward\BaseDefenseWard.csproj","tools\DevHarness\DevHarness.csproj") {
    $b = dotnet build (Join-Path $root $p) -c Release 2>&1 | Select-String "error CS|Build succeeded"
    if ($b -match "error CS") { $b; Write-Host "FAIL build $p"; exit 1 }
}

# ---- phase A: registration, glow, countdown, wave, AI, win, rewards, progression ----
Write-Host "`n== Phase A: win path"
Check "boots into world" (Boot)
$state = V "state"
Check "ward prefab registered" ($state -match "wardPrefab=True")
Check "ward in Hammer table" ((V "pieces basedefenseward") -match "^BaseDefenseWard")
V "god" | Out-Null; FastConfig
$pos = (V "pos") -split " "
Check "placed ward" ((V "place BaseDefenseWard 0 5") -match "zdo=True")
Start-Sleep 2
$t = @(V "toggle") + @(V "toggle")
Check "reddish glow follows activation" ((($t | Where-Object { $_ -match "-> True; enabledEffect active=True" }).Count -eq 1) -and (($t | Where-Object { $_ -match "BaseDefenseWardGlow color=RGBA\(0\.900, 0\.350, 0\.400" }).Count -eq 2))
Check "countdown starts on build" ((V "mod status") -match "state=Countdown")
Check "bdw_built key set" ((V "keys") -match "bdw_built")
Start-Sleep 12
$st = V "mod status"
Check "wave becomes active after countdown" ($st -match "state=Active")
# Leave right away, beyond PlayerHuntRange (15 m here) but inside the active area so the ward's ZDO stays owned:
# with no player in reach the whole wave must go for the ward.
V "tp $([float]$pos[0] + 60) $($pos[1]) $($pos[2])" | Out-Null
Start-Sleep 30
$mobs = V "mod mobs"; $st = V "mod status"
Check "wave mobs spawned" (($mobs | Select-Object -Last 1) -match "count=[1-9]")
# A mob that spawned on the player's side of the ring may legitimately find them; the ones at the ward (< 30 m) must not hunt.
$atWard = @($mobs | Where-Object { $_ -match "\t([0-9]+)m hunt=" -and [int]$Matches[1] -lt 30 })
Check "mobs at the ward don't hunt when no player is in range" (($atWard.Count -gt 0) -and (@($atWard -match "hunt=True").Count -eq 0))
Check "mobs attack the ward (hp < 100% or mobs adjacent)" (($st -match "hp=(\d+)%" -and [int]$Matches[1] -lt 100) -or (($mobs | Where-Object { $_ -match "\t[0-4]m " }).Count -gt 0))
if ($atWard.Count -eq 0 -or @($atWard -match "hunt=True").Count -ne 0) { $mobs; $st }
V "tp $($pos[0]) $($pos[1]) $($pos[2])" | Out-Null; Start-Sleep 5
$mobs = V "mod mobs"
Check "mobs switch to hunting the exposed player who returns" (($mobs -match "hunt=True").Count -gt 0)
V "screenshot $out\wave.png" | Out-Null
for ($i = 0; $i -lt 6; $i++) { V "mod kill" | Out-Null; Start-Sleep 3; if ((V "mod status") -match "state=Won") { break } }
$st = V "mod status"
Check "challenge won when wave is dead" ($st -match "state=Won")
Check "completed counter incremented" ($st -match "completed=1")
$items = V "items 10"
Check "rewards dropped at ward (coins + materials)" (($items -match "^Coins") -and ($items -match "^Flint|^LeatherScraps"))
V "screenshot $out\rewards.png" | Out-Null
# Progression: hanging a trophy on a boss stone unlocks the next tier.
Check "boss stones loaded at spawn" ((V "bossstones") -match "BossStone_Eikthyr")
V "bossstone eikthyr true" | Out-Null; Start-Sleep 2
Check "hung trophy sets bdw_hung_eikthyr" ((V "keys") -match "bdw_hung_eikthyr")
Check "boss tier becomes 1" ((V "mod status") -match "bossTier=1")
$sample = V "mod sample 12 1 0"
Check "tier-1 pool sampled (black forest mobs)" ($sample -match "Greydwarf|Skeleton")
Check "tier-0 sample never contains black forest mobs" (-not ((V "mod sample 20 0 5") -match "Greydwarf|Skeleton"))
Check "tier-1 reward includes copper" ((V "mod reward 1 1") -match "Copper")
V "bossstone eikthyr false" | Out-Null; Start-Sleep 2
Check "removed trophy clears key" (-not ((V "keys") -match "bdw_hung_eikthyr"))

# ---- phase A2: HUD, one ward per player, rebuild-to-move, lock during wave ----
Write-Host "`n== Phase A2: HUD + one ward per player"
$hud = V "mod hud"
Check "HUD visible and shows finished ward" ($hud -match "visible=True" -and $hud -match "Ward defended")
Check "rebuilding through the hammer path succeeds" ((V "build BaseDefenseWard 4 5") -match "^built")
Start-Sleep 3
Check "previous ward removed (only one per player)" ((V "mod wards") -match "^count=1 removed=1")
Check "HUD shows countdown to next wave" ((V "mod hud") -match "Wave in \d\d:\d\d")
V "mod skip 60" | Out-Null; Start-Sleep 4
Check "moved ward's wave starts" ((V "mod status") -match "state=Active")
Check "HUD shows wave in progress" ((V "mod hud") -match "Wave! hold \d\d:\d\d")
Check "placement refused while wave in progress" (((V "tryplace BaseDefenseWard") -match "result=False") -and ((V "mod wards") -match "blocked=1"))
Check "still exactly one ward" ((V "mod wards") -match "^count=1 ")
V "screenshot $out\hud_wave.png" | Out-Null
for ($i = 0; $i -lt 6; $i++) { V "mod kill" | Out-Null; Start-Sleep 3; if ((V "mod status") -match "state=Won") { break } }
Check "moved ward's wave won" ((V "mod status") -match "state=Won")

# ---- phase B: loss + world deletion ----
Write-Host "`n== Phase B: loss with DeleteWorldOnLoss"
V "mod cfg DeleteWorldOnLoss true" | Out-Null
V "destroy basedefense" | Out-Null   # clear the finished ward so the new one is the only piece nearby
Start-Sleep 1
Check "placed second ward" ((V "place BaseDefenseWard 0 5") -match "zdo=True")
Start-Sleep 2; V "mod skip 60" | Out-Null; Start-Sleep 4
Check "second wave active" ((V "mod status") -match "state=Active")
V "destroy basedefense" | Out-Null; Start-Sleep 2
Check "loss logged when ward destroyed mid-wave" ((Log "Challenge lost").Count -ge 1)
Check "world reset scheduled" ((Log "World reset scheduled").Count -ge 1)
Start-Sleep 16
Check "back at start menu after reset" ((V "state") -match "scene=start")
Check "DevTest world files deleted" ((Get-ChildItem $worlds -Filter "DevTest*" -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)
Check "no NullReferenceExceptions during reset" ((Log "NullReferenceException").Count -eq 0)

# ---- phase C: build deadline on a fresh world ----
Write-Host "`n== Phase C: build deadline"
Stop-Game
Check "boots into fresh world" (Boot)
FastConfig
V "mod cfg BuildDeadlineMinutes 5" | Out-Null; Start-Sleep 3
Check "HUD shows build deadline countdown" ((V "mod hud") -match "Build ward in \d\d:\d\d")
V "mod cfg BuildDeadlineMinutes 0.01" | Out-Null
Start-Sleep 4
Check "deadline failure key set" ((V "keys") -match "bdw_deadline_failed")
Check "deadline failure logged" ((Log "Build deadline missed").Count -ge 1)
$errs = Log "\[Error" | Where-Object { $_ -notmatch "Unity Log\] Failed to" }
Check "no errors in log" ($errs.Count -eq 0); if ($errs) { $errs | Select-Object -First 5 }

if (-not $KeepRunning) { Stop-Game }
if ($fail.Count) { Write-Host "`nFAILED ($($fail.Count)): $($fail -join '; ')"; exit 1 }
Write-Host "`nALL PASSED"; exit 0
