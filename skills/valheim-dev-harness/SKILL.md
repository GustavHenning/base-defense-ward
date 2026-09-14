---
name: valheim-dev-harness
description: Build, launch and drive Valheim with a BepInEx mod in an isolated r2modman profile, scripted over a TCP debug socket. Use when a Valheim mod needs to be run, inspected or tested in the real game without touching the developer's own profiles, characters or worlds.
---

# Valheim dev harness

A dev-only BepInEx plugin (`DevHarness/`) plus PowerShell scripts (`scripts/`) that make the real game
scriptable. Nothing here is ever shipped with a mod.

## Isolation rules (never break these)
- Everything deploys into the `dev` r2modman profile only: `%APPDATA%\r2modmanPlus-local\Valheim\profiles\dev`.
  `Directory.Build.props` at the repo root copies every built DLL there and nowhere else.
- The harness plays character `devtest` in world `DevTest`, both **local** saves it creates itself.
- Launch with `scripts\run-dev.ps1`, never through r2modman or Steam's play button. Steam must be running.
- Never edit the game folder. Never write to other profiles or the developer's saves.
- Override paths with env vars `VALHEIM_DIR` (game folder) and `VALHEIM_DEV_PROFILE` (profile folder).

## Workflow
1. Once per machine: `scripts\setup-profile.ps1` (copies just the BepInEx core from an existing profile).
2. Before patching anything, read the real API: `scripts\decompile.ps1` writes `.decomp\` at the repo root
   (git-ignored, needs `dotnet tool install -g ilspycmd --version 9.1.0.7988`). Field names change between
   game versions; grep there, don't guess.
3. Build: `dotnet build mods\<mod>\<Mod>.csproj -c Release` and `dotnet build skills\valheim-dev-harness\DevHarness\DevHarness.csproj -c Release`.
4. Launch: `scripts\run-dev.ps1` (add `-NoAutoStart` to stay in the main menu). Wait until
   `scripts\vh.ps1 "state"` reports `player=True` (about 55 s).
5. Drive: `scripts\vh.ps1 "<command>"` sends one line to the socket and prints the reply.
   Full command list: [references/commands.md](references/commands.md).
6. Log: `<profile>\BepInEx\LogOutput.log`. Harmony and plugin errors appear there; check it after every run.
   It is flushed lazily, so assert on state read over the socket rather than on log lines where possible.
7. Screenshots: `vh.ps1 "screenshot <abs path>"` writes a PNG of what the game shows; Read it to look.
   For clean shots: `hud off`, then `freecam x y z lookX lookY lookZ` (no player model in frame) or `cam 0`.

## Hooking a mod into the harness
- Give the mod's plugin class a `public static string DebugCommand(string line)` that returns a text report.
  `vh.ps1 "mod <line>"` forwards to it (with several such mods loaded: `mod <TypeName> <line>`).
  Use it for state dumps, fast-forwarding timers and runtime config changes (`cfg <key> <value>`), so tests
  never edit the shipped defaults.
- `state <prefab...>` reports whether custom prefabs are registered in `ZNetScene`.
- Write the mod's end-to-end test as `mods/<mod>/tests/e2e.ps1` (see the `valheim-mod-testing` skill).

## Gotchas
- Valheim is Unity 6 with Doorstop 4.x in the game folder: BepInEx core must be 5.4.23+, launch flags are
  `--doorstop-enabled true --doorstop-target-assembly <path>`. Older cores fail silently (no log at all).
- Test character `devtest` never gets the valkyrie intro (the harness clears `m_firstSpawn`).
- Simulation only runs for ZDOs a client owns. Objects far from every player pause; keep the player within
  ~100 m of what the test observes.
- `Remove-Item` on directories prompts in non-interactive PowerShell; pass `-Recurse -Force`.
