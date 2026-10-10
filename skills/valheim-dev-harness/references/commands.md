# Debug socket commands

`scripts\vh.ps1 "<line>"` sends one line to `127.0.0.1:52380`; the harness runs it on the Unity main thread and
replies with text terminated by `<<END>>`. Commands are case-insensitive; arguments are space separated.

## Session
| Command | Effect |
| --- | --- |
| `ping` | `pong` |
| `state [prefab...]` | Scene, ZNet/ObjectDB/ZNetScene presence, cheats flag, player position and HP; `prefab <name>=True/False` for each prefab named |
| `log <text>` | Writes `[remote] <text>` into the BepInEx log (handy as a marker between test phases) |
| `console <cmd>` | Runs a game console command (`devcommands` are already on; e.g. `console skiptime 0.5`, `console env Clear`) |
| `cheats [off]` | Toggle `Terminal.m_cheat` |
| `menu <FejdStartupMethod>` | Invoke a main-menu method by name (only in the start scene) |
| `quit` | `Application.Quit()` |

## Player
| Command | Effect |
| --- | --- |
| `pos` | Player position `x y z` |
| `tp <x> <y> <z>` | Teleport the player |
| `god [off]` | God mode |
| `hover` | What the player is looking at |
| `lookat <x> <y> <z>` | Turn the player toward a point |
| `keys` | Global keys of the world |

## Camera and screenshots
| Command | Effect |
| --- | --- |
| `cam <distance> [fov]` | Camera distance behind the player (0 = first person) and field of view |
| `freecam <x> <y> <z> <lookX> <lookY> <lookZ>` | Detach the camera at a point looking at another point; `freecam off` to return |
| `hud off` / `hud on` | Hide or show the whole HUD |
| `screenshot <abs path>` | Capture the frame to a PNG (takes a frame or two; sleep 2 s before reading it) |

## Prefabs and pieces
| Command | Effect |
| --- | --- |
| `pieces [filter]` | Hammer piece table entries (`prefab  name  category`) |
| `prefab <name>` | Components, Piece / PrivateArea data and meshes of a registered prefab |
| `place <prefab> [dx dz]` | Raw `Instantiate` of a prefab in front of the player, creator set. Bypasses build rules |
| `build <prefab> [dx dz]` | `Player.PlacePiece`, the real build path (creator id, Harmony postfixes), no resources consumed |
| `tryplace <prefab>` | `Player.TryPlacePiece`, what a hammer click does (prefix patches run first). Prints `result=` |
| `near [radius]` | Pieces around the player with creator flag |
| `destroy [filter]` | `WearNTear.Destroy` on the nearest matching piece within 10 m (a real "destroyed by damage" path) |
| `areas` | All loaded `PrivateArea`s and whether enabled |
| `toggle [radius]` | Flip activation of the nearest `PrivateArea` and report its lights |

## World
| Command | Effect |
| --- | --- |
| `chars [radius]` | Non-player characters in range with HP and distance |
| `items [radius]` | Item drops in range |
| `bossstones` | Loaded boss stones and their world keys |
| `bossstone <filter> <true/false>` | Force a boss stone's activated state (as if a trophy were hung or removed) |

## Mod under test
| Command | Effect |
| --- | --- |
| `mod <line>` | Forwards to the loaded plugin's `public static string DebugCommand(string)`. With several such plugins: `mod <TypeName> <line>` |
