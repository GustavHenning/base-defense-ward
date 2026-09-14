# Thunderstore

Package format: https://wiki.thunderstore.io/mods/creating-a-package

## Package rules (validated by `scripts/package.ps1`)
- Zip root contains `manifest.json`, `README.md`, `icon.png` (exactly 256x256 PNG); `CHANGELOG.md` optional but shown.
- `manifest.json`: `name` `[A-Za-z0-9_]`, max 128 (underscores render as spaces); `version_number` semver
  `Major.Minor.Patch`; `description` max 250 chars; `website_url` URL or `""`; `dependencies` as
  `Team-Package-Version`, e.g. `denikson-BepInExPack_Valheim-5.4.2202`.
- Max package size 5 GB. README is Markdown (UTF-8); images need absolute URLs.
- The full package name becomes `<Team>-<name>`. Name and team are immutable after the first upload; a version
  number can never be reused, so bump before every re-upload.

## Manual upload (web)
1. Log in at https://thunderstore.io (GitHub, Discord or Overwolf login).
2. Create a team: Settings > Teams > Create team. The team name is the author prefix of every package.
3. https://thunderstore.io/package/create/ : choose team, tick the **Valheim** community, pick categories
   (for a gameplay build-piece mod: Gameplay, Building, Enemies as applicable; "Server-side" / "Client-side"
   tags describe where it must be installed), NSFW off, upload the zip, Submit.
4. The package is live immediately at `https://thunderstore.io/c/valheim/p/<Team>/<Name>/`.

## CLI upload
```
dotnet tool install -g tcli
tcli publish --config-path thunderstore.toml --token <API token>
```
Create the token under the team's settings (Service accounts). A `thunderstore.toml` describing the package
lives next to the mod (`mods/<mod>/thunderstore.toml`) so this can run from CI later.

## After upload
- Download the zip from the package page and diff it against `dist\` to make sure the right file is up.
- Install it via r2modman (search the mod, Download) or `install-to-profile.ps1` and launch once.
- Keep `website_url` pointing at the public GitHub repo; add the Thunderstore link to the repo README.
