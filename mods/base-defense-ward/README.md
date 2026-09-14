# Base Defense Ward

Tower defense for your base. Build the **Base Defense Ward** (Hammer, Misc tab; vanilla ward model and cost,
glows red) and a countdown starts. When it ends a wave spawns around your base and goes for the ward.
Hold it and rewards drop at the ward; lose it and the challenge is lost. The ward then rests and re-arms,
with bigger waves each time.

- Mobs hunt players who are out in the open and ignore players who are roofed and walled in.
- Mob tiers and rewards unlock with boss trophies hung on the sacrificial stones (Meadows only on a fresh world).
- One ward per player; build a new one to move it. Locked while a wave is in progress.
- Press E on the ward during the countdown to start the wave now. Optional pause hotkey.
- HUD rows under the minimap for every running ward (yours first, other players' by name) and a map pin on each.
- Countdown and rest run while the builder is logged in, at any distance. They pause while the builder is offline.
- Requires BepInEx 5.4.23+ on every client and on the server or host.

## Options (`BepInEx/config/com.night.basedefenseward.cfg`)

| Key | Default | Meaning |
| --- | --- | --- |
| CountdownMinutes | 10 | Build (or end of rest) to wave |
| WaveTimeLimitMinutes | 5 | Hold this long and the wave counts as beaten |
| RestMinMinutes / RestMaxMinutes | 30 / 120 | Random rest between a defended wave and the next countdown |
| SpawnRadius | 55 | Spawn distance from the ward (its protected radius is 32) |
| BaseMobCount / MobsPerCompletedChallenge / MaxMobCount | 4 / 2 / 30 | Wave size |
| SpawnIntervalSeconds | 1.5 | Delay between individual spawns |
| PlayerHuntRange | 40 | Exposed players within this range of a mob get hunted |
| StartNow | true | E on the ward during the countdown starts the wave immediately |
| PausableWards | false | Allow pausing a ward's countdown or rest with PauseKey within interact range (not during a wave) |
| PauseKey | P | The pause hotkey |
| RewardMultiplier | 1 | Scales reward amounts |
| DeleteWorldOnLoss | false | **Destructive.** Losing deletes the world save; dedicated servers exit so a wrapper can restart |
| BuildDeadlineMinutes | 0 (off) | A ward must be built this many minutes after world creation or the challenge is lost |
| BossKeys | bdw_hung_* | Global keys that unlock each tier, in order (use `defeated_*` keys to count kills instead) |
| ExtraMobPools | empty | Override the built-in tier pools: tiers separated by `;`, prefabs by `,` |

Source, issues, contributions: https://github.com/GustavHenning/base-defense-ward
