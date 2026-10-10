# Testing: Base Defense Ward

What must be true before a release. `tests\e2e.ps1` implements every line against the real game and must exit 0
(about 7 minutes; it needs Steam running and the `dev` profile from `skills\valheim-dev-harness\scripts\setup-profile.ps1`).

## Registration and visuals
- [ ] Plugin loads; `BaseDefenseWard` prefab is registered and listed in the Hammer table.
- [ ] Activating the ward turns on its effect object and the glow light has the mod's red-pink colour; the
      vanilla ward is unaffected.
- [ ] Building the ward sets the `bdw_built` global key and starts the countdown.

## Wave
- [ ] After the countdown the state becomes Active and mobs spawn on the spawn ring.
- [ ] Timers are accumulated simulated time (`bdw_elapsed`, `bdw_waveelapsed` ZDO floats); `mod skip` advances them
      and the HUD counts down from them, so they pause while the ward is unowned or the game is closed.
- [ ] With no exposed player in hunt range, mobs at the ward do not hunt and they damage the ward (or stand at it).
- [ ] When an exposed player comes back into range, mobs switch to hunting.
- [ ] Killing every mob wins the challenge, increments the completed counter and drops coins plus tier materials at the ward.

## Progression
- [ ] Hanging a trophy on a boss stone sets `bdw_hung_<boss>`; removing it clears the key.
- [ ] Boss tier follows the keys; tier-0 samples never contain Black Forest mobs; tier-1 samples and rewards do.

## One ward per player and HUD
- [ ] HUD is visible and shows "Ward defended" after a win, "Wave in mm:ss" during a countdown,
      "Wave! hold mm:ss" during a wave, "Build ward in mm:ss" with a build deadline.
- [ ] Building a second ward through the Hammer path removes the first (exactly one ward per player).
- [ ] Placement is refused while that player's wave is active.

## Consequences (opt-in options)
- [ ] With `DeleteWorldOnLoss`, destroying the ward mid-wave logs the loss, schedules the reset, returns to the
      start menu and deletes the world files, with no NullReferenceException.
- [ ] With `BuildDeadlineMinutes`, missing the deadline sets `bdw_deadline_failed` and logs it.
- [ ] Both options default to off.

## Release
- [ ] No `[Error` lines in the BepInEx log for the whole run.
- [ ] `skills\mod-publishing\scripts\package.ps1` produces the zip and
      `install-to-profile.ps1 -Clean -Launch` loads it in a clean profile.

## Rest, pause, start now, distance (0.1.1)
- [ ] A defended ward enters Resting with a rest span inside RestMin..RestMax, then re-arms into Countdown.
- [ ] With PausableWards, the hotkey path pauses the nearest ward in reach: timers stand still, HUD and hover
      text say "paused", resume works; refused when the option is off or a wave is active.
- [ ] With StartNow, E on the ward during the countdown starts the wave (hover text says so); with it off,
      E keeps the vanilla toggle and the countdown continues.
- [ ] The server's ward board lists every ward with builder name, state, paused and running flags; a ward whose
      builder is offline has running=False; HUD rows and pins are built from the board (any distance).
- [ ] A destroyed ward's pin and HUD row disappear.
