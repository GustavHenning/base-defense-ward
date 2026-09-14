---
name: mod-publishing
description: Package a Valheim BepInEx mod from mods/<name> into a Thunderstore-format zip, verify it installs into a clean r2modman profile, and publish it to Thunderstore and Nexus Mods. Use when cutting a release, bumping a version, or uploading a mod.
---

# Packaging and publishing a mod

## 1. Bump the version in three places
`mods/<mod>/manifest.json` `version_number`, the csproj `<Version>`, and the plugin's `VERSION` constant.
`scripts\package.ps1` refuses to package if they disagree. Add the release to `CHANGELOG.md`.

## 2. Package
```
skills\mod-publishing\scripts\package.ps1 -Mod mods\<mod>
```
Validates manifest rules (name `[A-Za-z0-9_]`, semver, description <= 250 chars, 256x256 icon), builds
Release, and writes `dist\<Name>-<version>.zip` with this layout:
```
manifest.json  README.md  CHANGELOG.md  icon.png  plugins\<Mod>.dll
```
That is the layout Thunderstore and r2modman expect (`plugins\` is flattened into
`BepInEx\plugins\<Team>-<Name>\` on install). Manual users unpack the same zip.

## 3. Verify the zip like a user would
```
skills\mod-publishing\scripts\install-to-profile.ps1 -Zip dist\<Name>-<version>.zip -ProfileName release-test -Clean -Launch
```
Installs into a fresh profile and launches the game with that profile (no dev tooling), waits for the
plugin to log that it loaded, and fails on any `[Error`. For an in-world check, also build the dev harness
into that profile (`VALHEIM_DEV_PROFILE=<profile> dotnet build skills\valheim-dev-harness\DevHarness\DevHarness.csproj`)
and run the mod's e2e script with `VALHEIM_DEV_PROFILE` set; delete the harness DLL again afterwards.
r2modman's "Import local mod" accepts the same zip.

## 4. Publish
- Thunderstore: [references/thunderstore.md](references/thunderstore.md). Manual upload via the web form, or
  `tcli` with an API token. Package names are immutable and versions can never be re-uploaded.
- Nexus Mods: [references/nexus.md](references/nexus.md). Manual upload; needs a mod page (name, category,
  summary, description, images) plus the file.
- Tag the commit `v<version>` and publish a GitHub release with the same zip so the `website_url` in the
  manifest leads somewhere useful.

## Rules
- Never put dev-only DLLs (DevHarness) in a package. Never package from a dirty working tree.
- Screenshots on the mod page must come from the real game; take them with the dev harness
  (`hud off`, `freecam ...`, `screenshot ...`).
- README links to images must be absolute URLs (Thunderstore does not host README images).
- Keep the Thunderstore `README.md` identical to `mods/<mod>/README.md`; it is the same file.
