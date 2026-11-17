using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Earned titles, keyed by the PLAYER — which per docs/reference/PLAYER-IDENTITY-FACTS.md
    /// means `s_playerID`, the one long of the four look-alikes that survives across sessions
    /// and servers. Session ids and connection ids would fill this file with numbers that match
    /// nobody by next login.
    ///
    /// Same three properties as Persistence, same reasons, smaller stakes: world-scoped file
    /// (titles are world history), atomic writes, fail-safe load. Tab-separated text so an
    /// admin can grant or strip a title in an editor — the same hand-editability the zone
    /// store's plague test proved is a real write path, not a nicety.
    /// </summary>
    public static class TitleStore
    {
        private const int FormatVersion = 1;
        private const string FileStem = "ragnarokswrath_titles";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Dictionary<long, string> _titles = new Dictionary<long, string>(16);

        // Players who earned Winterborn in the CURRENT winter (2026-09-24). Persisted so a restart
        // mid-winter cannot award it a second time that winter; cleared when winter ends. Stored
        // as an optional third column, "W", on the player's title row: a build that predates it
        // reads columns 1 and 2 exactly as before and ignores the rest.
        private static readonly HashSet<long> _winterborn = new HashSet<long>();

        // CHOOSING A TITLE (2026-11-17). Every title a player has earned, in the order earned, and
        // which one they WEAR. Column 2 stays the latest earned title, exactly as before, so a build
        // that predates this reads the file correctly and a rollback simply returns everyone to
        // "latest earned wins". The new data ride as extra tab-separated tokens after the optional
        // "W", which an older build ignores:
        //   earned=Stormrider,Plaguewalker   every title earned (column 2 is always among them)
        //   pick=Plaguewalker                wear this one whatever is earned later
        //   pick=-                           wear none
        // No pick token means "auto": wear the latest earned, the original behaviour.
        private static readonly Dictionary<long, List<string>> _earned = new Dictionary<long, List<string>>(16);
        private static readonly Dictionary<long, string> _pick = new Dictionary<long, string>(16);

        /// <summary>The stored pick that means "show no title".</summary>
        public const string PickNone = "-";

        private static bool _loaded;

        /// <summary>Test seam, like Persistence's: when set, used instead of the world path.</summary>
        internal static string OverridePath = null;

        public static bool IsLoaded => _loaded;
        public static int Count => _titles.Count;

        public static string Get(long playerId)
            => _titles.TryGetValue(playerId, out string t) ? t : null;

        /// <summary>Every player's LATEST EARNED title. What a nameplate shows is <see cref="AllShown"/>.</summary>
        public static IEnumerable<KeyValuePair<long, string>> All() => _titles;

        /// <summary>
        /// Set (or clear with null/empty) a player's latest earned title, and add it to the titles
        /// they have earned. Clearing strips the player entirely: titles, earned list and choice.
        /// Saves immediately: titles change rarely and losing one to a crash reads as the mod
        /// breaking a promise.
        /// </summary>
        public static void Set(long playerId, string title)
        {
            if (playerId == 0) return;   // "Stranger" territory — never record the placeholder id

            if (string.IsNullOrEmpty(title))
            {
                _titles.Remove(playerId);
                _earned.Remove(playerId);
                _pick.Remove(playerId);
            }
            else
            {
                _titles[playerId] = title;
                AddEarned(playerId, title);
            }

            Save();
        }

        private static void AddEarned(long playerId, string title)
        {
            if (string.IsNullOrEmpty(title)) return;
            if (!_earned.TryGetValue(playerId, out List<string> list))
                _earned[playerId] = list = new List<string>(4);
            foreach (string t in list)
                if (string.Equals(t, title, StringComparison.Ordinal)) return;
            list.Add(title);
        }

        /// <summary>Every title this player has earned, oldest first. Empty, never null.</summary>
        public static IList<string> Earned(long playerId)
            => _earned.TryGetValue(playerId, out List<string> list) ? list.AsReadOnly() : (IList<string>)new string[0];

        /// <summary>The player's choice: null for auto (latest earned), <see cref="PickNone"/>, or a title.</summary>
        public static string Pick(long playerId)
            => _pick.TryGetValue(playerId, out string p) ? p : null;

        /// <summary>
        /// The title this player WEARS: what the nameplate shows. Their pick when they made one,
        /// nothing when they chose none, otherwise the latest earned. A pick of a title no longer
        /// earned (an admin edited the file) falls back to the latest, rather than showing a title
        /// the player does not hold.
        /// </summary>
        public static string Shown(long playerId)
        {
            string pick = Pick(playerId);
            if (pick == PickNone) return null;
            if (pick != null && HasEarned(playerId, pick)) return pick;
            return Get(playerId);
        }

        /// <summary>Every player wearing a title, and which. The table replayed at a joining player.</summary>
        public static IEnumerable<KeyValuePair<long, string>> AllShown()
        {
            foreach (long id in _titles.Keys)
            {
                string shown = Shown(id);
                if (!string.IsNullOrEmpty(shown)) yield return new KeyValuePair<long, string>(id, shown);
            }
        }

        private static bool HasEarned(long playerId, string title)
        {
            foreach (string t in Earned(playerId))
                if (string.Equals(t, title, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// A player's own request to wear a title. "auto" (or "latest") wears the latest earned,
        /// "none" (or "off") wears nothing, any other word must name a title they have earned,
        /// matched ignoring case. Returns false and says why when refused; saves when accepted.
        /// The reply always lists what they have, so the command teaches itself.
        /// </summary>
        public static bool TryChoose(long playerId, string choice, out string message)
        {
            message = null;
            if (playerId == 0) { message = "your character is not known to the server yet; try again in a moment."; return false; }

            string text = (choice ?? "").Trim();
            string lower = text.ToLowerInvariant();
            IList<string> earned = Earned(playerId);

            if (lower == "auto" || lower == "latest")
            {
                _pick.Remove(playerId);
                Save();
                message = "you now wear your latest title" + (Get(playerId) != null ? $" ({Get(playerId)})" : "") + ", and each new one as you earn it.";
                return true;
            }
            if (lower == "none" || lower == "off")
            {
                _pick[playerId] = PickNone;
                Save();
                message = "you now wear no title. Titles you earn are still kept.";
                return true;
            }
            if (earned.Count == 0)
            {
                message = "you have not earned a title yet.";
                return false;
            }
            foreach (string t in earned)
            {
                if (string.Equals(t, text, StringComparison.OrdinalIgnoreCase))
                {
                    _pick[playerId] = t;
                    Save();
                    message = $"you now wear {t}. It stays until you choose again (`wrath title auto` to follow new titles).";
                    return true;
                }
            }
            message = $"you have not earned '{text}'. " + Describe(playerId);
            return false;
        }

        /// <summary>What a player has and wears, as one line for the console.</summary>
        public static string Describe(long playerId)
        {
            IList<string> earned = Earned(playerId);
            if (earned.Count == 0) return "You have not earned a title yet.";
            string pick = Pick(playerId);
            string wearing = Shown(playerId);
            string mode = pick == null ? "latest earned" : pick == PickNone ? "none chosen" : "your choice";
            return $"Earned: {string.Join(", ", earned)}. Wearing: {(wearing ?? "nothing")} ({mode}). " +
                   "Choose with `wrath title <name>`, `wrath title auto` or `wrath title none`.";
        }

        /// <summary>True when this player already earned Winterborn this winter.</summary>
        public static bool WinterbornThisWinter(long playerId) => _winterborn.Contains(playerId);

        /// <summary>Record Winterborn for this winter. Saves, like Set.</summary>
        public static void MarkWinterborn(long playerId)
        {
            if (playerId == 0 || !_winterborn.Add(playerId)) return;
            Save();
        }

        /// <summary>Winter is over: everyone may earn Winterborn again next winter. Writes only
        /// when something was marked, so calling it every tick outside winter costs nothing.</summary>
        public static void ClearWinterborn()
        {
            if (_winterborn.Count == 0) return;
            _winterborn.Clear();
            Save();
        }

        /// <summary>
        /// The world is closing: forget it. Clears everything in memory and drops the loaded flag,
        /// so the next world reads its OWN file instead of inheriting this one, and a pure client
        /// stops mistaking itself for the authority. Does NOT save; flush first.
        /// </summary>
        public static void Unload()
        {
            _titles.Clear();
            _winterborn.Clear();
            _earned.Clear();
            _pick.Clear();
            _loaded = false;
        }

        private static void ClearAll()
        {
            _titles.Clear();
            _winterborn.Clear();
            _earned.Clear();
            _pick.Clear();
        }

        public static void Load()
        {
            ClearAll();
            _loaded = false;

            string path = ResolvePath();
            if (path == null) return;

            if (!File.Exists(path)) { _loaded = true; return; }

            try
            {
                string[] lines = File.ReadAllLines(path);
                int good = 0, bad = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
                    if (line.StartsWith("version", StringComparison.Ordinal)) continue;

                    string[] p = line.Split('\t');
                    if (p.Length >= 2
                        && long.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)
                        && id != 0
                        && !string.IsNullOrWhiteSpace(p[1]))
                    {
                        string latest = p[1].Trim();
                        _titles[id] = latest;
                        for (int c = 2; c < p.Length; c++)
                        {
                            string token = p[c].Trim();
                            if (token == "W") _winterborn.Add(id);
                            else if (token.StartsWith("earned=", StringComparison.Ordinal))
                            {
                                foreach (string t in token.Substring(7).Split(','))
                                    if (!string.IsNullOrWhiteSpace(t)) AddEarned(id, t.Trim());
                            }
                            else if (token.StartsWith("pick=", StringComparison.Ordinal))
                            {
                                string pick = token.Substring(5).Trim();
                                if (pick.Length > 0) _pick[id] = pick;
                            }
                        }
                        // The latest title is always earned. This is also the whole migration for a
                        // file written before the earned list existed: what you wore is what you have.
                        AddEarned(id, latest);
                        good++;
                    }
                    else bad++;
                }

                _loaded = true;

                if (good == 0 && bad > 0)
                {
                    // Same rule as the zone store: content that parsed as nothing is a corrupt
                    // file, not an empty one. Quarantine the evidence, start clean.
                    RagnaroksWrath.Log.LogError(
                        $"TitleStore: {Path.GetFileName(path)} is unreadable — all {bad} content " +
                        "line(s) failed to parse. Titles reset; file kept for inspection.");
                    TryQuarantine(path);
                    ClearAll();
                    return;
                }

                if (_titles.Count > 0)
                    RagnaroksWrath.Log.LogInfo($"TitleStore: loaded {_titles.Count} title(s).");
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError(
                    $"TitleStore: could not read {Path.GetFileName(path)} ({ex.Message}). " +
                    "Titles reset; file kept for inspection.");
                TryQuarantine(path);
                ClearAll();
                _loaded = true;
            }
        }

        private static void Save()
        {
            if (!_loaded) return;

            string path = ResolvePath();
            if (path == null) return;

            string tmp = path + ".tmp";
            try
            {
                var sb = new StringBuilder(_titles.Count * 32 + 64);
                sb.Append("version\t").Append(FormatVersion).Append('\n');
                sb.Append("# playerID\tlatest title\t[W = earned Winterborn this winter]\t[earned=every title earned]\t[pick=title worn, - for none]\n");
                foreach (KeyValuePair<long, string> kv in _titles)
                {
                    sb.Append(kv.Key.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(kv.Value);
                    // Rides on the title row. Awarding Winterborn always writes a row, so the
                    // mark only goes unsaved if an admin clears the player's title. Kept FIRST
                    // after the title, where builds before the earned list look for it.
                    if (_winterborn.Contains(kv.Key)) sb.Append("\tW");
                    IList<string> earned = Earned(kv.Key);
                    if (earned.Count > 0) sb.Append("\tearned=").Append(string.Join(",", earned));
                    string pick = Pick(kv.Key);
                    if (pick != null) sb.Append("\tpick=").Append(pick);
                    sb.Append('\n');
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, sb.ToString(), Utf8NoBom);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError($"TitleStore: save failed ({ex.Message}). Titles kept in memory.");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        private static string ResolvePath()
        {
            if (!string.IsNullOrEmpty(OverridePath)) return OverridePath;
            return Persistence.ResolveSiblingPath($"{FileStem}");
        }

        private static void TryQuarantine(string path)
        {
            try
            {
                string dead = path + ".corrupt";
                if (File.Exists(dead)) File.Delete(dead);
                File.Move(path, dead);
            }
            catch { }
        }
    }
}
