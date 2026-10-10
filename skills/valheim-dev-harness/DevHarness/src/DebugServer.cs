using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevHarness
{
    // Line-based TCP debug channel. Commands run on the Unity main thread; each reply ends with "<<END>>".
    public class DebugServer
    {
        class Job { public string Line; public StringBuilder Out = new StringBuilder(); public ManualResetEventSlim Done = new ManualResetEventSlim(); }

        private readonly int _port;
        private TcpListener _listener;
        private Thread _thread;
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private volatile bool _running;

        public DebugServer(int port) { _port = port; }

        public void Start()
        {
            _running = true;
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _thread = new Thread(Loop) { IsBackground = true, Name = "HWT-DebugServer" };
            _thread.Start();
        }

        public void Stop() { _running = false; try { _listener?.Stop(); } catch { } }

        void Loop()
        {
            while (_running)
            {
                TcpClient c;
                try { c = _listener.AcceptTcpClient(); } catch { break; }
                ThreadPool.QueueUserWorkItem(_ => Handle(c));
            }
        }

        void Handle(TcpClient c)
        {
            using (c)
            using (var s = c.GetStream())
            using (var r = new StreamReader(s, Encoding.UTF8))
            using (var w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true })
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    var job = new Job { Line = line.Trim() };
                    _jobs.Enqueue(job);
                    if (!job.Done.Wait(30000)) job.Out.AppendLine("ERROR: timeout waiting for main thread");
                    w.Write(job.Out.ToString());
                    w.WriteLine("<<END>>");
                    if (job.Line == "quit") break;
                }
            }
        }

        // Called from Plugin.Update on the main thread.
        public void Pump()
        {
            while (_jobs.TryDequeue(out var job))
            {
                try { Commands.Run(job.Line, job.Out); }
                catch (Exception e) { job.Out.AppendLine("ERROR: " + e); }
                finally { job.Done.Set(); }
            }
        }
    }

    public static class Commands
    {
        public static void Run(string line, StringBuilder o)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) { o.AppendLine("empty"); return; }
            var cmd = parts[0].ToLowerInvariant();
            var args = parts.Skip(1).ToArray();
            var rest = string.Join(" ", args);
            var p = Player.m_localPlayer;

            switch (cmd)
            {
                case "ping": o.AppendLine("pong"); break;

                case "state":
                    o.AppendLine($"scene={SceneManager.GetActiveScene().name} znet={(ZNet.instance != null)} player={(p != null)} " +
                                 $"objectdb={(ObjectDB.instance != null)} znetscene={(ZNetScene.instance != null)} cheats={Terminal.m_cheat}");
                    // state [prefab...] -> also reports whether each named prefab is registered in ZNetScene
                    foreach (var name in args) o.AppendLine($"prefab {name}={(ZNetScene.instance?.GetPrefab(name) != null)}");
                    if (p != null) o.AppendLine($"pos={F(p.transform.position)} hp={p.GetHealth():0}/{p.GetMaxHealth():0}");
                    break;

                case "log": HarnessPlugin.Log.LogInfo("[remote] " + rest); o.AppendLine("ok"); break;

                case "console":
                    if (Console.instance == null) { o.AppendLine("ERROR: no console"); break; }
                    Console.instance.TryRunCommand(rest, false, true);
                    o.AppendLine("ran: " + rest);
                    break;

                case "cheats": Terminal.m_cheat = args.Length == 0 || args[0] != "off"; o.AppendLine("cheats=" + Terminal.m_cheat); break;

                case "pos": Need(p); o.AppendLine(F(p.transform.position)); break;

                case "tp":
                {
                    Need(p);
                    var v = new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
                    p.TeleportTo(v, p.transform.rotation, true);
                    o.AppendLine("teleporting to " + F(v)); break;
                }

                case "god": Need(p); p.SetGodMode(args.Length == 0 || args[0] != "off"); o.AppendLine("god=" + p.InGodMode()); break;

                case "pieces":
                {
                    var table = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
                    if (table == null) { o.AppendLine("ERROR: no hammer table"); break; }
                    var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
                    foreach (var g in table.m_pieces)
                    {
                        var pc = g.GetComponent<Piece>();
                        var s = $"{g.name}\t{pc?.m_name}\t{pc?.m_category}";
                        if (filter == null || s.ToLowerInvariant().Contains(filter)) o.AppendLine(s);
                    }
                    o.AppendLine($"count={table.m_pieces.Count}"); break;
                }

                case "prefab":
                {
                    var g = ZNetScene.instance?.GetPrefab(args[0]);
                    if (g == null) { o.AppendLine("not found"); break; }
                    o.AppendLine($"name={g.name} components: " + string.Join(", ", g.GetComponents<Component>().Select(c => c.GetType().Name)));
                    var pc = g.GetComponent<Piece>();
                    if (pc != null) o.AppendLine($"piece name={pc.m_name} desc={pc.m_description} category={pc.m_category} resources={pc.m_resources.Length} icon={(pc.m_icon != null)}");
                    var pa = g.GetComponent<PrivateArea>();
                    if (pa != null) o.AppendLine($"privatearea name={pa.m_name} radius={pa.m_radius}");
                    var mf = g.GetComponentsInChildren<MeshFilter>(true);
                    o.AppendLine("meshes: " + string.Join(", ", mf.Select(m => m.sharedMesh?.name)));
                    break;
                }

                case "place":
                {
                    // place <prefab> [dx dz] -> spawns prefab in front of the player as a built piece
                    Need(p);
                    var g = ZNetScene.instance.GetPrefab(args[0]);
                    if (g == null) { o.AppendLine("ERROR: prefab not found"); break; }
                    float dx = args.Length > 1 ? float.Parse(args[1]) : 0f, dz = args.Length > 2 ? float.Parse(args[2]) : 3f;
                    var pos = p.transform.position + p.transform.forward * dz + p.transform.right * dx;
                    if (ZoneSystem.instance.GetGroundHeight(pos, out var h)) pos.y = h;
                    var inst = UnityEngine.Object.Instantiate(g, pos, Quaternion.LookRotation(-p.transform.forward));
                    var pc = inst.GetComponent<Piece>();
                    if (pc != null) pc.SetCreator(p.GetPlayerID(), Splatform.PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
                    o.AppendLine($"placed {inst.name} at {F(pos)} zdo={(inst.GetComponent<ZNetView>()?.GetZDO() != null)}");
                    break;
                }

                case "build":
                {
                    // build <prefab> [dx dz] -> places through Player.PlacePiece (the real build path: creator, mod patches), no resources
                    Need(p);
                    var g = ZNetScene.instance.GetPrefab(args[0]);
                    if (g == null) { o.AppendLine("ERROR: prefab not found"); break; }
                    float dx = args.Length > 1 ? float.Parse(args[1]) : 0f, dz = args.Length > 2 ? float.Parse(args[2]) : 3f;
                    var pos = p.transform.position + p.transform.forward * dz + p.transform.right * dx;
                    if (ZoneSystem.instance.GetGroundHeight(pos, out var h)) pos.y = h;
                    p.PlacePiece(g.GetComponent<Piece>(), pos, Quaternion.LookRotation(-p.transform.forward), false, true);
                    o.AppendLine($"built {args[0]} at {F(pos)}"); break;
                }

                case "tryplace":
                {
                    // tryplace <prefab> -> Player.TryPlacePiece, i.e. what the hammer click does (mod prefixes run first)
                    Need(p);
                    var g = ZNetScene.instance.GetPrefab(args[0]);
                    if (g == null) { o.AppendLine("ERROR: prefab not found"); break; }
                    o.AppendLine("result=" + p.TryPlacePiece(g.GetComponent<Piece>())); break;
                }

                case "near":
                {
                    Need(p);
                    float r = args.Length > 0 ? float.Parse(args[0]) : 20f;
                    var list = new List<Piece>();
                    Piece.GetAllPiecesInRadius(p.transform.position, r, list);
                    foreach (var pc in list) o.AppendLine($"{pc.gameObject.name}\t{pc.m_name}\t{F(pc.transform.position)}\tcreator={pc.IsCreator()}");
                    o.AppendLine($"count={list.Count}"); break;
                }

                case "areas":
                {
                    var areas = UnityEngine.Object.FindObjectsOfType<PrivateArea>();
                    foreach (var a in areas) o.AppendLine($"{a.gameObject.name}\t{a.m_name}\t{F(a.transform.position)}\tenabled={(bool)HarmonyLib.AccessTools.Method(typeof(PrivateArea), "IsEnabled").Invoke(a, null)}");
                    o.AppendLine($"count={areas.Length}"); break;
                }

                case "toggle":
                {
                    // toggle [radius] -> flips activation of the nearest PrivateArea (as the creator would by interacting)
                    Need(p);
                    float r = args.Length > 0 ? float.Parse(args[0]) : 10f;
                    PrivateArea best = null; float bd = r;
                    foreach (var a in UnityEngine.Object.FindObjectsOfType<PrivateArea>())
                    {
                        float d = Vector3.Distance(a.transform.position, p.transform.position);
                        if (d < bd) { bd = d; best = a; }
                    }
                    if (best == null) { o.AppendLine("no area in range"); break; }
                    var isEn = HarmonyLib.AccessTools.Method(typeof(PrivateArea), "IsEnabled");
                    var setEn = HarmonyLib.AccessTools.Method(typeof(PrivateArea), "SetEnabled");
                    bool was = (bool)isEn.Invoke(best, null);
                    setEn.Invoke(best, new object[] { !was });
                    bool lightOn = best.m_enabledEffect != null && best.m_enabledEffect.activeInHierarchy;
                    o.AppendLine($"{best.gameObject.name} enabled {was} -> {!was}; enabledEffect active={lightOn}");
                    foreach (var l in best.GetComponentsInChildren<Light>(true))
                        o.AppendLine($"  light {l.gameObject.name} color={l.color} intensity={l.intensity} active={l.gameObject.activeInHierarchy}");
                    break;
                }

                case "mod":
                {
                    // mod <line> -> forwards to the mod under test: any loaded BepInEx plugin type exposing
                    // `public static string DebugCommand(string line)`. With several such mods: mod <TypeName> <line>.
                    var mods = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .Where(x => x.GetCustomAttributes(typeof(BepInEx.BepInPlugin), false).Length > 0 &&
                                    x.GetMethod("DebugCommand", new[] { typeof(string) })?.IsStatic == true).ToList();
                    if (mods.Count == 0) { o.AppendLine("ERROR: no loaded plugin exposes static DebugCommand(string)"); break; }
                    var target = mods.Count == 1 ? mods[0] : mods.FirstOrDefault(x => x.FullName == args[0] || x.Name == args[0]);
                    if (target == null) { o.AppendLine("ERROR: several mods expose DebugCommand; use: mod <TypeName> <line>. Loaded: " + string.Join(", ", mods.Select(m => m.FullName))); break; }
                    if (mods.Count > 1) rest = string.Join(" ", args.Skip(1));
                    o.AppendLine((string)target.GetMethod("DebugCommand", new[] { typeof(string) }).Invoke(null, new object[] { rest }));
                    break;
                }

                case "chars":
                {
                    Need(p);
                    float r = args.Length > 0 ? float.Parse(args[0]) : 80f;
                    var list = new List<Character>();
                    Character.GetCharactersInRange(p.transform.position, r, list);
                    foreach (var c in list.Where(c => !c.IsPlayer()))
                        o.AppendLine($"{c.gameObject.name}\t{c.GetHealth():0}/{c.GetMaxHealth():0}\t{F(c.transform.position)}\t{Vector3.Distance(c.transform.position, p.transform.position):0}m");
                    o.AppendLine($"count={list.Count(c => !c.IsPlayer())}"); break;
                }

                case "items":
                {
                    Need(p);
                    float r = args.Length > 0 ? float.Parse(args[0]) : 10f;
                    var drops = UnityEngine.Object.FindObjectsOfType<ItemDrop>().Where(d => Vector3.Distance(d.transform.position, p.transform.position) <= r).ToList();
                    foreach (var d in drops) o.AppendLine($"{d.gameObject.name}\tx{d.m_itemData.m_stack}\t{F(d.transform.position)}");
                    o.AppendLine($"count={drops.Count}"); break;
                }

                case "destroy":
                {
                    // destroy [filter] -> WearNTear.Destroy on the nearest piece (optionally name filtered) within 10 m
                    Need(p);
                    var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
                    var pcs = new List<Piece>();
                    Piece.GetAllPiecesInRadius(p.transform.position, 10f, pcs);
                    var pc = pcs.Where(x => filter == null || x.gameObject.name.ToLowerInvariant().Contains(filter))
                                .OrderBy(x => Vector3.Distance(x.transform.position, p.transform.position)).FirstOrDefault();
                    if (pc == null) { o.AppendLine("no piece"); break; }
                    var wnt = pc.GetComponent<WearNTear>();
                    if (wnt == null) { o.AppendLine("no WearNTear"); break; }
                    HarmonyLib.AccessTools.Method(typeof(WearNTear), "Destroy").Invoke(wnt, new object[] { null, false }); // private Destroy(HitData, bool) = a real "destroyed by damage" path
                    o.AppendLine("destroyed " + pc.gameObject.name); break;
                }

                case "keys":
                    o.AppendLine(string.Join("\n", ZoneSystem.instance.GetGlobalKeys())); break;

                case "bossstones":
                    foreach (var b in UnityEngine.Object.FindObjectsOfType<BossStone>())
                        o.AppendLine($"{b.gameObject.name}\tkey={b.m_setsWorldKey}\t{F(b.transform.position)}");
                    break;

                case "bossstone":
                {
                    // bossstone <nameFilter> <true|false> -> force a boss stone's activated state (as if a trophy were hung / removed)
                    var b = UnityEngine.Object.FindObjectsOfType<BossStone>().FirstOrDefault(x => x.gameObject.name.ToLowerInvariant().Contains(args[0].ToLowerInvariant()));
                    if (b == null) { o.AppendLine("no such boss stone loaded (stand near the sacrificial stones)"); break; }
                    HarmonyLib.AccessTools.Method(typeof(BossStone), "SetActivated").Invoke(b, new object[] { bool.Parse(args[1]), false });
                    b.CancelInvoke("UpdateVisual"); // stop the stone reverting to the real item-stand state every second
                    o.AppendLine($"{b.gameObject.name} activated={args[1]}"); break;
                }

                case "hover":
                {
                    Need(p);
                    var go = p.GetHoverObject();
                    o.AppendLine(go == null ? "nothing" : $"{go.name} : {p.GetHoverName()}"); break;
                }

                case "lookat":
                {
                    Need(p);
                    var v = new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
                    var dir = v - p.transform.position; dir.y = 0;
                    p.transform.rotation = Quaternion.LookRotation(dir.normalized);
                    o.AppendLine("ok"); break;
                }

                case "cam":
                {
                    // cam <distance> [fov] -> camera distance behind the player (0 = first person) and field of view
                    var cam = GameCamera.instance;
                    if (cam == null) { o.AppendLine("ERROR: no camera"); break; }
                    var dist = HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_distance");
                    if (args.Length > 0) { var d = float.Parse(args[0]); dist.SetValue(cam, d); cam.m_maxDistance = Mathf.Max(cam.m_maxDistance, d); }
                    if (args.Length > 1) cam.m_fov = float.Parse(args[1]);
                    o.AppendLine($"distance={dist.GetValue(cam)} fov={cam.m_fov}"); break;
                }

                case "freecam":
                {
                    // freecam <x y z> <lookX lookY lookZ> -> detach the camera (game free-fly mode) at a position looking at a point;
                    // freecam off -> back to the player camera. For screenshots without the player model in frame.
                    var cam = GameCamera.instance;
                    if (cam == null) { o.AppendLine("ERROR: no camera"); break; }
                    var ff = HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFly");
                    if (args.Length == 0 || args[0] == "off") { ff.SetValue(cam, false); o.AppendLine("freecam off"); break; }
                    var at = new Vector3(float.Parse(args[0]), float.Parse(args[1]), float.Parse(args[2]));
                    var look = new Vector3(float.Parse(args[3]), float.Parse(args[4]), float.Parse(args[5]));
                    var dir = (look - at).normalized;
                    ff.SetValue(cam, true);
                    HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFlyLockon").SetValue(cam, null);
                    HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFlyTarget").SetValue(cam, null);
                    HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFlySavedVel").SetValue(cam, Vector3.zero);
                    HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFlyYaw").SetValue(cam, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
                    HarmonyLib.AccessTools.Field(typeof(GameCamera), "m_freeFlyPitch").SetValue(cam, -Mathf.Asin(dir.y) * Mathf.Rad2Deg);
                    cam.transform.position = at;
                    o.AppendLine($"freecam at {F(at)} looking at {F(look)}"); break;
                }

                case "hud":
                {
                    // hud on|off -> show or hide the whole in-game HUD (clean screenshots)
                    if (Hud.instance == null) { o.AppendLine("ERROR: no hud"); break; }
                    Hud.instance.m_userHidden = args.Length > 0 && args[0] == "off";
                    o.AppendLine("hud hidden=" + Hud.instance.m_userHidden); break;
                }

                case "screenshot":
                {
                    var path = args.Length > 0 ? rest : Path.Combine(Application.persistentDataPath, "hwt_screenshot.png");
                    ScreenCapture.CaptureScreenshot(path);
                    o.AppendLine("capturing to " + path); break;
                }

                case "menu":
                {
                    var f = FejdStartup.instance;
                    if (f == null) { o.AppendLine("ERROR: not in menu"); break; }
                    typeof(FejdStartup).GetMethod(args[0]).Invoke(f, null);
                    o.AppendLine("ok"); break;
                }

                case "quit": Application.Quit(); o.AppendLine("quitting"); break;

                default: o.AppendLine("unknown command: " + cmd + " (ping state log console cheats pos tp god pieces prefab place build tryplace near areas toggle mod chars items destroy keys bossstones bossstone hover lookat cam freecam hud screenshot menu quit)"); break;
            }
        }

        static void Need(Player p) { if (p == null) throw new Exception("no local player"); }
        static string F(Vector3 v) => $"{v.x:0.0} {v.y:0.0} {v.z:0.0}";
    }
}
