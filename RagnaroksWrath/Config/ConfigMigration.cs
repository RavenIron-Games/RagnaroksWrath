using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using RavenIron.RagnaroksWrath.Core;

namespace RavenIron.RagnaroksWrath.Config
{
    /// <summary>
    /// The engine-facing half of the config migration; the decisions are <see cref="ConfigLedger"/>
    /// (pure, off-game, tested). Ported 2026-09-18 from Valkyrie's Cargo, which took the shape from
    /// Wu'barrk's WingsoftheValkyrie — snapshot the raw file BEFORE any bind, back it up beside
    /// itself, apply after every bind, stamp a version, and never let a failed migration stop the
    /// mod loading.
    ///
    /// <see cref="Begin"/> runs before the first `cfg.Bind`; <see cref="Finish"/> after the last.
    /// THE ORDER IS THE WHOLE MECHANISM, not a convention: a backfill acts on a key being ABSENT,
    /// and the moment BepInEx binds that key it is present at its shipped default. Snapshot after
    /// binding and every backfill silently becomes a no-op — the migration would run, log happily,
    /// stamp its version, and change nothing.
    ///
    /// ONE DELIBERATE DIFFERENCE FROM VALKYRIE'S CARGO. There, a failed backup blocks the version
    /// stamp, because that migration RETIRES a key and the backup is the admin's only remaining
    /// copy of it. Nothing here destroys anything: this writes a value to a key that was absent and
    /// touches nothing else, so a backup that could not be written is worth a warning and not worth
    /// refusing to proceed. The backup is still taken, because the next rung on this ladder may not
    /// be so harmless.
    /// </summary>
    public static class ConfigMigration
    {
        /// <summary>
        /// What <see cref="Begin"/> decided, so <see cref="Finish"/> can tell "nothing to do" from
        /// "tried and could not". Stamping a version on a state the mod never reached makes the
        /// failure PERMANENT — the next boot sees a current file and never retries — so only
        /// <see cref="MigrationState.Failed"/> withholds the stamp.
        /// </summary>
        private enum MigrationState { Fresh, AlreadyCurrent, Planned, Failed }

        private static Dictionary<string, string> _snapshot;
        private static ConfigLedger.MigrationPlan _plan;
        private static string _path;
        private static MigrationState _state = MigrationState.Fresh;
        private static bool _backedUp;

        /// <summary>The last migration's boot line, kept for `wrath status`. Empty when nothing has run.</summary>
        public static string LastSummary { get; private set; } = "";

        /// <summary>Call BEFORE the first cfg.Bind. Never throws.</summary>
        public static void Begin(ConfigFile cfg)
        {
            _snapshot = null;
            _plan = null;
            _path = null;
            _state = MigrationState.Fresh;
            _backedUp = false;
            LastSummary = "";

            try
            {
                if (cfg == null) return;

                string path = cfg.ConfigFilePath;

                // A fresh install has no file, and that is not a failure — it is the case where
                // every shipped default is exactly right and a migration would be wrong.
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                _snapshot = ConfigLedger.ParseIni(ReadLinesShared(path));

                int fileVersion = ConfigLedger.ReadVersion(_snapshot);
                if (fileVersion >= ConfigLedger.CurrentVersion)
                {
                    _state = MigrationState.AlreadyCurrent;
                    return;
                }

                _path = path;
                _backedUp = Backup(path, fileVersion);

                _plan = ConfigLedger.Plan(_snapshot, fileVersion);
                _state = MigrationState.Planned;
                LastSummary = ConfigLedger.Describe(_plan);

                // A migration that changes nothing but the stamp is not worth a warning; one that
                // changes an owner's world is, because they did not ask for it and the whole point
                // of this machinery is that they find out from the log rather than from the sky.
                if (_plan.IsEmpty)
                    RagnaroksWrath.Log.LogInfo(LastSummary);
                else
                    RagnaroksWrath.Log.LogWarning(
                        LastSummary +
                        (_backedUp
                            ? " (your previous config is backed up beside it, .v" + fileVersion.ToString(CultureInfo.InvariantCulture) + ".bak)"
                            : " (no backup could be written; nothing here removes a setting, so the migration proceeds anyway)"));
            }
            catch (Exception ex)
            {
                // Never stop the mod loading. Every value binds exactly as it always did; the file
                // is left unstamped, which is precisely what makes the next boot retry.
                _state = MigrationState.Failed;
                RagnaroksWrath.Log.LogError(
                    "Config migration could not start. Every value binds as it always did and nothing " +
                    "has been changed; the config is left unstamped so this retries on the next boot. " +
                    "Reason: " + ex);
            }
        }

        /// <summary>Call AFTER the last cfg.Bind, with the bound Meta.ConfigVersion entry. Never throws.</summary>
        public static void Finish(ConfigFile cfg, ConfigEntry<int> versionEntry)
        {
            try
            {
                if (_plan != null && cfg != null)
                {
                    foreach (string slot in _plan.ResetToDefault)
                    {
                        ConfigEntryBase entry = Lookup(cfg, slot);
                        if (entry == null) continue;
                        entry.BoxedValue = entry.DefaultValue;
                    }

                    foreach (ConfigLedger.BackfilledSlot b in _plan.Backfilled)
                    {
                        ConfigEntryBase entry = Lookup(cfg, b.Slot);
                        if (entry == null)
                        {
                            // The ledger names a key this build does not bind. Only reachable by
                            // editing one file and not the other, and silence would make it
                            // permanent: the version stamps, and the backfill never runs again.
                            RagnaroksWrath.Log.LogWarning(
                                "Config migration wanted to set " + b.Slot + " but this build binds no such key. " +
                                "Nothing was changed for it. This is a bug in ConfigLedger, not in your file.");
                            continue;
                        }

                        try
                        {
                            entry.SetSerializedValue(b.Value);
                        }
                        catch (Exception ex)
                        {
                            RagnaroksWrath.Log.LogWarning(
                                "Config migration could not set " + b.Slot + " to '" + b.Value + "': " + ex.Message +
                                ". That key keeps its shipped default, which may change how this world behaves - " +
                                "see the changelog for what it does.");
                        }
                    }
                }

                if (_state == MigrationState.Failed)
                {
                    RagnaroksWrath.Log.LogWarning(
                        "Config migration did not run this boot; your config is unchanged and unstamped, " +
                        "and the next boot retries.");
                }
                else if (versionEntry != null)
                {
                    versionEntry.Value = ConfigLedger.CurrentVersion;
                }

                if (cfg != null) cfg.Save();
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError(
                    "Config migration could not finish - check the backup beside your config file. Reason: " + ex);
            }
            finally
            {
                _snapshot = null;
                _plan = null;
                _path = null;
                _state = MigrationState.Fresh;
                _backedUp = false;
            }
        }

        /// <summary>The bound entry for a "Section::Key" slot, or null when this build does not bind it.</summary>
        private static ConfigEntryBase Lookup(ConfigFile cfg, string slot)
        {
            int i = slot.IndexOf("::", StringComparison.Ordinal);
            if (i < 0) return null;

            var def = new ConfigDefinition(slot.Substring(0, i), slot.Substring(i + 2));
            return cfg.ContainsKey(def) ? cfg[def] : null;
        }

        /// <summary>
        /// The raw file, line by line, opened with FileShare.ReadWrite so another process holding
        /// the cfg open for writing — a config manager, an editor, a sync tool, the realistic
        /// Windows case — does not fail the migration the way File.ReadAllLines would.
        /// </summary>
        private static List<string> ReadLinesShared(string path)
        {
            var lines = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        /// <summary>
        /// Copies the config beside itself as `.v&lt;fromVersion&gt;.bak`, never overwriting — an
        /// existing backup that differs is somebody's only clean copy from a half-finished earlier
        /// run, so that case falls back to a timestamped name rather than clobbering it. Returns
        /// whether a copy now exists on disk. Never throws.
        /// </summary>
        private static bool Backup(string path, int fromVersion)
        {
            try
            {
                string bak = path + ".v" + fromVersion.ToString(CultureInfo.InvariantCulture) + ".bak";
                if (File.Exists(bak))
                {
                    if (BytesEqual(bak, path)) return true;

                    bak = path + ".v" + fromVersion.ToString(CultureInfo.InvariantCulture) + "." +
                          DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".bak";
                }
                File.Copy(path, bak, overwrite: false);
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning(
                    "Could not back up the config before migrating (" + ex.Message + "). Proceeding anyway: " +
                    "this migration only fills in a key that was absent and removes nothing.");
                return false;
            }
        }

        private static bool BytesEqual(string pathA, string pathB)
        {
            try
            {
                byte[] a = File.ReadAllBytes(pathA);
                byte[] b = File.ReadAllBytes(pathB);
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            catch
            {
                return false;   // cannot prove they match — treat as different, which routes to the timestamped name
            }
        }
    }
}
