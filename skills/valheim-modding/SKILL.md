---
name: valheim-modding
description: Patterns and pitfalls for writing Valheim BepInEx/Harmony mods in C# (custom build pieces cloned from vanilla prefabs, ZDO state, global keys, HUD elements, multiplayer ownership). Use when adding or changing gameplay code in any mod under mods/.
---

# Valheim modding

## Project shape
- One publishable mod per `mods/<name>/` folder: `<Mod>.csproj`, `src/`, `manifest.json`, `README.md`,
  `CHANGELOG.md`, `icon.png` (256x256), `TESTING.md`, `tests/e2e.ps1`. Nothing dev-only goes in here.
- `Directory.Build.props` at the repo root supplies the game and BepInEx references (netstandard2.1, no
  copy-local) and deploys every build into the isolated dev profile. A new mod's csproj only needs
  `AssemblyName`, `RootNamespace` and `Version`.
- Plugin class: `[BepInPlugin(GUID, NAME, VERSION)]`, bind config in `Awake`, then
  `new Harmony(GUID).PatchAll(Assembly.GetExecutingAssembly())`. Keep `VERSION` equal to
  `manifest.json` `version_number` and the csproj `Version`.
- Expose `public static string DebugCommand(string line)` on the plugin so the dev harness can drive it.

## Read the real API first
Field and method names change between game versions. Run `skills\valheim-dev-harness\scripts\decompile.ps1`
and grep `.decomp\` before writing a patch. Don't rely on memory of older versions.

## Custom build pieces (clone of a vanilla prefab)
1. Clone from `ZNetScene.instance.GetPrefab("<vanilla>")` under an **inactive** holder `GameObject`
   (`DontDestroyOnLoad`), rename it, edit `Piece` fields, add components.
2. Register: `ZNetScene.m_prefabs.Add(clone)` and the private `m_namedPrefabs[name.GetStableHashCode()]`.
3. Add to the Hammer table: `ObjectDB.GetItemPrefab("Hammer").GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces.m_pieces`.
4. Call the registration from postfixes on `ZNetScene.Awake`, `ObjectDB.Awake` and `ObjectDB.CopyOtherDB`;
   make it idempotent and skip while `ObjectDB.m_items` is empty (main menu).
5. Recolour with `renderer.materials` (instances), never `sharedMaterials`, or the vanilla piece changes too.
6. A `StaticTarget` component (`m_primaryTarget`, `m_randomTarget`) makes monsters path to and attack a piece.
7. The clone inherits build cost and crafting-station requirement from the source piece; override
   `Piece.m_resources` only if a different cost is wanted.

## State, multiplayer and ownership
- Persist per-object state in the ZDO (`nview.GetZDO().Set(hash, value)`), never in C# fields alone; ZDOs
  survive relogs and sync to other clients.
- Run gameplay logic only on the ZDO owner (`nview.IsOwner()`); in multiplayer that is usually the server or
  the closest player. Objects with no nearby player are unowned and pause.
- World-wide flags go in global keys: `ZoneSystem.instance.SetGlobalKey(...)`. Key-value keys are stored as
  `"name value"`; remove before re-setting to update a value.
- Per-player ownership of pieces: `ZDOVars.s_creator` holds the builder's player id.
  `ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, list, ref index)` enumerates loaded and unloaded ZDOs.
- To destroy an unloaded ZDO: `z.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(z)`.

## Assorted API facts (verified on the Unity 6 build, 2026)
- `PlatformManager` lives in namespace `Splatform`.
- `PrivateArea.IsEnabled/SetEnabled` are private; use `AccessTools`. Vanilla ward prefab is `guard_stone`, its
  in-game name is "Ward".
- `MonsterAI.m_targetStatic` / `m_targetCreature` are private; set them through `AccessTools` to point an AI
  at something it cannot see. `SetHuntPlayer(bool)` and `Alert()` are public.
- `Player.InShelter()` is true when roofed and walled in.
- Boss stones have an empty `m_setsWorldKey`; mirror `BossStone.SetActivated` into your own global keys if
  you need trophy state.
- `ItemDrop.DropItem` needs `m_itemData.m_dropPrefab` set on never-instantiated prefabs.
- `Game.Logout(save:false)` still saves. For a no-save exit call the private `Game.Shutdown(false)`, then
  `SystemResourceManager.FastLoadScene(Game.instance.m_startScene)` (or `Application.Quit()` on a dedicated server).
- `World.RemoveWorld(name, source)` + `SaveSystem.InvalidateCache(SaveDataType.World)` deletes a world.
- HUD: parent custom UI next to `Minimap.instance.m_smallRoot`; keep the updating `MonoBehaviour` on an
  always-active holder, because `SetActive(false)` on the same object stops its `Update`.
- `MessageHud.instance.MessageAll(MessageHud.MessageType.Center, text)` broadcasts to every player.
- `ZNet.instance.GetTimeSeconds()` is the world clock; use it for timers that must survive relogs.
