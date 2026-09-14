# Changelog

## 0.1.1
- After a defended wave the ward rests for a random span (RestMinMinutes..RestMaxMinutes, default 30-120 min)
  and then re-arms; waves keep growing with every completed challenge.
- Countdown and rest keep running while the builder is logged in, however far away the ward is, and pause while
  the builder is offline (the server advances unloaded wards). The hold timer still needs the wave simulated.
- StartNow (default on): E on the ward during the countdown starts the wave immediately.
- PausableWards (default off) + PauseKey (P): pause or resume a ward's countdown/rest within interact range.
- Hover text on the ward shows its timer, the start-now hint and the pause hint.
- HUD lists every running ward as its own row (yours first, then other players' wards by builder name), stacked
  under the minimap so rows never overlap each other or the vanilla status-effect icons. Works at any distance.
- Every running ward gets a minimap pin named after its builder; the pin goes away when the ward is gone.

## 0.1.0
First public release.

- Base Defense Ward build piece (vanilla ward model and cost, red glow while active).
- Countdown on build, then a wave spawns around the ward and attacks it; exposed players nearby get hunted,
  sheltered players are ignored.
- Win on wave wipe or by holding out; tiered rewards scaled by completed challenges.
- Mob pool tiers unlocked by boss trophies hung on the sacrificial stones.
- One ward per player; rebuild to move it, locked while a wave is in progress.
- HUD panel left of the minimap with the current countdown.
- Countdown and hold timers only advance while the ward is simulated: they pause while everyone is offline or
  away from the ward, on servers too.
- Options: timers, wave size, rewards, hunt range, boss keys, custom mob pools, and the opt-in
  DeleteWorldOnLoss and BuildDeadlineMinutes consequences (both off by default).
