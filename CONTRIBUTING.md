# Contributing

Thanks for helping. This repo holds Valheim mods and the tooling that tests them against the real game.

## Reporting a bug
Open an issue with: the mod version, whether you play single-player, as a client, or on a dedicated server,
what you did, what happened, and `BepInEx\LogOutput.log` from the profile you played with (r2modman:
Settings > Browse profile folder). Screenshots help.

## Suggesting a feature
Open an issue and describe the situation in the game first, the change second. Gameplay ideas are judged on
whether they keep the challenge readable for a player who has not read the source.

## Making a change
1. Read `AGENTS.md` and the relevant `skills/*/SKILL.md`. They are short and they are the rules.
2. Build with `dotnet build mods\<mod>\<Mod>.csproj -c Release`. Everything deploys only into the isolated
   `dev` r2modman profile (`skills\valheim-dev-harness\scripts\setup-profile.ps1` creates it).
3. Before patching game code, decompile it (`skills\valheim-dev-harness\scripts\decompile.ps1`) and read the
   real method you are touching. Field names change between game versions.
4. Every gameplay change gets a line in `mods/<mod>/TESTING.md` and a check in `mods/<mod>/tests/e2e.ps1`.
   Run the script; it must print `ALL PASSED`.
5. Update the mod's `README.md` (options table, behaviour) and add a line to `CHANGELOG.md` under an
   "Unreleased" heading. Do not bump versions in a feature PR; releases are cut separately.
6. Keep dev-only code out of `mods/`. Anything reusable across mods belongs in `skills/`.

## Pull requests
- One topic per PR, small diffs, plain commit messages that say what changed and why.
- Include the tail of a passing e2e run in the PR description.
- No generated files, no `.decomp`, no screenshots except under `docs/`.

## Code style
- C# as in the existing files: small static classes per feature, Harmony patches next to the code they
  serve, `Cfg` for every tunable, no magic numbers in gameplay code.
- PowerShell scripts take parameters with sensible defaults and never touch anything outside the
  profile they are given.

## Releases (maintainers)
Follow `skills/mod-publishing/SKILL.md`: bump the three version fields, run the e2e test, package, verify
the zip in a clean profile, upload to Thunderstore and Nexus, tag `v<version>`, attach the zip to a GitHub release.
