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
