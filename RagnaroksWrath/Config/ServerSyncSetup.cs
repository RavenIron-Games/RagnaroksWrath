using System;
using System.Reflection;
using BepInEx.Configuration;
using RavenIron.RagnaroksWrath.Core;
using ServerSync;

namespace RavenIron.RagnaroksWrath.Config
{
    /// <summary>
    /// The server's config, enforced on every player, through blaxxun's ServerSync (vendored in
    /// Vendor\ServerSync.cs, byte-identical to Valkyrie's Cargo's copy). Added 2026-11-17.
    ///
    /// WHAT IT DOES. Every setting in both files, except the few <see cref="SyncRules"/> keeps local,
    /// is sent from the server to each player at login and again whenever it changes there. A player's
    /// game then reads the server's value; their own file is left exactly as it was (ServerSync holds
    /// the local value aside and puts it back on disconnect, and never saves the server's into it).
    /// With LockConfiguration on (the default) only an admin on adminlist.txt can change a synced
    /// value from a client, for instance through a configuration manager, and that change goes to the
    /// server and out to everyone.
    ///
    /// THE VERSION GATE, deliberately strict, as in Valkyrie's Cargo: ModRequired with the minimum
    /// equal to this build. A player without the mod, or on any other version, is refused at the
    /// handshake with a message naming both versions. Ragnarok's Wrath runs real code on every
    /// player's game (titles, the nemesis, consequences, the wild's answer, every visual), so a
    /// mismatched client was always quietly wrong; now it is told.
    ///
    /// ORDER. Registered AFTER ModConfig.Bind, so the migration has finished writing both files
    /// before ServerSync can intercept a single value. ServerSync's own Harmony patches arrive with
    /// this assembly's PatchAll (its nested classes carry the attributes), and it skips patching
    /// itself again when it finds them on ZNet.Awake.
    /// </summary>
    public static class ServerSyncSetup
    {
        public static ConfigSync Sync { get; private set; }

        /// <summary>ServerSync's lock: on, only admins may change the server's values from a client.</summary>
        public static ConfigEntry<bool> LockConfiguration { get; private set; }

        public static int SyncedCount { get; private set; }
        public static int LocalCount { get; private set; }

        public static void Register(ConfigFile main, ConfigFile advanced, string guid, string displayName, string version)
        {
            Sync = new ConfigSync(guid)
            {
                DisplayName = displayName,
                CurrentVersion = version,
                MinimumRequiredVersion = version,
                ModRequired = true,
            };

            // New in config layout 4's build, in the main file beside VerboseLogging. A brand-new key
            // needs no migration: BepInEx writes it at its default into a file that never had it.
            LockConfiguration = main.Bind(ModConfig.GeneralSection, "LockConfiguration", true,
                "Only admins (adminlist.txt) may change this mod's settings from a player's game. Every " +
                "player always gets the server's settings either way.");
            Sync.AddLockingConfigEntry(LockConfiguration);

            MethodInfo add = typeof(ConfigSync).GetMethod(nameof(ConfigSync.AddConfigEntry));
            foreach (ConfigFile file in new[] { main, advanced })
            {
                if (file == null) continue;
                // ConfigFile is an IDictionary whose Values is an explicit interface member; copy
                // first, so nothing a registration does can disturb the walk.
                var entries = new System.Collections.Generic.List<ConfigEntryBase>();
                foreach (System.Collections.Generic.KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in file)
                    if (kv.Value != null) entries.Add(kv.Value);

                foreach (ConfigEntryBase entry in entries)
                {
                    if (ReferenceEquals(entry, LockConfiguration)) continue;
                    try
                    {
                        // AddConfigEntry is generic over the entry's own type, and the two files hold
                        // bools, ints, floats and strings side by side.
                        var own = (OwnConfigEntryBase)add.MakeGenericMethod(entry.SettingType).Invoke(Sync, new object[] { entry });
                        if (SyncRules.IsLocal(entry.Definition.Section, entry.Definition.Key))
                        {
                            own.SynchronizedConfig = false;
                            LocalCount++;
                        }
                        else SyncedCount++;
                    }
                    catch (Exception ex)
                    {
                        RagnaroksWrath.Log.LogWarning(
                            $"ServerSync: could not register [{entry.Definition.Section}] {entry.Definition.Key}: {ex.Message}. " +
                            "It stays a local setting.");
                    }
                }
            }

            RagnaroksWrath.Log.LogInfo(
                $"ServerSync: {SyncedCount} setting(s) follow the server on every player's game, {LocalCount} stay local " +
                $"(the layout stamp, VerboseLogging, the visual dials). Every player needs exactly {displayName} {version}.");
        }

        /// <summary>One line for `wrath status`: who the config is coming from on this machine.</summary>
        public static string Describe()
        {
            ConfigSync s = Sync;
            if (s == null) return "config sync: not set up";
            return "config sync: " + (s.IsSourceOfTruth ? "this machine's own file" : "following the server") +
                   $", locked {(s.IsLocked ? "yes" : "no")}, admin here {(s.IsAdmin ? "yes" : "no")}, " +
                   $"{SyncedCount} synced / {LocalCount} local";
        }
    }
}
