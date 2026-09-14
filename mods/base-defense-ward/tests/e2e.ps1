# End-to-end test for Base Defense Ward against the real game (see TESTING.md): builds mod + harness, launches into the throwaway DevTest world and
# drives every feature over the debug socket. Exit code 0 = all checks passed. Screenshots land in .out at the repo root.
# Timers are shortened through the mod's runtime config; nothing outside the dev profile / DevTest saves is touched.
param([switch]$KeepRunning)
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "../../..")          # repo root
$harness = Join-Path $root "skills/valheim-dev-harness"
$vh = Join-Path $harness "scripts/vh.ps1"
$out = Join-Path $root ".out"; New-Item -ItemType Directory -Force $out | Out-Null
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
    & (Join-Path $harness "scripts/run-dev.ps1") | Out-Null
    for ($i = 0; $i -lt 40; $i++) { Start-Sleep 5; $s = V "state"; if ($s -match "player=True") { return $true } }
    return $false
}
function Stop-Game { try { V "quit" | Out-Null } catch {}; Start-Sleep 4; Stop-Process -Name valheim -Force -ErrorAction SilentlyContinue }
function FastConfig {
    foreach ($kv in "CountdownMinutes 0.1","SpawnRadius 30","WaveTimeLimitMinutes 3","PlayerHuntRange 15","DeleteWorldOnLoss false","BuildDeadlineMinutes 0","RestMinMinutes 0.5","RestMaxMinutes 0.6") { V "mod cfg $kv" | Out-Null }
}
# Position of the nearest ward piece, from the harness "near" listing.
function WardPos { $l = (V "near 30") | Where-Object { $_ -match "^BaseDefenseWard" } | Select-Object -First 1; if ($l) { ($l -split "`t")[2] -split " " } }
function GoToWard { $w = WardPos; if ($w) { V "tp $([float]$w[0] + 1.5) $($w[1]) $([float]$w[2] + 1.5)" | Out-Null; Start-Sleep 3 } }

# ---- build ----
Stop-Game
foreach ($p in "mods\base-defense-ward\BaseDefenseWard.csproj","skills/valheim-dev-harness/DevHarness/DevHarness.csproj") {
    $b = dotnet build (Join-Path $root $p) -c Release 2>&1 | Select-String "error CS|Build succeeded"
    if ($b -match "error CS") { $b; Write-Host "FAIL build $p"; exit 1 }
}

# ---- phase A: registration, glow, countdown, wave, AI, win, rewards, progression ----
Write-Host "`n== Phase A: win path"
Check "boots into world" (Boot)
$state = V "state BaseDefenseWard"
Check "ward prefab registered" ($state -match "prefab BaseDefenseWard=True")
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
Check "mobs attack the ward (hp < 100% or mobs adjacent)" ((($st -join " ") -match "hp=(\d+)%" -and [int]$Matches[1] -lt 100) -or (($mobs | Where-Object { $_ -match "\t[0-4]m " }).Count -gt 0))
if ($atWard.Count -eq 0 -or @($atWard -match "hunt=True").Count -ne 0) { $mobs; $st }
V "tp $($pos[0]) $($pos[1]) $($pos[2])" | Out-Null; Start-Sleep 5
$mobs = V "mod mobs"
Check "mobs switch to hunting the exposed player who returns" (($mobs -match "hunt=True").Count -gt 0)
V "screenshot $out\wave.png" | Out-Null
for ($i = 0; $i -lt 6; $i++) { V "mod kill" | Out-Null; Start-Sleep 3; if ((V "mod status") -match "state=Resting") { break } }
$st = V "mod status"
Check "challenge won when wave is dead (ward rests)" ($st -match "state=Resting")
Check "rest span drawn from RestMin..RestMax" (($st -join " ") -match "rest=\d+/(\d+)s" -and [int]$Matches[1] -ge 30 -and [int]$Matches[1] -le 36)
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
GoToWard                                            # pause and interact need the player within reach (5 m)
V "mod cfg CountdownMinutes 1" | Out-Null           # hold the next countdown open long enough to test start-now
$hud = V "mod hud"
Check "HUD visible and shows the resting ward" ($hud -match "visible=True" -and $hud -match "Your ward: defended, next in \d\d:\d\d")
Check "resting ward keeps its map pin" ((V "mod pins") -match "count=1")
# Pause: the hotkey path (same code) while resting; timers must stand still.
V "mod cfg PausableWards true" | Out-Null
Check "pause toggles through the hotkey path" ((V "mod pause") -match "^toggled")
Start-Sleep 3
$a = (V "mod status") -join " "; Start-Sleep 4; $b = (V "mod status") -join " "
$ra = if ($a -match "rest=(\d+)/") { $Matches[1] } else { "a" }; $rb = if ($b -match "rest=(\d+)/") { $Matches[1] } else { "b" }
Check "paused ward's rest timer does not advance" (($a -match "paused=True") -and ($ra -eq $rb))
Check "HUD shows the pause" ((V "mod hud") -match "\(paused\)")
Check "hover text offers resume" ((V "mod interact") -match "Resume the ward")
V "mod pause" | Out-Null; Start-Sleep 2
Check "resumed ward continues" ((V "mod status") -match "paused=False")
V "mod cfg PausableWards false" | Out-Null
Check "pause refused when PausableWards is off" ((V "mod pause") -match "disabled")
for ($i = 0; $i -lt 16; $i++) { Start-Sleep 3; if ((V "mod status") -match "state=Countdown") { break } }
Check "ward re-arms after the rest" ((V "mod status") -match "state=Countdown")
# Start now: E on the ward during the countdown starts the wave (StartNow, default on); the vanilla toggle is skipped.
Check "hover text offers starting the wave" ((V "mod hover") -match "Start the wave now")
$ia = V "mod interact"
Start-Sleep 3
Check "interacting during the countdown starts the wave" (($ia -match "result=True") -and ((V "mod status") -match "state=Active"))
for ($i = 0; $i -lt 6; $i++) { V "mod kill" | Out-Null; Start-Sleep 3; if ((V "mod status") -match "state=Resting") { break } }
Check "early wave won" ((V "mod status") -match "state=Resting")
V "mod cfg StartNow false" | Out-Null
for ($i = 0; $i -lt 16; $i++) { Start-Sleep 3; if ((V "mod status") -match "state=Countdown") { break } }
Check "interact with StartNow off does not start the wave" (((V "mod interact") -match "result=") -and ((V "mod status") -match "state=Countdown"))
V "mod cfg StartNow true" | Out-Null
Check "rebuilding through the hammer path succeeds" ((V "build BaseDefenseWard 4 5") -match "^built")
Start-Sleep 3
Check "previous ward removed (only one per player)" ((V "mod wards") -match "^count=1 removed=1")
Check "HUD shows countdown to next wave" ((V "mod hud") -match "Your ward: wave in \d\d:\d\d")
Check "running ward has a map pin" ((V "mod pins") -match "count=1")
V "mod skip 60" | Out-Null; Start-Sleep 4
Check "moved ward's wave starts" ((V "mod status") -match "state=Active")
Check "HUD shows wave in progress" ((V "mod hud") -match "Your ward: hold \d\d:\d\d")
Check "placement refused while wave in progress" (((V "tryplace BaseDefenseWard") -match "result=False") -and ((V "mod wards") -match "blocked=1"))
Check "still exactly one ward" ((V "mod wards") -match "^count=1 ")
V "screenshot $out\hud_wave.png" | Out-Null
for ($i = 0; $i -lt 6; $i++) { V "mod kill" | Out-Null; Start-Sleep 3; if ((V "mod status") -match "state=Resting") { break } }
Check "moved ward's wave won" ((V "mod status") -match "state=Resting")
Start-Sleep 3
$board = V "mod board"
Check "board lists the ward with its builder and a running rest timer" ($board -match "name=devtest state=Resting paused=False running=True")

# ---- phase A3: another player's ward: stacked HUD rows, map pin, no collision with vanilla HUD ----
Write-Host "`n== Phase A3: multiplayer HUD rows + map pins"
Check "placed a second ward" ((V "place BaseDefenseWard 6 4") -match "zdo=True")
Start-Sleep 2
Check "reassigned it to another player" ((V "mod setcreator 424242") -match "creator=424242")
Start-Sleep 3
$hud = V "mod hud"
Check "HUD shows two rows (mine + theirs)" ($hud -match "rows=2" -and $hud -match "Your ward: (defended|wave in)" -and $hud -match "Viking: wave in \d\d:\d\d")
$tops = @(((($hud -split "tops=")[1] -split " ")[0]) -split ",")
Check "rows are stacked, not overlapping" ($tops.Count -eq 2 -and ([math]::Abs([float]$tops[0] - [float]$tops[1]) -ge 36))
$pins = V "mod pins"
Check "both wards have map pins, the other one named after its builder" (($pins -match "count=2") -and ($pins -match "Viking's ward") -and ($pins -match "devtest's ward"))
Check "offline builder's countdown is not running" ((V "mod board") -match "creator=424242 name=Viking state=Countdown paused=False running=False")
$rects = V "mod hudrects"
$hits = @($rects | Where-Object { $_ -match "^(minimap|statusEffects|eventBar|messageTopLeft|messageCenter|healthPanel|guardianPower|buildHud|saveIcon|betaText)`tactive=True" -and $_ -match "overlapsRows=Row" })
Check "HUD rows do not overlap vanilla HUD elements" ($hits.Count -eq 0); if ($hits) { $rects }
V "screenshot $out\hud_rows.png" | Out-Null
V "destroy basedefense" | Out-Null; Start-Sleep 5   # the "other player's" ward, nearest to the player
Check "destroyed ward's pin and row disappear" (((V "mod pins") -match "count=1") -and ((V "mod hud") -match "rows=1"))

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
