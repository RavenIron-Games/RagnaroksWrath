// Resolves, against the REAL Valheim assemblies, every reflection lookup and Harmony patch
// target the Raven Iron mods depend on. These are the surfaces a compiler cannot check: a
// renamed field or a changed parameter list yields null at runtime, silently, and the mod
// keeps running with a feature quietly switched off. That is exactly what Valheim 1.0.7 did.
//
// MetadataLoadContext inspects the assemblies without executing them, so no Unity runtime is
// needed and this can run anywhere the game files exist.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

class Probe
{
    static MetadataLoadContext _mlc;
    static Assembly[] _asms;
    static int _pass, _fail;

    const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static int Main(string[] args)
    {
        string managed = args.Length > 0 ? args[0]
            : @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed";
        if (!Directory.Exists(managed)) { Console.WriteLine("No Managed dir: " + managed); return 2; }

        // The game ships its own Mono mscorlib. Feeding the host runtime's copy in as well
        // makes MetadataLoadContext refuse to start, so dedupe by simple name and let the
        // game's own assemblies win — they are the ones the mods actually bind against.
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.GetFiles(managed, "*.dll"))
            byName[Path.GetFileNameWithoutExtension(f)] = f;
        foreach (var f in Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"))
            if (!byName.ContainsKey(Path.GetFileNameWithoutExtension(f)))
                byName[Path.GetFileNameWithoutExtension(f)] = f;

        _mlc = new MetadataLoadContext(new PathAssemblyResolver(byName.Values.ToList()), "mscorlib");
        // Splatform is the third one that matters: PlatformUserID and the platform ids the admin
        // lists are matched against live there, not in assembly_valheim.
        var loaded = new List<Assembly>();
        foreach (var name in new[] { "assembly_valheim.dll", "assembly_utils.dll", "Splatform.dll" })
        {
            string path = Path.Combine(managed, name);
            if (File.Exists(path)) loaded.Add(_mlc.LoadFromAssemblyPath(path));
        }
        _asms = loaded.ToArray();

        Console.WriteLine("Valheim assemblies: " + managed);
        Console.WriteLine();

        Console.WriteLine("=== FireFront reflection lookups ===");
        M("TreeLog", "Destroy", AnyInstance, "HitData", "System.Boolean");
        M("WearNTear", "Destroy", AnyInstance, "HitData", "System.Boolean");
        M("ZDOMan", "FindSectorObjects", AnyInstance, "Vector2s", "SimulationDistance", "List<ZDO>", "List<ZDO>");
        M("TerrainComp", "PaintCleared", AnyInstance, "UnityEngine.Vector3", "UnityEngine.Vector3", "TerrainOp+Settings");
        M("TerrainComp", "Save", AnyInstance, "System.Boolean");
        M("TerrainComp", "FindTerrainCompiler", AnyStatic, "UnityEngine.Vector3");
        M("TerrainComp", "IsOwner", AnyInstance);
        M("Player", "PlacePiece", AnyInstance, "Piece", "UnityEngine.Vector3", "UnityEngine.Quaternion", "System.Boolean", "System.Boolean");
        M("Player", "Message", AnyInstance, "MessageHud+MessageType", "System.String", "System.Int32", "UnityEngine.Sprite", "System.Boolean");
        M("Character", "AddFireDamage", AnyInstance, "System.Single", "System.Int16");
        M("Character", "GetSEMan", AnyInstance);
        M("SEMan", "AddStatusEffect", AnyInstance, "System.Int32", "System.Boolean", "System.Int32", "System.Single", "System.Int16");
        M("SEMan", "AddStatusEffect", AnyInstance, "StatusEffect", "System.Boolean", "System.Int32", "System.Single", "System.Int16");
        M("ZoneSystem", "GetGroundHeight", AnyInstance, "UnityEngine.Vector3");
        F("SEMan", "s_statusEffectBurning", AnyStatic);
        F("ZoneSystem", "s_instance", AnyStatic);
        F("ZoneSystem", "m_waterLevel", AnyInstance);
        F("GameCamera", "m_instance", AnyStatic);
        F("ZNetScene", "s_instance", AnyStatic);
        F("ZNetScene", "m_namedPrefabs", AnyInstance);
        F("EnvMan", "s_instance", AnyStatic);
        // The weather replay behind FireFront's ValheimBridge.IsRainingAt (0.21.0+), which
        // Ragnarok's Wrath's storm lightning also asks since 0.28.0. If one of these moves,
        // FireFront reads every sky as dry: its own rain suppression stops, and bolts fall in
        // rain again.
        M("EnvMan", "GetAvailableEnvironments", AnyInstance, "BiomeSector");
        M("EnvMan", "SelectWeightedEnvironment", AnyInstance, "List<EnvEntry>");
        M("EnvMan", "GetEnv", AnyInstance, "System.String");
        F("EnvMan", "m_environmentDuration", AnyInstance);
        F("EnvMan", "m_debugEnv", AnyInstance);
        F("EnvMan", "m_forceEnv", AnyInstance);

        Console.WriteLine();
        Console.WriteLine("=== Ragnarok's Wrath surfaces ===");
        M("ZoneSystem", "GetZone", AnyStatic, "UnityEngine.Vector3");
        M("ZoneSystem", "GetZonePos", AnyStatic, "Vector2s");
        M("SaveSystem", "GetWorldsSaveRootPath", AnyStatic, "FileHelpers+FileSource");
        M("SpawnSystem", "GetLevelUpChance", AnyStatic, "UnityEngine.Vector3", "SpawnSystem+SpawnData");
        M("SpawnSystem", "GetNrOfInstances", AnyStatic, "UnityEngine.GameObject", "UnityEngine.Vector3", "System.Single", "System.Boolean", "System.Boolean");
        M("ZDOMan", "FindSectorObjects", AnyInstance, "Vector2s", "SimulationDistance", "List<ZDO>", "List<ZDO>");
        M("EnvMan", "GetCurrentDay", AnyInstance);   // rule 5: private, reached by AccessTools
        F("ZDOVars", "s_creator", AnyStatic);
        // Review fixes, 2026-09-24: the storm's wait for a running event, the sender checks on
        // relic and zone messages (SenderGuard, RelicSync), and the world-close reset. Most are
        // direct calls, so this catches a rename or a new signature, NOT a member turning private
        // (M/F search public and non-public alike; rule 5's trap needs a visibility check).
        F("ZNetPeer", "m_refPos", AnyInstance);          // RelicSync's reporter-near test
        M("RandEventSystem", "GetCurrentRandomEvent", AnyInstance);
        M("RandEventSystem", "HaveEvent", AnyInstance, "System.String");
        M("ZNet", "GetServerPeer", AnyInstance);
        M("ZNet", "GetPeer", AnyInstance, "System.Int64");
        F("ZNetPeer", "m_rpc", AnyInstance);
        M("ZPackage", "GetPos", AnyInstance);
        M("ZPackage", "SetPos", AnyInstance, "System.Int32");
        M("ZPackage", "ReadZDOID", AnyInstance);
        Any("ZNet", "OnDestroy");                    // Harmony target, private, patched by name
        Any("ZRoutedRpc", "RPC_RoutedRPC");          // Harmony target, private, patched by name

        // The four mods below were outside this probe until 2026-09-11. That gap was the whole
        // reason the 1.0.7 port needed a 32-agent sweep to find what a tool should have found.
        Console.WriteLine();
        Console.WriteLine("=== Cairn surfaces ===");
        F("Terminal", "commands", AnyStatic);              // protected static; the console registry
        F("Raven", "m_instance", AnyStatic);
        F("Raven", "m_isMunin", AnyInstance);
        F("Raven", "m_staticTexts", AnyStatic);
        F("Raven", "m_tempTexts", AnyStatic);              // public STATIC, like m_staticTexts
        F("ZNetScene", "m_prefabs", AnyInstance);
        F("ItemDrop", "m_itemData", AnyInstance);
        F("ItemDrop+ItemData", "m_shared", AnyInstance);
        F("Piece", "m_resources", AnyInstance);
        F("Piece+Requirement", "m_amount", AnyInstance);
        F("Piece+Requirement", "m_resItem", AnyInstance);
        F("ItemDrop+ItemData+SharedData", "m_buildPieces", AnyInstance);
        F("PieceTable", "m_pieces", AnyInstance);

        Console.WriteLine();
        Console.WriteLine("=== Undertow surfaces ===");
        F("Terminal", "commands", AnyStatic);
        Any("Character", "UpdateSwimming");
        Any("Ship", "CustomFixedUpdate");

        Console.WriteLine();
        Console.WriteLine("=== RavenEye surfaces ===");
        // Shared by RavenEye's AdminGate, FireFront's PeerIsAdmin and Valkyrie's Cargo.
        // 1.0.12 changed its BODY, not its shape: `flag = list.Contains(filtered)` became
        // `flag |= ...`, so a match on the UNFILTERED id is no longer thrown away. That is a
        // vanilla bug fix, and it makes admin matching slightly more forgiving, never less.
        M("ZNet", "ListContainsId", AnyInstance | AnyStatic, "SyncedList", "System.String");
        Any("Game", "UpdateNoMap");
        Any("ZNet", "GetOtherPublicPlayers");
        Any("Minimap", "Explore");

        Console.WriteLine();
        Console.WriteLine("=== Valkyrie's Cargo surfaces ===");
        F("ZNet", "m_adminList", AnyInstance);
        F("ZNet", "m_connectionStatus", AnyStatic);
        F("ZNetPeer", "m_socket", AnyInstance);
        F("ZRoutedRpc", "m_peers", AnyInstance);
        F("ZRpc", "m_socket", AnyInstance);
        F("ZRpc", "m_functions", AnyInstance);
        F("ZPlayFabSocket", "m_remotePlayerId", AnyInstance);
        M("ZNet", "GetPeer", AnyInstance, "ZRpc");
        foreach (var t in new[]
        {
            ("BaseAI","IsEnemy"), ("Character","RPC_Damage"), ("Character","ApplyDamage"),
            ("Character","GetHoverText"), ("Character","InIntro"), ("Chat","HasFocus"),
            ("FejdStartup","ShowConnectError"), ("GameCamera","UpdateMouseCapture"),
            ("Humanoid","Awake"), ("ZNet","Awake"), ("ZNet","OnNewConnection"),
            ("ZNet","RPC_PeerInfo"), ("ZNet","Shutdown"), ("ZNet","Disconnect"),
            ("ZRpc","HandlePackage"),
        })
            Any(t.Item1, t.Item2);

        Console.WriteLine();
        Console.WriteLine("=== Harmony patch targets (a missing one throws at patch time) ===");
        foreach (var t in new[]
        {
            ("CreatureSpawner","Spawn"), ("Plant","GetGrowTime"), ("Player","OnDeath"),
            ("RandEventSystem","Awake"), ("Terminal","InitTerminal"), ("Character","GetHoverName"),
            ("Destructible","Destroy"), ("Pickable","GetHoverText"), ("Pickable","Interact"),
            ("Plant","GetHoverText"), ("Plant","UpdateHealth"), ("Player","GetHoverName"),
            ("SpawnSystem","GetLevelUpChance"), ("ObjectDB","Awake"), ("ObjectDB","CopyOtherDB"),
            ("Projectile","OnHit"), ("TreeBase","RPC_Damage"), ("TreeLog","RPC_Damage"),
            ("WearNTear","OnDestroy"), ("WearNTear","RPC_Damage"), ("ZNetScene","Awake"),
        })
            Any(t.Item1, t.Item2);

        Console.WriteLine();
        Console.WriteLine($"RESOLVED {_pass}   FAILED {_fail}");
        return _fail > 0 ? 1 : 0;
    }

    static Type T(string name)
    {
        if (name.StartsWith("List<"))
        {
            Type inner = T(name.Substring(5, name.Length - 6));
            if (inner == null) return null;
            // List`1 must come from the CORE assembly this context loaded (the game's Mono
            // mscorlib), not the host runtime's. Types from two different assemblies are two
            // different types, and GetMethod would miss on identity alone - which looks
            // exactly like a real API break and is not one.
            Type open = _asms[0].GetType("System.Collections.Generic.List`1", false);
            if (open == null)
                foreach (var a in _mlc.GetAssemblies())
                {
                    open = a.GetType("System.Collections.Generic.List`1", false);
                    if (open != null) break;
                }
            return open?.MakeGenericType(inner);
        }
        foreach (var a in _asms) { var t = a.GetType(name, false); if (t != null) return t; }
        foreach (var a in _mlc.GetAssemblies()) { var t = a.GetType(name, false); if (t != null) return t; }
        return null;
    }

    static void M(string type, string method, BindingFlags flags, params string[] ps)
    {
        string label = $"{type}.{method}({string.Join(", ", ps.Select(Short))})";
        Type t = T(type);
        if (t == null) { Console.WriteLine($"  MISSING TYPE  {type}"); _fail++; return; }
        var types = new Type[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            types[i] = T(ps[i]);
            if (types[i] == null) { Console.WriteLine($"  MISSING PARAM TYPE {ps[i]}  ({label})"); _fail++; return; }
        }
        var m = t.GetMethod(method, flags, null, types, null);
        if (m != null) { Console.WriteLine($"  ok    {label}"); _pass++; }
        else
        {
            Console.WriteLine($"  NULL  {label}   <-- would silently no-op in-game");
            // Print what DOES exist, so a miss names its own replacement instead of just
            // saying no.
            foreach (var cand in t.GetMethods(flags).Where(x => x.Name == method))
                Console.WriteLine($"          exists: {cand.Name}({string.Join(", ", cand.GetParameters().Select(p => p.ParameterType.Name))})");
            _fail++;
        }
    }

    static void F(string type, string field, BindingFlags flags)
    {
        Type t = T(type);
        if (t == null) { Console.WriteLine($"  MISSING TYPE  {type}"); _fail++; return; }
        var f = t.GetField(field, flags);
        if (f != null) { Console.WriteLine($"  ok    {type}.{field}"); _pass++; }
        else { Console.WriteLine($"  NULL  {type}.{field}   <-- would silently no-op in-game"); _fail++; }
    }

    static void Any(string type, string member)
    {
        Type t = T(type);
        if (t == null) { Console.WriteLine($"  MISSING TYPE  {type}"); _fail++; return; }
        var ms = t.GetMember(member, MemberTypes.Method, AnyInstance | AnyStatic);
        if (ms.Length > 0) { Console.WriteLine($"  ok    {type}.{member}  [{ms.Length} overload(s)]"); _pass++; }
        else { Console.WriteLine($"  NULL  {type}.{member}   <-- Harmony would throw while patching"); _fail++; }
    }

    static string Short(string s)
    {
        int i = s.LastIndexOf('.');
        return i >= 0 ? s.Substring(i + 1) : s;
    }
}
