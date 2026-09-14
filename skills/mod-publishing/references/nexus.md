# Nexus Mods

Valheim section: https://www.nexusmods.com/valheim

## What Nexus needs
- An account (email registration). Uploads happen through the web form only; there is no public upload API.
- A mod page: name, category (for a build piece with gameplay: **Gameplay** or **Buildings**), a one-line
  summary (max 250 chars), a description (BBCode or the rich editor; the mod README converted), at least
  one image (screenshots from the real game; the first image becomes the thumbnail, 16:9 works best),
  language, author, permissions choices (open source / credits), and whether the mod requires other mods
  (add BepInExPack Valheim as a requirement).
- A file: the same Thunderstore-format zip. Give it a name and a version matching the manifest, and a short
  description of what is in it. Nexus users install by unpacking `plugins\` into `BepInEx\plugins\`, so say that
  in the installation section.

## Workflow
1. https://www.nexusmods.com/valheim/mods/add
2. Fill the page fields, save as draft, upload the file under **Files**, then **Publish**.
3. Copy the mod page URL into the repo README and, if used, `CHANGELOG.md`.

## Keep in sync
- Every release: upload the new zip as a new main file, mark the old one as "old version", paste the changelog
  entry into the mod's changelog tab.
- Version numbers on Nexus are free text; use the semver from `manifest.json`.
