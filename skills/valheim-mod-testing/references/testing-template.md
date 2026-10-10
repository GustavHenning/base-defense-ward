# Testing: <Mod name>

What must be true before a release. `tests/e2e.ps1` implements every line; run it and it must exit 0.

## Registration
- [ ] Plugin loads without errors (`<Mod> <version> loading` in the log, no `[Error`).
- [ ] Custom prefab(s) registered in `ZNetScene` and present in the Hammer piece table.

## Feature: <name>
- [ ] Happy path: ...
- [ ] Failure path: ...
- [ ] Persistence: state survives leaving and returning (ZDO / global key).
- [ ] Multiplayer: logic runs on the owner only; per-player rules hold.

## Config
- [ ] Every option in `README.md` exists in the config file with the documented default.
- [ ] Destructive options are off by default and do nothing when off.

## Visual
- [ ] Screenshot of <thing> looks right (`.out\<name>.png`).

## Release
- [ ] Packaged zip installs into a clean profile and loads (`install-to-profile.ps1`).
