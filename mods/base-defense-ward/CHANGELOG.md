# Changelog

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
