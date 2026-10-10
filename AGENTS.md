# AGENTS.md

Valheim BepInEx mods plus an isolated, scriptable test harness. This file is the map; the details live in
`skills/`. Read the skill for the job before starting it.

## Layout
- `mods/<name>/` – one publishable mod per folder (csproj, `src/`, `manifest.json`, `README.md`, `CHANGELOG.md`,
  `icon.png`, `TESTING.md`, `tests/e2e.ps1`). Nothing dev-only goes in here.
- `skills/` – shared, mod-independent instructions and tooling, one folder per skill with a `SKILL.md`:
  - `valheim-modding/` – how to write the C# (prefab cloning, ZDO state, ownership, verified API facts).
  - `valheim-dev-harness/` – dev-only plugin, launcher, debug socket, decompiler. Command reference in `references/`.
  - `valheim-mod-testing/` – how to write `TESTING.md` and `tests/e2e.ps1` for a mod; skeleton and template.
  - `mod-publishing/` – `package.ps1`, `install-to-profile.ps1`, Thunderstore and Nexus how-tos.
- `docs/` – screenshots for READMEs and mod pages. `Directory.Build.props` – shared build settings.
- `.decomp/`, `.out/`, `dist/` – generated, git-ignored.

## Non-negotiables
- Testing is isolated: only the `dev` r2modman profile and the `devtest` character / `DevTest` world are ever
  touched. Never the game folder, never other profiles, never the developer's saves.
- Read the decompiled API before patching. Field names change between game versions.
- Every gameplay change gets a `TESTING.md` line and an e2e check. `mods\<mod>\tests\e2e.ps1` must print `ALL PASSED`.
- Timers and options are shortened for tests through the mod's `DebugCommand` (`mod cfg ...`), never by
  editing shipped defaults.
- Commits are authored by the human developer only. Do not add AI co-author trailers or attribution lines.
  Never commit secrets, tokens, local paths outside the defaults, or anything from `.out/`, `dist/`, `.decomp/`.

## Quick commands
```
skills\valheim-dev-harness\scripts\setup-profile.ps1                    # once per machine
skills\valheim-dev-harness\scripts\decompile.ps1                        # .decomp\ (needs ilspycmd)
dotnet build mods\base-defense-ward\BaseDefenseWard.csproj -c Release
skills\valheim-dev-harness\scripts\run-dev.ps1                          # launch into DevTest
skills\valheim-dev-harness\scripts\vh.ps1 "state BaseDefenseWard"       # drive the game
mods\base-defense-ward\tests\e2e.ps1                                    # full test, ~7 min
skills\mod-publishing\scripts\package.ps1 -Mod mods\base-defense-ward   # dist\*.zip
```
