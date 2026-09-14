# AGENTS.md

Valheim BepInEx mods. Everything here is designed so testing is **isolated**: it never touches the
developer's own r2modman profiles, the game folder's BepInEx, or their real characters/worlds.

## Layout
- `mods/<name>/` – one publishable mod per folder (csproj, `src/`, `manifest.json`, `README.md`, `icon.png`). Nothing dev-only goes in here.
- `tools/DevHarness/` – separate dev-only BepInEx plugin: auto-loads a throwaway character/world and serves a TCP debug socket. Never packaged.
- `tools/*.ps1` – setup, launch, command client, e2e test, packaging, decompile.
- `Directory.Build.props` – shared references and paths. Override with env vars `VALHEIM_DIR` and `VALHEIM_DEV_PROFILE`.

## Isolation rules
- All builds deploy only into the `dev` r2modman profile (`%APPDATA%\r2modmanPlus-local\Valheim\profiles\dev`). Create it with `tools\setup-profile.ps1`; it copies just the BepInEx core from an existing profile.
- The harness plays character `devtest` in world `DevTest`, both **local** saves it creates itself. Do not use the developer's saves.
- Launch with `tools\run-dev.ps1`, never through r2modman or Steam's play button. Steam must be running in the background.
- Do not edit the game folder. Do not write to other profiles.

## Workflow
1. `tools\setup-profile.ps1` once per machine.
2. Read the real API before patching: `tools\decompile.ps1` writes `.decomp\` (git-ignored). Field names change between game versions; grep there, don't guess.
3. Build: `dotnet build mods\base-defense-ward\BaseDefenseWard.csproj -c Release` (and the harness).
4. Test: `tools\test-e2e.ps1` builds both, launches, waits ~55 s, drives the game over the socket, screenshots to `tools\out\`, quits. Exit code 0 = pass.
5. Ad-hoc: `tools\vh.ps1 "state"`, `"pieces <filter>"`, `"place <prefab> 0 4"`, `"near 10"`, `"toggle"`, `"console <devcommand>"`, `"screenshot <path>"`, `"quit"`. Read `tools\out\*.png` to see what the game shows.
6. Log: `<profile>\BepInEx\LogOutput.log`. Harmony/plugin errors appear there; check it after every run.
7. Package: `tools\package.ps1` → `dist\`.

## Gotchas
- Game is Unity 6 with Doorstop 4.x in the game folder: BepInEx core must be 5.4.23+, launch flags are `--doorstop-enabled true --doorstop-target-assembly <path>`. Older cores fail silently (no log file at all).
- Custom prefabs: clone from `ZNetScene.instance.GetPrefab(...)` under an **inactive** holder object, rename, add to `ZNetScene.m_prefabs` and its `m_namedPrefabs` dict, then add to the Hammer `PieceTable`. Register from postfixes on `ZNetScene.Awake`, `ObjectDB.Awake` and `ObjectDB.CopyOtherDB`; make it idempotent and skip when `ObjectDB.m_items` is empty (main menu).
- Materials: use `renderer.materials` (instances) when recolouring a clone so the vanilla piece is unaffected.
- `PlatformManager` lives in namespace `Splatform`. `PrivateArea.IsEnabled/SetEnabled` are private; use Harmony `AccessTools`.
- Vanilla ward prefab is `guard_stone`; the in-game name is "Ward" (not totem).

## Contributions
Commits are authored by the human developer only. Do not add AI co-author trailers or attribution lines.
