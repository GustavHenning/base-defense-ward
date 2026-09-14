# Base Defense Ward

Valheim mods by Gustav Henning, plus the tooling used to build and test them against the real game.

| Mod | What it does | Get it |
| --- | --- | --- |
| [Base Defense Ward](mods/base-defense-ward/) | Build a ward, survive the wave that comes for it, earn tiered rewards. Mobs hunt exposed players and ignore sheltered ones. | Thunderstore: `GustavHenning-BaseDefenseWard` · Nexus Mods: *Base Defense Ward* · [Releases](https://github.com/GustavHenning/base-defense-ward/releases) |

![The Base Defense Ward](docs/ward.jpg)

## Repository layout

```
mods/<name>/            one publishable mod: csproj, src/, manifest.json, README.md, CHANGELOG.md, icon.png,
                        TESTING.md (release checklist) and tests/e2e.ps1 (runs it against the real game)
skills/                 shared, mod-independent know-how and tooling
  valheim-modding/        API patterns and pitfalls for BepInEx/Harmony code
  valheim-dev-harness/    dev-only plugin + scripts: isolated profile, launcher, TCP debug socket, decompiler
  valheim-mod-testing/    how to write an e2e test and a TESTING.md; skeleton and template
  mod-publishing/         package.ps1, install-to-profile.ps1, Thunderstore and Nexus how-tos
docs/                   screenshots used by READMEs and mod pages
Directory.Build.props   shared build settings; every build deploys only into the isolated dev profile
```

Each `skills/*/SKILL.md` is written for both people and coding agents. If you use Claude Code, point it at
`skills/` (see `AGENTS.md`).

## Building

Requirements: Windows, Valheim installed through Steam, .NET SDK 8, and r2modman with *BepInExPack Valheim*
installed in at least one profile (the harness copies its BepInEx core from there).

```
skills\valheim-dev-harness\scripts\setup-profile.ps1     # once: creates the isolated "dev" profile
dotnet build mods\base-defense-ward\BaseDefenseWard.csproj -c Release
```

Paths default to the standard Steam location and `%APPDATA%\r2modmanPlus-local\Valheim\profiles\dev`;
override with the `VALHEIM_DIR` and `VALHEIM_DEV_PROFILE` environment variables.

## Testing

```
mods\base-defense-ward\tests\e2e.ps1
```

Builds the mod and the dev harness, launches the game into a throwaway world, drives every feature in
`mods/base-defense-ward/TESTING.md` over a local socket and exits 0 when all checks pass (about 7 minutes).
Your own profiles, characters and worlds are never touched.

## Releasing

```
skills\mod-publishing\scripts\package.ps1 -Mod mods\base-defense-ward
skills\mod-publishing\scripts\install-to-profile.ps1 -Zip dist\BaseDefenseWard-0.1.0.zip -Clean -Launch
```

Then upload `dist\BaseDefenseWard-<version>.zip` to Thunderstore and Nexus as described in
[skills/mod-publishing/SKILL.md](skills/mod-publishing/SKILL.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports and feature ideas go in
[GitHub issues](https://github.com/GustavHenning/base-defense-ward/issues); please attach
`BepInEx\LogOutput.log`.

## License

[MIT](LICENSE).
