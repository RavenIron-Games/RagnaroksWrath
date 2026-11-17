using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;

namespace RavenIron.RagnaroksWrath
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    public class RagnaroksWrath : BaseUnityPlugin
    {
        public const string PluginId      = "com.raveniron.ragnarokswrath";
        public const string PluginName    = "Ragnarok's Wrath";
        public const string PluginVersion = "0.30.0";

        public static RagnaroksWrath Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        /// <summary>
        /// True once ZNet exists and this process is the authority for world simulation.
        /// Null before ZNet.Start — callers must handle "not known yet", which is why this
        /// is a method rather than a cached bool set at Awake.
        /// </summary>
        public static bool IsSimulationAuthority()
        {
            ZNet znet = ZNet.instance;
            if (znet == null) return false;
            return znet.IsServer();
        }

        /// <summary>Headless dedicated server: no local player, no presence layer, no rendering.</summary>
        public static bool IsDedicated()
        {
            ZNet znet = ZNet.instance;
            return znet != null && znet.IsDedicated();
        }

        /// <summary>
        /// What Harmony.PatchAll does inside, one patch class at a time, each in its own try/catch.
        /// PatchAll stops at the first class that throws, so one patch a game update broke took every
        /// patch after it down too; with the vendored ServerSync's classes in this assembly since
        /// 2026-11-17 that would include the config lock and the version gate. Valkyrie's Cargo made
        /// the same change for the same reason (its issue #31). Every failure is logged by name.
        /// </summary>
        private static void PatchEachClass(Harmony harmony)
        {
            System.Type[] types;
            try { types = AccessTools.GetTypesFromAssembly(typeof(RagnaroksWrath).Assembly); }
            catch (System.Exception ex) { Log.LogError($"patches: could not list this mod's types, none applied: {ex.Message}"); return; }

            int applied = 0, failed = 0;
            foreach (System.Type type in types)
            {
                if (type == null) continue;
                bool isPatch;
                try { isPatch = type.GetCustomAttributes(typeof(HarmonyAttribute), true).Length > 0; }
                catch { isPatch = false; }
                if (!isPatch) continue;

                try { harmony.CreateClassProcessor(type).Patch(); applied++; }
                catch (System.Exception ex)
                {
                    failed++;
                    Log.LogError($"patch {type.FullName} did not apply: {ex.Message}");
                }
            }
            if (failed > 0) Log.LogError($"patches: {applied} applied, {failed} FAILED (named above).");
            else Log.LogInfo($"patches: {applied} patch class(es) applied.");
        }

        private static void OnStormSettingChanged(object sender, BepInEx.Configuration.SettingChangedEventArgs e)
        {
            try
            {
                if (e?.ChangedSetting == null) return;
                if (Core.StormLive.IsBakedKey(e.ChangedSetting.Definition.Key))
                    Systems.World.WeatherSystem.RefreshStormEvents();
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"storm settings: could not apply a change live: {ex.Message}");
            }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Two files since 0.28.0: the settings an owner changes, and the tuning beside them.
            // Nothing is written until ModConfig.Bind finishes (see ConfigMigration), so the
            // advanced file is created on first boot with every setting in it, not empty.
            var advancedConfig = new BepInEx.Configuration.ConfigFile(
                System.IO.Path.Combine(Paths.ConfigPath, ConfigLedger.AdvancedFileName), false, Info.Metadata);
            ModConfig.Bind(base.Config, advancedConfig);

            // Live storm settings (0.30.0). The sky, duration and range are copied into vanilla's
            // storm events, so a change has to be pushed into them. Hooked AFTER Bind so the
            // migration's own writes do not count as an owner's edit. Both files: the range lives
            // in the advanced one. Fires for a configuration manager, `wrath storm set` and a
            // config reload alike.
            base.Config.SettingChanged += OnStormSettingChanged;
            advancedConfig.SettingChanged += OnStormSettingChanged;

            // The server's settings on every player's game (2026-11-17). After the migration, so
            // nothing intercepts its writes. A value arriving from the server raises SettingChanged
            // on the client like any other change, so the storm events follow it with no extra code.
            try { ServerSyncSetup.Register(base.Config, advancedConfig, PluginId, PluginName, PluginVersion); }
            catch (System.Exception ex) { Log.LogError($"ServerSync: setup failed, every setting stays local: {ex}"); }

            _harmony = new Harmony(PluginId);
            PatchEachClass(_harmony);

            // WorldTick is a plain MonoBehaviour driven from Update — deliberately NOT a
            // coroutine. See House Style rule 2: long-lived coroutines in this codebase have
            // repeatedly grown a `continue`-past-`yield` hard-lock.
            gameObject.AddComponent<WorldTick>();

            RegisterSystems();

            // Visuals exist only where something renders. GraphicsDeviceType.Null is the
            // headless tell that survives compiling against client reference DLLs (where
            // ZNet.IsDedicated() is a hardcoded false).
            if (UnityEngine.SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                gameObject.AddComponent<Visuals.PlagueFog>();
                gameObject.AddComponent<Visuals.FrostBreath>();
                gameObject.AddComponent<Visuals.ScorchAsh>();
                gameObject.AddComponent<Visuals.RelicRunes>();
                gameObject.AddComponent<Visuals.LightningBolts>();
                gameObject.AddComponent<Client.HealthEffects>();
                gameObject.AddComponent<Client.ConsequenceEffects>();
            }


            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        /// <summary>
        /// Every system registers here, in one place. Systems are ticked in registration order
        /// by WorldTick's round-robin cursor, so ordering here is a mild scheduling hint only —
        /// no system may depend on another having ticked first within the same frame.
        ///
        /// Also called by WorldTick.EndWorld: every world gets fresh system instances, so no
        /// per-world state can survive in an instance field.
        /// </summary>
        internal static void RegisterSystems()
        {
            WorldTick.Register(new Systems.World.SeasonSystem());
            WorldTick.Register(new Systems.World.WeatherSystem());
            WorldTick.Register(new Systems.World.WindSystem());
            WorldTick.Register(new Systems.World.BiomeStateSystem());
            WorldTick.Register(new Systems.World.FireSystem());
            WorldTick.Register(new Systems.World.PlagueSystem());
            WorldTick.Register(new Systems.World.EcologySystem());
            WorldTick.Register(new Systems.World.FarmingSystem());
            WorldTick.Register(new Systems.World.WorldStateSystem());
            WorldTick.Register(new Systems.World.HealthSystem());
            WorldTick.Register(new Systems.World.ConsequenceSystem());
            WorldTick.Register(new Systems.World.RivalrySystem());
            WorldTick.Register(new Systems.World.RelicSystem());
            WorldTick.Register(new Systems.TitleSystem());
            WorldTick.Register(new Systems.ZoneSyncSystem());
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            Instance = null;
        }
    }
}
