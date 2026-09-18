using System;
using System.Collections.Generic;
using System.Globalization;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// The config migration's DECISIONS, pure and off-game. Ported 2026-09-18 from Valkyrie's
    /// Cargo, which took the shape from Wu'barrk's WingsoftheValkyrie (the family's, alongside
    /// TortalPortal and Fatty): a stamped layout version, a table keyed by the version it
    /// produces, and the rule that a stored value still equal to an OLD SHIPPED DEFAULT belongs to
    /// the mod and may move, while anything else is an admin's and is kept untouched.
    ///
    /// WHY A MIGRATION AT ALL, when BepInEx already merges new keys into an existing file. Because
    /// merging is not the problem. BepInEx appends a new key at its SHIPPED default, and a shipped
    /// default is chosen for a fresh install — it is a statement about what a new world should
    /// feel like, not a promise about what an existing one already feels like. When those differ,
    /// an owner who changed nothing gets a world that behaves differently, with no error and
    /// nothing in the log. That is this codebase's named enemy, arriving through the front door.
    ///
    /// So this file distinguishes two shapes:
    ///
    ///   REBASE — the key exists in the file and still holds an old shipped default. The value was
    ///   never the admin's, so it moves to the new default. A value that is anything else is the
    ///   admin's work and is kept and named in the log. (None yet; the machinery is here because
    ///   the ladder should exist before the rung that needs it.)
    ///
    ///   BACKFILL — the key is ABSENT, and the shipped default would change how an existing world
    ///   behaves. The file gets a stated legacy value instead, chosen to reproduce exactly what
    ///   that world did before the update. A fresh install never sees this and gets the shipped
    ///   default, which is the point: new worlds get the new feeling, old worlds keep theirs until
    ///   somebody decides otherwise.
    ///
    /// Version numbers (backfilled — nothing before this cut ever stamped one):
    ///   0 = any unstamped file: every config this mod has ever written, up to and including
    ///       0.27.0, whatever it carries.
    ///   1 = the two-sky storm. Current.
    /// </summary>
    public static class ConfigLedger
    {
        public const string MetaSection = "Meta";
        public const string VersionKey = "ConfigVersion";
        public const int CurrentVersion = 1;

        /// <summary>One slot's every old shipped default. A stored value equal to ANY of them is the mod's, not the admin's.</summary>
        public sealed class Rebase
        {
            public string Section;
            public string Key;
            public string[] OldDefaults;
            /// <summary>Used only in the boot line, so an owner reads why their value moved.</summary>
            public string Because;
        }

        /// <summary>
        /// A key absent from an existing file, given a value that preserves how that world already
        /// behaved rather than the shipped default meant for new ones.
        /// </summary>
        public sealed class Backfill
        {
            public string Section;
            public string Key;
            /// <summary>The legacy value, serialised exactly as the config file spells it.</summary>
            public string LegacyValue;
            public string Because;
        }

        /// <summary>Keyed by the version the step produces: <c>Rebases[n]</c> takes a file at n-1 up to n.</summary>
        private static readonly Dictionary<int, Rebase[]> Rebases = new Dictionary<int, Rebase[]>();

        /// <summary>
        /// Version 1, and the only rung so far. Before it, every Devastating Storm wore the single
        /// `StormForcedEnvironment` sky; after it, each storm rolls wet or dry and the dry one can
        /// start fires that the wet one cannot. `StormDryChance` ships at 0.5 because that is the
        /// feature — but on a server that already exists, 0.5 means lightning begins striking
        /// where it never did (a ThunderStorm owner) or stops striking half the time where it
        /// always did (an Eikthyr owner). BOTH directions are a silent change to a live world, so
        /// an existing file gets 0: every storm keeps using the one sky it already used, whichever
        /// that was. One rule, provably behaviour-preserving for every prior configuration.
        /// </summary>
        private static readonly Dictionary<int, Backfill[]> Backfills = new Dictionary<int, Backfill[]>
        {
            { 1, new[]
                {
                    new Backfill
                    {
                        Section = "6 - Weather",
                        Key = "StormDryChance",
                        LegacyValue = "0",
                        Because = "your storms keep the single sky they already had; set it to 0.5 for the new wet/dry roll",
                    },
                }
            },
        };

        /// <summary>A slot whose stored value was NOT an old default — real admin work, kept and named.</summary>
        public struct KeptSlot
        {
            public string Slot;
            public string Value;
        }

        /// <summary>A key this migration writes because it was absent and the shipped default would have changed behaviour.</summary>
        public struct BackfilledSlot
        {
            public string Slot;
            public string Value;
            public string Because;
        }

        /// <summary>The whole plan a snapshot produces. Nothing to do plans an empty one.</summary>
        public sealed class MigrationPlan
        {
            public int FromVersion;
            public int ToVersion;
            public List<string> ResetToDefault = new List<string>();
            public List<KeptSlot> Kept = new List<KeptSlot>();
            public List<BackfilledSlot> Backfilled = new List<BackfilledSlot>();

            /// <summary>True when the plan would change nothing on disk beyond the version stamp.</summary>
            public bool IsEmpty => ResetToDefault.Count == 0 && Kept.Count == 0 && Backfilled.Count == 0;
        }

        public static string Slot(string section, string key) => section + "::" + key;

        /// <summary>
        /// BepInEx config files are plain INI: `[Section]` headers, `#` comments, blank lines, and
        /// `Key = value` where the value may itself contain `=`. Keyed "Section::Key", ordinal
        /// ignore-case; the last duplicate wins. Values trimmed. NEVER THROWS — a config this
        /// cannot read must not stop the mod loading, it must look like a fresh install.
        /// </summary>
        public static Dictionary<string, string> ParseIni(IEnumerable<string> lines)
        {
            var into = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (lines == null) return into;

            string section = "";
            foreach (string rawLine in lines)
            {
                if (rawLine == null) continue;
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;

                into[Slot(section, key)] = value;
            }
            return into;
        }

        /// <summary>The stamped `Meta.ConfigVersion`, or 0 when absent or unparseable — which is exactly what a pre-migration file is.</summary>
        public static int ReadVersion(Dictionary<string, string> snapshot)
        {
            string raw;
            if (snapshot != null && snapshot.TryGetValue(Slot(MetaSection, VersionKey), out raw) &&
                int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
            {
                return version;
            }
            return 0;
        }

        /// <summary>
        /// What migrating <paramref name="snapshot"/> from <paramref name="fileVersion"/> to
        /// <see cref="CurrentVersion"/> does. An empty snapshot (a fresh install, or a file that
        /// could not be read) plans nothing, and so does a file already at or beyond the current
        /// version. Steps apply in version order. NEVER THROWS.
        /// </summary>
        public static MigrationPlan Plan(Dictionary<string, string> snapshot, int fileVersion)
        {
            var plan = new MigrationPlan { FromVersion = fileVersion, ToVersion = CurrentVersion };
            if (snapshot == null || snapshot.Count == 0) return plan;
            if (fileVersion >= CurrentVersion) return plan;

            for (int version = fileVersion + 1; version <= CurrentVersion; version++)
            {
                Rebase[] rebases;
                if (Rebases.TryGetValue(version, out rebases) && rebases != null)
                {
                    foreach (Rebase r in rebases)
                    {
                        string slot = Slot(r.Section, r.Key);
                        string stored;
                        if (!snapshot.TryGetValue(slot, out stored)) continue;

                        bool wasOldDefault = false;
                        if (r.OldDefaults != null)
                        {
                            foreach (string oldDefault in r.OldDefaults)
                            {
                                if (string.Equals(stored.Trim(), oldDefault, StringComparison.Ordinal))
                                {
                                    wasOldDefault = true;
                                    break;
                                }
                            }
                        }

                        if (wasOldDefault) plan.ResetToDefault.Add(slot);
                        else plan.Kept.Add(new KeptSlot { Slot = slot, Value = stored });
                    }
                }

                Backfill[] backfills;
                if (Backfills.TryGetValue(version, out backfills) && backfills != null)
                {
                    foreach (Backfill b in backfills)
                    {
                        string slot = Slot(b.Section, b.Key);

                        // ABSENT ONLY, and this is the whole safety of a backfill. A key already
                        // in the file carries either the admin's choice or a value an earlier
                        // partial run wrote, and overwriting either would make this migration the
                        // thing that loses settings. The snapshot is taken BEFORE any bind for
                        // exactly this reason: once BepInEx has bound the key it is present at its
                        // shipped default and absence can no longer be observed.
                        if (snapshot.ContainsKey(slot)) continue;

                        plan.Backfilled.Add(new BackfilledSlot
                        {
                            Slot = slot,
                            Value = b.LegacyValue,
                            Because = b.Because,
                        });
                    }
                }
            }
            return plan;
        }

        /// <summary>
        /// One boot line an owner can act on. NEVER THROWS.
        /// </summary>
        public static string Describe(MigrationPlan plan)
        {
            if (plan == null) return "config: nothing to migrate";

            string head = "config: version " + plan.FromVersion.ToString(CultureInfo.InvariantCulture) +
                          " -> " + plan.ToVersion.ToString(CultureInfo.InvariantCulture) + ": ";

            if (plan.IsEmpty) return head + "nothing to migrate";

            var parts = new List<string>();

            foreach (BackfilledSlot b in plan.Backfilled)
                parts.Add(b.Slot + " set to " + b.Value + " (" + b.Because + ")");

            if (plan.ResetToDefault.Count > 0)
                parts.Add(plan.ResetToDefault.Count.ToString(CultureInfo.InvariantCulture) +
                          " value(s) moved to their new defaults: " + string.Join(", ", plan.ResetToDefault.ToArray()));

            if (plan.Kept.Count > 0)
            {
                var names = new List<string>();
                foreach (KeptSlot k in plan.Kept) names.Add(k.Slot);
                parts.Add(names.Count.ToString(CultureInfo.InvariantCulture) +
                          " kept as yours: " + string.Join(", ", names.ToArray()));
            }

            return head + string.Join("; ", parts.ToArray());
        }
    }
}
