---
name: valheim-mod-testing
description: How to write and run end-to-end tests for a Valheim mod against the real game using the dev harness, and how to keep a per-mod TESTING.md checklist. Use when a mod's behaviour needs to be verified, a test added, or a release checked before publishing.
---

# Testing Valheim mods end to end

Unit tests cannot load the game, so every mod is tested by driving the real client through the
`valheim-dev-harness` skill. Each mod owns two files:

- `mods/<mod>/TESTING.md` – the checklist: what must be true for a release, in plain language, one line per
  behaviour. It is the contract; the script implements it. Template: [references/testing-template.md](references/testing-template.md).
- `mods/<mod>/tests/e2e.ps1` – the script. `exit 0` means every check passed. Run it before every release and
  after every gameplay change.

## Writing the script
1. Copy the skeleton in [references/e2e-skeleton.ps1](references/e2e-skeleton.ps1). It builds the mod and the
   harness, boots into a fresh throwaway `DevTest` world, and gives you `V "<cmd>"`, `Check "<name>" <bool>`,
   `Log "<pattern>"`, `Boot` and `Stop-Game`.
2. Speed the game up through the mod's own `DebugCommand` (`mod cfg <key> <value>`, `mod skip <seconds>`),
   never by changing shipped defaults. Delete the mod's config file in `Boot` so stale values cannot leak in.
3. Assert on state read over the socket (`mod status`, `near`, `items`, `keys`, ...). Only use the BepInEx log
   for things that have no other observable effect; it is flushed lazily.
4. Every branch of the feature gets a check: the happy path, the failure path, the multiplayer-ish path
   (ownership, per-player limits), and "no errors in log" at the end.
5. Take a screenshot at each visually interesting moment (`screenshot <abs path>`) into `.out\`; read them
   when a check fails and keep the good ones for the mod page (`docs/`).
6. Keep the player within ~100 m of what you observe, otherwise the ZDOs go unowned and simulation pauses.
7. The whole run should stay under 10 minutes. Shorten timers, don't wait for defaults.

## Release check
Run `mods\<mod>\tests\e2e.ps1`, then package with the `mod-publishing` skill and install the resulting zip
into a **clean** profile with `skills\mod-publishing\scripts\install-to-profile.ps1`, launch with that profile
and confirm the plugin loads and registers (log shows `<Mod> <version> loading` and no `[Error`). Only then upload.
