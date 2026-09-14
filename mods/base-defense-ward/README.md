# Base Defense Ward

A tower-defence challenge for your base. Build the **Base Defense Ward** (Hammer, Misc tab; same model as the
vanilla ward, glows red while active) and a countdown starts. When it ends, a wave spawns around your base and
comes for the ward. Hold it and you are rewarded; lose it and the challenge is lost.

## How it plays

- **Countdown** starts the moment the ward is built (default 10 minutes). A panel left of the minimap shows the
  time to the next wave, the time left to hold during a wave, or the build deadline if one is configured.
- **The wave** spawns at a distance from the ward (default 55 m, outside the ward's 32 m radius) and is scaled by
  how many challenges this world has already completed.
- **Not stupid**: mobs hunt any player within range who is *exposed*. A player who is roofed and walled in is
  ignored and the mobs go for the ward and the structures in their way instead.
- **Win** when every wave mob is dead, or when the ward still stands at the end of the time limit (default 5 min).
  Rewards drop at the ward: coins plus materials matching your progression, scaled by completed challenges.
- **Lose** if the ward is destroyed during a wave.
- **One ward per player.** Build a new one to move it: the previous one disappears and the countdown restarts.
  You cannot rebuild while your ward has a wave in progress. In multiplayer each player can have one.

## Progression

The mob pool is picked from tiers unlocked by **boss trophies hung on the sacrificial stones**. A fresh world
only sees Meadows creatures; hang Eikthyr's trophy and Black Forest creatures join, and so on through Ashlands.
Later tiers get heavier as you complete more challenges, but a tier you have not unlocked never appears.
Rewards follow the same tiers.

## Options (`BepInEx/config/com.night.basedefenseward.cfg`)

| Key | Default | Meaning |
| --- | --- | --- |
| CountdownMinutes | 10 | Build to first wave |
| WaveTimeLimitMinutes | 5 | Hold this long and the wave counts as beaten |
| SpawnRadius | 55 | Spawn distance from the ward |
| BaseMobCount / MobsPerCompletedChallenge / MaxMobCount | 4 / 2 / 30 | Wave size |
| PlayerHuntRange | 40 | Exposed players within this range of a mob get hunted |
| RewardMultiplier | 1 | Scales reward amounts |
| DeleteWorldOnLoss | false | Lose the challenge and the world save is deleted; dedicated servers exit so a wrapper can restart |
| BuildDeadlineMinutes | 0 (off) | A ward must be built this many minutes after world creation or the challenge is lost |
| BossKeys | bdw_hung_* keys | Which global keys unlock each tier (use the defeated_* keys to count kills instead) |
| ExtraMobPools | empty | Override the built-in tier pools |

Requires BepInEx 5.4.23+ (Valheim runs on Unity 6). Install on every client; the host/server runs the wave logic.

Source: https://github.com/GustavHenning/base-defense-ward
