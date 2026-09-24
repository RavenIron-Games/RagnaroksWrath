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
    /// <see cref="Begin"/> runs before the first `Bind`; <see cref="Finish"/> after the last.
    /// THE ORDER IS THE WHOLE MECHANISM, not a convention: a backfill acts on a key being ABSENT,
    /// and the moment BepInEx binds that key it is present at its shipped default. Snapshot after
    /// binding and every backfill silently becomes a no-op — the migration would run, log happily,
    /// stamp its version, and change nothing.
    ///
    /// TWO FILES since 0.28.0 (layout version 2): the main config and the advanced one beside it.
    /// Both are snapshotted, both are backed up, and the order they are written in is what makes a
    /// failure safe — see <see cref="Finish"/>.
    ///
    /// NOTHING IS WRITTEN UNTIL THE END. BepInEx saves the whole file on every Bind and every value
    /// change while `SaveOnConfigSet` is on (read out of libs\BepInEx.dll: `Bind` ends with
    /// `if (SaveOnConfigSet) Save();`). Begin turns it off for both files and Finish writes each
    /// once, so a boot that dies half-way leaves the files exactly as they were rather than half
    /// migrated — and a migration that moves 146 settings does not rewrite a 40 KB file three
    /// hundred times on the boot thread.
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

        private static ConfigLedger.MigrationPlan _plan;
        private static MigrationState _state = MigrationState.Fresh;
        private static bool _mainSavedOnSet = true;
        private static bool _advancedSavedOnSet = true;

        /// <summary>The last migration's boot line, kept for `wrath status`. Empty when nothing has run.</summary>
        public static string LastSummary { get; private set; } = "";

        /// <summary>
        /// How many steps were REFUSED this boot. A refusal is a bug in our own ledger rather than
        /// in the owner's file: a row naming a key this build does not bind, or a line to drop that
        /// this build still binds. Counted because <see cref="LastSummary"/> is written in
        /// <see cref="Begin"/> from the plan's INTENT, before a single step has run, and
        /// `wrath status` reads it back verbatim. Without this it claims a value was moved that was
        /// never found, and the only contradiction is a warning several hundred log lines earlier.
        /// </summary>
        private static int _refused;

        /// <summary>
        /// Settings found in BOTH the old place and the new one with different values, where the new
        /// place's value was kept. Named in <see cref="LastSummary"/> for the same reason as
        /// <see cref="_refused"/>: the summary is written before this is known.
        /// </summary>
        private static int _keptAtNewPlace;

        /// <summary>Call BEFORE the first Bind on either file. Never throws.</summary>
        public static void Begin(ConfigFile main, ConfigFile advanced)
        {
            _refused = 0;
            _keptAtNewPlace = 0;
            _plan = null;
            _state = MigrationState.Fresh;
            LastSummary = "";

            try
            {
                if (main != null) { _mainSavedOnSet = main.SaveOnConfigSet; main.SaveOnConfigSet = false; }
                if (advanced != null) { _advancedSavedOnSet = advanced.SaveOnConfigSet; advanced.SaveOnConfigSet = false; }

                if (main == null) return;

                string path = main.ConfigFilePath;

                // A fresh install has no file, and that is not a failure — it is the case where
                // every shipped default is exactly right and a migration would be wrong.
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string advancedPath = advanced != null ? advanced.ConfigFilePath : null;
                bool advancedExists = !string.IsNullOrEmpty(advancedPath) && File.Exists(advancedPath);

                var snapshot = ConfigLedger.Combine(
                    ConfigLedger.ParseIni(ReadLinesWithRetry(path)),
                    advancedExists ? ConfigLedger.ParseIni(ReadLinesWithRetry(advancedPath)) : null);

                int fileVersion = ConfigLedger.ReadVersion(snapshot);
                if (fileVersion >= ConfigLedger.CurrentVersion)
                {
                    _state = MigrationState.AlreadyCurrent;
                    return;
                }

                // Judged per file. The main file's backup is the one an owner restores to go back
                // to an older version, so its note must not be spoiled by the advanced file's.
                bool mainBackedUp = Backup(path, fileVersion);
                bool advancedBackedUp = !advancedExists || Backup(advancedPath, fileVersion);

                _plan = ConfigLedger.Plan(snapshot, fileVersion);
                _state = MigrationState.Planned;
                LastSummary = ConfigLedger.Describe(_plan);

                string backupNote = mainBackedUp
                    ? " (your previous config is backed up beside it as .v" + fileVersion.ToString(CultureInfo.InvariantCulture) +
                      ".bak" + (advancedBackedUp ? "" : "; the advanced file's own backup could not be written") +
                      "; restore that file if you ever go back to an older version of this mod)"
                    : " (no backup of the config file could be written; every value is carried to its new place before its old line is removed)";

                // A migration that changes how the world behaves is a warning: the owner did not ask
                // for it, and the whole point of this machinery is that they find out from the log
                // rather than from the sky. One that only moves settings is news.
                if (_plan.ChangesBehaviour)
                    RagnaroksWrath.Log.LogWarning(LastSummary + backupNote);
                else if (!_plan.IsEmpty)
                    RagnaroksWrath.Log.LogInfo(LastSummary + backupNote);
                else
                    RagnaroksWrath.Log.LogInfo(LastSummary);
            }
            catch (Exception ex)
            {
                // Never stop the mod loading — but never pretend either. Since 0.28.0 renamed every
                // section, a file this could not read binds almost nothing: BepInEx matches a stored
                // line only by its exact section and key, so every setting that moved runs at its
                // shipped default until a boot that can read the file. That is said out loud, in
                // the log and in `wrath status`, and Finish writes nothing, so the files stay
                // exactly as they were and the next boot migrates them properly. Found by the
                // 0.28.0 review: this used to say "every value binds exactly as it always did".
                _state = MigrationState.Failed;
                LastSummary = "config migration FAILED this boot: your config file could not be read. " +
                              "If it predates this version's layout, settings whose place moved are at their " +
                              "shipped defaults until a restart. See the error in the log";
                RagnaroksWrath.Log.LogError(
                    "Config migration could not read your config file, so this boot writes nothing to either " +
                    "config file: both stay exactly as they were, unstamped, and the next boot migrates them. " +
                    "Until then, if your file predates this version's layout, every setting whose place moved " +
                    "runs at its shipped default. Restart once whatever is holding the file lets go. Reason: " + ex);
            }
        }

        /// <summary>
        /// Call AFTER the last Bind, with the bound Meta.ConfigVersion entry. Never throws.
        ///
        /// THE ORDER IS THE SAFETY. Values are carried into both files in memory; the ADVANCED file
        /// is saved first; only if that worked are the old lines dropped and the version stamped;
        /// only then is the main file saved. So:
        ///   - the advanced save fails, or a drop does: the MAIN FILE IS NOT SAVED AT ALL, so it
        ///     stays byte-for-byte as it was, every old line in it and unstamped, and the next boot
        ///     carries them again. (It used to be saved anyway, which wrote each main-file setting
        ///     in both its old and its new place. Found by the 0.28.0 review.)
        ///   - the main save fails: it is put back exactly as it was (see <see cref="TrySave"/>),
        ///     still unstamped, and the next boot carries again into an advanced file that already
        ///     holds the same values.
        ///   - the file could not even be read (<see cref="MigrationState.Failed"/>): neither file
        ///     is saved. See Begin.
        /// A carried value can never be lost between the two, because its old line leaves the disk
        /// only in the same write that stamps the version, after its new home is already saved.
        /// </summary>
        public static void Finish(ConfigFile main, ConfigFile advanced, ConfigEntry<int> versionEntry)
        {
            try
            {
                if (_state == MigrationState.Failed) return;   // Begin said why; nothing is written

                if (_plan != null) Apply(main, advanced, _plan);

                bool advancedSaved = TrySave(advanced, ConfigLedger.AdvancedFileName);
                bool dropsLanded = true;
                if (advancedSaved && _plan != null) dropsLanded = Drop(main, advanced, _plan);

                if (!advancedSaved || !dropsLanded)
                {
                    RagnaroksWrath.Log.LogWarning(
                        "Config migration could not finish this boot, so the main config file is left exactly as it " +
                        "was, unstamped, and the next boot tries again. Every setting is in effect as it was.");
                    LastSummary += " - but it could not finish this boot and runs again on the next one; see the log";
                    return;
                }

                if (versionEntry != null)
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
                if (_keptAtNewPlace > 0)
                    LastSummary += " - " + _keptAtNewPlace.ToString(CultureInfo.InvariantCulture) +
                                   " setting(s) already had a different value in the new layout, and that value was kept; see the log";

                if (!TrySave(main, "the config file") && _plan != null && !_plan.IsEmpty)
                    LastSummary += " - but the config file could not be saved, so this runs again on the next boot";
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError(
                    "Config migration could not finish - check the backup beside your config file. Reason: " + ex);
            }
            finally
            {
                Restore(main, advanced);
            }
        }

        /// <summary>
        /// A bind threw between <see cref="Begin"/> and <see cref="Finish"/>. Writes nothing — the
        /// files keep exactly what they held before this boot — and gives BepInEx back its
        /// save-on-change behaviour. Never throws.
        /// </summary>
        public static void Abandon(ConfigFile main, ConfigFile advanced)
        {
            try { Restore(main, advanced); } catch { }
        }

        private static void Restore(ConfigFile main, ConfigFile advanced)
        {
            try
            {
                if (main != null) main.SaveOnConfigSet = _mainSavedOnSet;
                if (advanced != null) advanced.SaveOnConfigSet = _advancedSavedOnSet;
            }
            finally
            {
                _plan = null;
                _state = MigrationState.Fresh;
                _mainSavedOnSet = true;
                _advancedSavedOnSet = true;
            }
        }

        /// <summary>
        /// Save one file, and if the save fails, PUT THE FILE BACK. BepInEx's Save is not atomic: it
        /// opens the file with `new StreamWriter(path, append: false)`, which empties it before the
        /// first line is written (read out of libs\BepInEx.dll), so a save that fails part-way — a
        /// full disk, a transient I/O error — leaves a truncated file, not the untouched one this
        /// class used to promise. The bytes are read just before saving and written back on failure;
        /// a file that did not exist before is removed instead, so a half-written advanced file
        /// cannot be read as settings on the next boot. Found by the 0.28.0 review. Never throws.
        /// </summary>
        private static bool TrySave(ConfigFile file, string name)
        {
            if (file == null) return true;

            string path = file.ConfigFilePath;
            bool known = false, existed = false;
            byte[] before = null;
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    existed = File.Exists(path);
                    if (existed) before = ReadBytesShared(path);
                    known = true;
                }
            }
            catch { }   // unreadable now: the save is still attempted, but a failure cannot be undone

            try
            {
                file.Save();
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError("Could not save " + name + " (" + ex.Message + "). " + PutBack(path, known, existed, before));
                return false;
            }
        }

        private static string PutBack(string path, bool known, bool existed, byte[] before)
        {
            if (!known)
                return "It may be left part-written; if it will not load, restore the .bak copy beside it.";
            try
            {
                if (existed)
                {
                    File.WriteAllBytes(path, before);
                    return "It has been put back exactly as it was.";
                }
                if (File.Exists(path)) File.Delete(path);
                return "The part-written file has been removed.";
            }
            catch (Exception ex)
            {
                return "It could not be put back (" + ex.Message + "); if it will not load, restore the .bak copy beside it.";
            }
        }

        /// <summary>
        /// Every WRITE a plan makes, to the bound entries of both files: resets, backfills,
        /// relocations and carried values. Nothing is removed here — see <see cref="Drop"/>.
        /// Separated from <see cref="Finish"/> on 2026-09-18 so the harness can drive it with a
        /// synthetic plan. Internal rather than public — the test project compiles this source into
        /// its own assembly, so it can reach this, and nothing outside the mod can.
        /// </summary>
        internal static void Apply(ConfigFile main, ConfigFile advanced, ConfigLedger.MigrationPlan plan)
        {
            if (plan == null || (main == null && advanced == null)) return;

            foreach (string slot in plan.ResetToDefault)
            {
                ConfigEntryBase entry = Lookup(main, advanced, slot);
                if (entry == null) { WarnUnknownSlot(slot); continue; }
                entry.BoxedValue = entry.DefaultValue;
            }

            // Carried values BEFORE the steps that write over a decided key. The ledger never
            // carries onto a key another step writes, so the order cannot matter; if it ever did,
            // the deliberate step must be the one left standing, and going last guarantees that.
            foreach (ConfigLedger.MovedSlot m in plan.Moved)
            {
                ConfigEntryBase entry = Lookup(main, advanced, m.To);
                if (entry == null) { WarnUnknownSlot(m.To); continue; }
                ApplyCarry(entry, m);
            }

            foreach (ConfigLedger.BackfilledSlot b in plan.Backfilled)
            {
                ConfigEntryBase entry = Lookup(main, advanced, b.Slot);
                if (entry == null) { WarnUnknownSlot(b.Slot); continue; }
                ApplyBackfill(entry, b);
            }

            // Relocations LAST, and the order matters. A relocation writes over a key the owner
            // set, so it must be the final word on that key — if a backfill and a relocation ever
            // named the same slot, the plan is wrong, but applying relocations second means the
            // file still ends up in the state the boot line just described rather than in a third
            // state nobody announced. (The ledger's one-slot-one-decision guard should make that
            // collision impossible; this is the belt to its braces.)
            foreach (ConfigLedger.BackfilledSlot r in plan.Relocated)
            {
                ConfigEntryBase entry = Lookup(main, advanced, r.Slot);
                if (entry == null) { WarnUnknownSlot(r.Slot); continue; }
                ApplyBackfill(entry, r);
            }
        }

        /// <summary>
        /// Take the plan's dropped lines out of the files, through public `ConfigFile` API alone:
        /// `Bind` under a throwaway default takes the line out of BepInEx's private orphan table (its
        /// own `Bind` removes it from there the moment it binds), and `Remove` then takes the entry
        /// back out, so neither collection carries it into the next `Save`. Never names the private
        /// property — house rule 5's Mono failure applies to anything reached by name. The same shape
        /// FireFront and Undertow use for their retirements.
        ///
        /// A LINE THIS BUILD STILL BINDS IS NEVER DROPPED: Bind would hand back the live entry and
        /// Remove would unbind a setting the mod is using. Refused by name instead.
        /// </summary>
        /// <returns>False when a drop threw — the caller withholds the stamp so the next boot retries.</returns>
        internal static bool Drop(ConfigFile main, ConfigFile advanced, ConfigLedger.MigrationPlan plan)
        {
            if (plan == null) return true;
            bool allLanded = true;

            foreach (ConfigLedger.DroppedSlot d in plan.Dropped)
            {
                if (!ConfigLedger.SplitSlot(d.Slot, out bool isAdvanced, out string section, out string key)) continue;
                ConfigFile file = isAdvanced ? advanced : main;
                if (file == null) continue;

                if (Lookup(main, advanced, d.Slot) != null)
                {
                    _refused++;
                    RagnaroksWrath.Log.LogWarning(
                        "Config migration wanted to remove " + ConfigLedger.DescribeSlot(d.Slot) + ", but this build " +
                        "still reads that setting. Nothing was removed. This is a bug in ConfigLedger, not in your file.");
                    continue;
                }

                try
                {
                    var def = new ConfigDefinition(section, key);
                    file.Bind<string>(def, "");
                    file.Remove(def);
                }
                catch (Exception ex)
                {
                    allLanded = false;
                    RagnaroksWrath.Log.LogError(
                        "Could not remove " + ConfigLedger.DescribeSlot(d.Slot) + " from the config file; the layout " +
                        "version is left unstamped so the next boot tries again. Reason: " + ex.Message);
                }
            }
            return allLanded;
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
                "Config migration wanted to touch " + ConfigLedger.DescribeSlot(slot) + " but this build binds no " +
                "such key. Nothing was changed for it. This is a bug in ConfigLedger, not in your file.");
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
        /// Carry a stored value to the key's new place and check what landed. A difference here is
        /// NOT a behaviour change, and says so: the text is exactly what the old line held, and the
        /// same setting reads it with the same type and range, so BepInEx clamps or ignores it now
        /// precisely as it did under the old section name. It is still named, because it means the
        /// file held something the owner probably did not intend.
        ///
        /// A SETTING IN BOTH PLACES. Only an interrupted earlier run leaves one, and from then on the
        /// new place is what the mod has been reading, so it may hold an edit made since, by hand or
        /// through a config manager. Found by the 0.28.0 review, which reproduced exactly that edit
        /// being silently reverted by the old line. So when the two differ, the NEW place's value is
        /// kept — unless it is only the shipped default, which is what an interrupted run leaves
        /// behind when it could not carry anything, and then the old line is the owner's value, as
        /// it always was. Either way both values are named in the log.
        /// </summary>
        private static void ApplyCarry(ConfigEntryBase entry, ConfigLedger.MovedSlot m)
        {
            if (m.AlreadyThere != null && !ValuesAgree(m.Value, m.AlreadyThere))
            {
                string shipped = DefaultText(entry);
                if (shipped == null || !ValuesAgree(m.AlreadyThere, shipped))
                {
                    // BepInEx bound the entry from the new place's line already; leave it there.
                    _keptAtNewPlace++;
                    RagnaroksWrath.Log.LogWarning(
                        "Config migration found " + ConfigLedger.DescribeSlot(m.To) + " = '" + m.AlreadyThere + "' in the new " +
                        "layout and " + ConfigLedger.DescribeSlot(m.From) + " = '" + m.Value + "' in the old one. It kept '" +
                        m.AlreadyThere + "', the value this mod has been reading since an earlier boot was interrupted; the " +
                        "old line is removed. If '" + m.Value + "' is the one you meant, set it again in the new place.");
                    return;
                }

                RagnaroksWrath.Log.LogInfo(
                    "Config migration found " + ConfigLedger.DescribeSlot(m.To) + " at its shipped default, left there by an " +
                    "interrupted earlier boot, and carried your '" + m.Value + "' over it from " + ConfigLedger.DescribeSlot(m.From) + ".");
            }

            entry.SetSerializedValue(m.Value);
            string landed = SerializedOrNull(entry);

            if (ValuesAgree(m.Value, landed)) return;

            RagnaroksWrath.Log.LogWarning(
                "Config migration carried '" + m.Value + "' from " + ConfigLedger.DescribeSlot(m.From) + " to " +
                ConfigLedger.DescribeSlot(m.To) + ", and the setting holds '" + (landed ?? "?") + "': that text is " +
                "outside the setting's allowed range or not readable as its type. It was treated the same way " +
                "before the move, so nothing about the world changes; correct it in the file if it is not what you meant.");
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

        /// <summary>
        /// The setting's shipped default as BepInEx would write it, or null if it cannot say. Public
        /// BepInEx API both halves (`TomlTypeConverter.ConvertToString` is what `GetSerializedValue`
        /// itself calls), and libs\BepInEx.dll is not publicized, so what compiles is what runs.
        /// </summary>
        private static string DefaultText(ConfigEntryBase entry)
        {
            try { return TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType); }
            catch { return null; }
        }

        /// <summary>The bound entry for a slot in either file, or null when this build does not bind it.</summary>
        private static ConfigEntryBase Lookup(ConfigFile main, ConfigFile advanced, string slot)
        {
            if (!ConfigLedger.SplitSlot(slot, out bool isAdvanced, out string section, out string key)) return null;
            ConfigFile file = isAdvanced ? advanced : main;
            if (file == null) return null;

            var def = new ConfigDefinition(section, key);
            return file.ContainsKey(def) ? file[def] : null;
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
        /// <see cref="ReadLinesShared"/>, tried three times a tenth of a second apart. A file this
        /// cannot read costs the whole boot its settings (see Begin), and the likely cause — a
        /// virus scanner or sync tool holding it without sharing — lets go in milliseconds. Costs
        /// nothing when the first read works, which is every boot but a bad one.
        /// </summary>
        private static List<string> ReadLinesWithRetry(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return ReadLinesShared(path); }
                catch (IOException) when (attempt < 3) { System.Threading.Thread.Sleep(100); }
                catch (UnauthorizedAccessException) when (attempt < 3) { System.Threading.Thread.Sleep(100); }
            }
        }

        private static byte[] ReadBytesShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        /// <summary>
        /// Copies a config beside itself as `.v&lt;fromVersion&gt;.bak`, never overwriting — an
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
                    "Could not back up " + Path.GetFileName(path) + " before migrating (" + ex.Message + "). " +
                    "Proceeding anyway: every value is carried to its new place before its old line is removed.");
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
