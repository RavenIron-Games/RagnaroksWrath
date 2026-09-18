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

        /// <summary>
        /// How many steps <see cref="Apply"/> REFUSED this boot. A refusal is a bug in our own
        /// ledger rather than in the owner's file: a row naming a key this build does not bind.
        /// Counted because <see cref="LastSummary"/> is written in <see cref="Begin"/> from the
        /// plan's INTENT, before a single step has run, and `wrath status` reads it back verbatim.
        /// Without this it claims a value was moved that was never found, and the only
        /// contradiction is a warning several hundred log lines earlier.
        /// </summary>
        private static int _refused;

        /// <summary>Call BEFORE the first cfg.Bind. Never throws.</summary>
        public static void Begin(ConfigFile cfg)
        {
            _refused = 0;
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
                if (_plan != null && cfg != null) Apply(cfg, _plan);

                if (_state == MigrationState.Failed)
                {
                    RagnaroksWrath.Log.LogWarning(
                        "Config migration did not run this boot; your config is unchanged and unstamped, " +
                        "and the next boot retries.");
                }
                else if (versionEntry != null)
                {
                    // THE STAMP ONLY EVER GOES UP. A file carrying a HIGHER version was written by
                    // a newer build whose rungs have already run, and this build knows nothing
                    // about them. Roll the mod back for an afternoon and an unconditional
                    // assignment drags the stamp down; roll forward and those rungs replay against
                    // values the owner has since chosen — and a rebase cannot tell a deliberate
                    // choice from the old default it happens to equal. Corrected 2026-09-18.
                    if (versionEntry.Value < ConfigLedger.CurrentVersion)
                        versionEntry.Value = ConfigLedger.CurrentVersion;
                }

                // LastSummary was written in Begin, from the plan's INTENT, before anything ran.
                // `wrath status` reads it back verbatim, so a refused step has to reach it or the
                // one line the owner actually looks at is confidently wrong.
                if (_refused > 0)
                    LastSummary += " - but " + _refused.ToString(CultureInfo.InvariantCulture) +
                                   " step(s) were REFUSED; see the warnings in the log";

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

        /// <summary>
        /// Apply a plan to the bound entries. Separated from <see cref="Finish"/> on 2026-09-18 so
        /// the harness can drive it with a synthetic plan: the shipped ledger has exactly one rung,
        /// so the reset loop and every failure branch below it had never executed anywhere, and the
        /// first real rebase would have been the first run of that code on somebody's server.
        /// Internal rather than public — the test project compiles this source into its own
        /// assembly, so it can reach this, and nothing outside the mod can.
        /// </summary>
        internal static void Apply(ConfigFile cfg, ConfigLedger.MigrationPlan plan)
        {
            if (cfg == null || plan == null) return;

            foreach (string slot in plan.ResetToDefault)
            {
                ConfigEntryBase entry = Lookup(cfg, slot);
                if (entry == null) { WarnUnknownSlot(slot); continue; }
                entry.BoxedValue = entry.DefaultValue;
            }

            foreach (ConfigLedger.BackfilledSlot b in plan.Backfilled)
            {
                ConfigEntryBase entry = Lookup(cfg, b.Slot);
                if (entry == null) { WarnUnknownSlot(b.Slot); continue; }
                ApplyBackfill(entry, b);
            }
        }

        /// <summary>
        /// The ledger names a key this build does not bind. Only reachable by editing one file and
        /// not the other, and silence would make it permanent: the version stamps and the step
        /// never runs again.
        /// </summary>
        private static void WarnUnknownSlot(string slot)
        {
            _refused++;
            RagnaroksWrath.Log.LogWarning(
                "Config migration wanted to touch " + slot + " but this build binds no such key. " +
                "Nothing was changed for it. This is a bug in ConfigLedger, not in your file.");
        }

        /// <summary>
        /// Write a backfill and CHECK IT LANDED. This replaced a try/catch around
        /// <c>SetSerializedValue</c> on 2026-09-18, which was UNREACHABLE CODE: that method's whole
        /// body is its own try/catch, which logs a BepInEx warning and leaves the value untouched
        /// (read out of libs\BepInEx.dll with ilspycmd). So a value BepInEx would not parse was a
        /// silent no-op followed by a confident version stamp — and the stamp makes it permanent.
        ///
        /// Asking "did it land on what we asked for" rather than "did it move" also catches the
        /// second failure hiding here: <c>ConfigEntry&lt;T&gt;</c>'s setter CLAMPS into the entry's
        /// AcceptableValueRange rather than refusing, so an out-of-range legacy value parses fine,
        /// stores something else, and moves the entry — which a did-it-move test calls success.
        /// </summary>
        private static void ApplyBackfill(ConfigEntryBase entry, ConfigLedger.BackfilledSlot b)
        {
            entry.SetSerializedValue(b.Value);
            string landed = SerializedOrNull(entry);

            if (ValuesAgree(b.Value, landed)) return;

            RagnaroksWrath.Log.LogWarning(
                "Config migration set " + b.Slot + " to '" + (landed ?? "?") + "' rather than the '" + b.Value +
                "' it intended - BepInEx either would not parse that text for this setting's type or clamped it " +
                "into the setting's allowed range. This world may behave differently from before; see the " +
                "changelog for what that key does. This is a bug in ConfigLedger, not in your file.");
        }

        /// <summary>
        /// Whether an entry ended up holding what the ledger asked for. Numbers compare as numbers,
        /// because a round trip legitimately reformats them ("0.50" comes back "0.5") and a
        /// text-only test would cry wolf on every float — including this mod's own
        /// <c>StormDryChance</c> rung. Everything else is ordinal ignore-case, which covers a bool's
        /// "true"/"True". InvariantCulture throughout: this text came off a disk.
        /// </summary>
        private static bool ValuesAgree(string requested, string actual)
        {
            if (requested == null || actual == null) return false;

            double a, c;
            if (double.TryParse(requested.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a) &&
                double.TryParse(actual.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out c))
            {
                return Math.Abs(a - c) <= 1e-6 * Math.Max(1.0, Math.Abs(a));
            }

            return string.Equals(requested.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string SerializedOrNull(ConfigEntryBase entry)
        {
            try { return entry.GetSerializedValue(); }
            catch { return null; }
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
