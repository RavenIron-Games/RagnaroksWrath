// Hand-written stand-ins for the handful of game / BepInEx types the tested source
// mentions in its signatures. Deliberately minimal: the stub surface is almost always
// smaller than it looks. Nothing here needs to behave like Valheim — it only needs to
// compile and let the real logic run.

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// ---- UnityEngine ------------------------------------------------------------------
// Must live in the real namespace: the shipping source has `using UnityEngine;`, and the
// point of this harness is to compile that source unmodified.

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public override string ToString() => $"({x},{y},{z})";
    }
}

// ---- Valheim ----------------------------------------------------------------------
// These are global-namespace types in the real game, so the stubs are too.

public struct Vector2i
{
    public int x, y;
    public Vector2i(int x, int y) { this.x = x; this.y = y; }
    public override string ToString() => $"({x},{y})";
}

// Valheim 1.0.7's zone id, and it lives in assembly_utils rather than assembly_valheim.
// SHORT-backed on purpose here, exactly as the game has it: a stub that widened these to int
// would hide a narrowing bug in ZoneKey instead of catching one.
public struct Vector2s
{
    public short x, y;
    public Vector2s(short x, short y) { this.x = x; this.y = y; }
    public Vector2s(int x, int y) { this.x = (short)x; this.y = (short)y; }
    public Vector2s(Vector2i v) { x = (short)v.x; y = (short)v.y; }
    public Vector2i ToVector2i() => new Vector2i(x, y);
    public override string ToString() => $"{x},{y}";
}

public static class ZoneSystem
{
    public const float ZoneSize = 64f;

    // 1.0.7 returns Vector2s here and takes Vector2s below. Both changed together.
    public static Vector2s GetZone(Vector3 point)
        => new Vector2s(
            (int)Math.Floor((point.x + ZoneSize / 2f) / ZoneSize),
            (int)Math.Floor((point.z + ZoneSize / 2f) / ZoneSize));

    public static Vector3 GetZonePos(Vector2s id)
        => new Vector3(id.x * ZoneSize, 0f, id.y * ZoneSize);
}

// Mirrors assembly_utils. Persistence passes Local explicitly: Auto/Cloud resolve to a
// RELATIVE cloud path, which is not a filesystem location.
public static class FileHelpers
{
    // 1.0.7 made this a [Flags] enum and RENUMBERED it: Local moved from 1 to 2. The numbers
    // are mirrored faithfully because that is the trap — code that passes the symbol is fine,
    // code that ever stored the number is not.
    [Flags]
    public enum FileSource { Auto = 1, Local = 2, Cloud = 4, Legacy = 8 }
}

public class World
{
    // long, not ulong — matches the real assembly. Persistence casts on the way out.
    public long m_uid;

    // Tests always set Persistence.OverrideDirectory, so this is never the path taken.
}

// 1.0.7 deleted World.GetWorldSavePath and rehoused it here, same body and same
// "/worlds_local" suffix for Local. Tests always set Persistence.OverrideDirectory, so this
// is never the path taken; it exists so the shipping source compiles.
public static class SaveSystem
{
    public static string GetWorldsSaveRootPath(FileHelpers.FileSource fileSource) => System.IO.Path.GetTempPath();
}

// ZNet is an instance type in the game with a static `instance`. Persistence uses the static
// helper; BiomeStateSystem goes through `instance`, which stays null here so the live
// contact path is never taken and the drift math is tested directly instead.
public class ZNet
{
    public static ZNet instance => null;

    // Null here means "not the host". Tests override the uid, so this stays null.
    public static World GetWorldIfIsHost() => null;

    public List<ZDO> GetAllCharacterZDOS() => new List<ZDO>();
}

public class ZDO
{
    public Vector3 Position;

    public bool IsValid() => true;
    public Vector3 GetPosition() => Position;
}

// ---- HarmonyLib -------------------------------------------------------------------
// Only the members the tested source mentions. Tests never take the reflection path
// (Persistence.OverrideWorldUid short-circuits it), but it still has to compile.

namespace HarmonyLib
{
    public static class AccessTools
    {
        public static TField FieldRefAccess<TObject, TField>(TObject instance, string fieldName)
            => default;
    }
}

// ---- the plugin's logger ----------------------------------------------------------
// The real RagnaroksWrath class derives from BepInEx's BaseUnityPlugin, which cannot run
// off-game. This stand-in supplies only the static Log surface the tested files use, and
// routes it to the console so a failing test shows why.

namespace RavenIron.RagnaroksWrath
{
    /// <summary>
    /// Routes the plugin's log to the console so a failing test shows the boot line it would have
    /// written — and RECORDS it, because several of the migration's guarantees ARE the log line. A
    /// backfill that BepInEx clamped still changes the entry, so "did the value move" cannot tell
    /// it from one that landed correctly; the only difference the outside world can see is that the
    /// mod said so. An assertion about a silent failure has to be able to hear the noise.
    /// </summary>
    public class TestLog
    {
        public readonly List<string> Messages = new List<string>();

        public void LogInfo(object o)    { Record("info", o); }
        public void LogWarning(object o) { Record("warn", o); }
        public void LogError(object o)   { Record("error", o); }

        private void Record(string level, object o)
        {
            string line = $"[{level}] {o}";
            Messages.Add(line);
            Console.WriteLine($"      {line}");
        }

        public void Clear() => Messages.Clear();

        public bool Said(string fragment)
            => Messages.Exists(m => m.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static class RagnaroksWrath
    {
        public static readonly TestLog Log = new TestLog();
    }
}

// ---- BepInEx.Configuration --------------------------------------------------------

namespace BepInEx.Configuration
{
    public class AcceptableValueRange<T>
    {
        public readonly T MinValue, MaxValue;
        public AcceptableValueRange(T min, T max) { MinValue = min; MaxValue = max; }
    }

    public class ConfigDescription
    {
        public readonly string Description;
        public readonly object AcceptableValues;

        public ConfigDescription(string description, object acceptableValues = null)
        {
            Description = description;
            AcceptableValues = acceptableValues;
        }
    }

    /// <summary>
    /// Enough of BepInEx's ConfigDefinition for the migration to address a key. ORDINAL AND
    /// CASE-SENSITIVE, corrected 2026-09-18.
    ///
    /// This said "ordinal ignore-case on both halves, matching the real one" and implemented that.
    /// It does not match: BepInEx's `Equals` is `string.Equals(Key, other.Key) &&
    /// string.Equals(Section, other.Section)` — the two-argument overload — over a case-sensitive
    /// `GetHashCode` (read out of libs\BepInEx.dll with ilspycmd). A mis-cased ledger row would
    /// have found its entry here and missed it in game, so every apply assertion was passing for a
    /// reason that does not hold on a real server.
    /// </summary>
    public class ConfigDefinition
    {
        public readonly string Section;
        public readonly string Key;

        public ConfigDefinition(string section, string key) { Section = section; Key = key; }

        public override bool Equals(object obj)
            => obj is ConfigDefinition d
               && string.Equals(d.Section, Section, StringComparison.Ordinal)
               && string.Equals(d.Key, Key, StringComparison.Ordinal);

        public override int GetHashCode()
            => ((Key ?? "").GetHashCode() * 397) ^ (Section ?? "").GetHashCode();
    }

    public abstract class ConfigEntryBase
    {
        public abstract object BoxedValue { get; set; }
        public abstract object DefaultValue { get; }
        public abstract Type SettingType { get; }
        public abstract string GetSerializedValue();
        public abstract void SetSerializedValue(string value);
    }

    /// <summary>
    /// How BepInEx spells a value in a config file. The real one is a public static class, and the
    /// real ConfigEntryBase.GetSerializedValue is exactly ConvertToString(BoxedValue, SettingType),
    /// so the stub's entry routes through this too and the two cannot disagree.
    /// </summary>
    public static class TomlTypeConverter
    {
        public static string ConvertToString(object value, Type valueType)
            => value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public class ConfigEntry<T> : ConfigEntryBase
    {
        private readonly T _default;
        private readonly ConfigDescription _description;
        private T _value;

        /// <summary>
        /// CLAMPS on the way in, because the real one does: BepInEx's setter runs
        /// `value = ClampValue(value)` against the entry's AcceptableValueRange, and
        /// AcceptableValueRange.Clamp returns MinValue or MaxValue rather than refusing. Without
        /// this, a test cannot tell a backfill that landed from one that landed CLAMPED — and this
        /// mod has a LIVE backfill rung, so that distinction is not hypothetical here.
        /// </summary>
        public T Value
        {
            get => _value;
            set => _value = Clamp(value);
        }

        public ConfigEntry(T defaultValue, ConfigDescription description = null)
        {
            _default = defaultValue;
            _description = description;
            _value = defaultValue;
        }

        private T Clamp(T candidate)
        {
            if (_description?.AcceptableValues is AcceptableValueRange<T> range && candidate is IComparable<T> c)
            {
                if (c.CompareTo(range.MinValue) < 0) return range.MinValue;
                if (c.CompareTo(range.MaxValue) > 0) return range.MaxValue;
            }
            return candidate;
        }

        /// <summary>What BepInEx prints above the setting. The real ConfigEntryBase exposes the same.</summary>
        public ConfigDescription Description => _description;

        public override object BoxedValue { get => Value; set => Value = (T)value; }
        public override object DefaultValue => _default;
        public override Type SettingType => typeof(T);

        /// <summary>
        /// Invariant culture, and lower-case for a bool, because that is how BepInEx's own
        /// TomlTypeConverter spells a config value. A comma-decimal machine writing "0,5" where the
        /// shipping mod writes "0.5" is exactly the locale bug this repo's working agreement warns
        /// about for anything crossing a file.
        /// </summary>
        public override string GetSerializedValue() => TomlTypeConverter.ConvertToString(Value, typeof(T));

        /// <summary>
        /// SWALLOWS a value it cannot parse and leaves the entry untouched — not laziness, but what
        /// the real one does: BepInEx's ConfigEntryBase.SetSerializedValue catches every exception
        /// itself, logs its own warning and returns (read out of libs\BepInEx.dll with ilspycmd,
        /// 2026-09-18). That is why the try/catch this file's migration used to wrap around it was
        /// unreachable code, and why ApplyBackfill now reads the value back instead. A stub that
        /// threw here would let that check pass for the wrong reason.
        /// </summary>
        public override void SetSerializedValue(string value)
        {
            try { Value = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture); }
            catch { }
        }
    }

    /// <summary>
    /// A ConfigFile that behaves like BepInEx's where the config migration can tell the difference
    /// (read out of libs\BepInEx.dll with ilspycmd, 2026-09-24):
    ///   - the file's lines are loaded ONCE, and every line no Bind has claimed is an ORPHAN that
    ///     Save writes back, exactly as BepInEx keeps them in its private OrphanedEntries;
    ///   - Bind claims an orphan by exact section and key and applies its text;
    ///   - Remove takes a bound entry out, and a removed entry is gone from the next Save;
    ///   - Save writes entries then orphans, grouped by section, sections sorted by name.
    /// Save only touches the disk when <see cref="WriteOnSave"/> is set, so older tests that keep
    /// their own fixture file on disk are unaffected; a multi-boot test sets it and reads back what
    /// the "mod" wrote. <see cref="ThrowOnSave"/> makes a save fail the way a locked file does.
    ///
    /// The original stub returned a fresh entry per Bind and stored nothing, which is fine for
    /// counting binds and useless for proving a migration reached the key it named.
    /// </summary>
    public class ConfigFile
    {
        private readonly List<string> _bound = new List<string>();
        private readonly Dictionary<ConfigDefinition, ConfigEntryBase> _entries =
            new Dictionary<ConfigDefinition, ConfigEntryBase>();

        public int BoundCount => _bound.Count;
        public int SaveCount { get; private set; }

        /// <summary>Where the pre-bind snapshot is read from. Settable so a test can point it at a real temp file.</summary>
        public string ConfigFilePath { get; set; } = "";

        /// <summary>BepInEx's own switch. The stub never saves by itself, so this is only recorded, for tests to assert on.</summary>
        public bool SaveOnConfigSet { get; set; } = true;

        /// <summary>When set, Save writes the file to <see cref="ConfigFilePath"/> the way BepInEx lays it out.</summary>
        public bool WriteOnSave { get; set; }

        /// <summary>When set, Save throws before writing, like a file another process has locked.</summary>
        public bool ThrowOnSave { get; set; }

        /// <summary>
        /// When set, Save fails the OTHER way the real one can: BepInEx opens the file with
        /// `new StreamWriter(path, append: false)`, which empties it on open, so a disk that fills
        /// part-way leaves a truncated file behind. This writes the first half and throws.
        /// </summary>
        public bool ThrowMidSave { get; set; }

        /// <summary>What the last successful Save held, "Section::Key" to text: bound entries and orphans alike.</summary>
        public Dictionary<string, string> LastSaved { get; private set; }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue,
                                      ConfigDescription description = null)
            => BindCore(new ConfigDefinition(section, key), defaultValue, description);

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
            => BindCore(new ConfigDefinition(section, key), defaultValue, new ConfigDescription(description));

        public ConfigEntry<T> Bind<T>(ConfigDefinition definition, T defaultValue, ConfigDescription description = null)
            => BindCore(definition, defaultValue, description);

        /// <summary>
        /// Lines in the file that no Bind has claimed, loaded once on first use — because the real
        /// BepInEx applies a stored value over the shipped default, and a stub that did not would
        /// make an admin's setting invisible to every test.
        ///
        /// Parsed with the shipping ConfigLedger.ParseIni rather than a second parser, per the
        /// working agreement's "a harness that duplicates logic proves nothing and drifts". That
        /// parser is pinned separately by its own tests against hand-written expectations, so it
        /// cannot quietly agree with itself here.
        /// </summary>
        private Dictionary<string, string> _orphans;

        private void EnsureLoaded()
        {
            if (_orphans != null) return;
            // Ordinal, matching both the shipping ParseIni and BepInEx's own key comparison.
            _orphans = !string.IsNullOrEmpty(ConfigFilePath) && System.IO.File.Exists(ConfigFilePath)
                ? RavenIron.RagnaroksWrath.Core.ConfigLedger.ParseIni(System.IO.File.ReadAllLines(ConfigFilePath))
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private ConfigEntry<T> BindCore<T>(ConfigDefinition def, T defaultValue, ConfigDescription description)
        {
            _bound.Add($"{def.Section}/{def.Key}");
            EnsureLoaded();

            if (_entries.TryGetValue(def, out ConfigEntryBase existing)) return (ConfigEntry<T>)existing;

            var entry = new ConfigEntry<T>(defaultValue, description);
            string slot = def.Section + "::" + def.Key;
            if (_orphans.TryGetValue(slot, out string raw))
            {
                // A value the file cannot express as T is what BepInEx itself treats as absent.
                try { entry.SetSerializedValue(raw); } catch { }
                _orphans.Remove(slot);
            }

            _entries[def] = entry;
            return entry;
        }

        public bool ContainsKey(ConfigDefinition def) => _entries.ContainsKey(def);
        public ConfigEntryBase this[ConfigDefinition def] => _entries[def];
        public bool Remove(ConfigDefinition def) => _entries.Remove(def);

        /// <summary>Every bound definition, as the real ConfigFile exposes through its dictionary interface.</summary>
        public ICollection<ConfigDefinition> Keys => _entries.Keys;

        /// <summary>Whether a line is still an unclaimed orphan — what BepInEx would write back without anyone reading it.</summary>
        public bool HasOrphan(string section, string key)
        {
            EnsureLoaded();
            return _orphans.ContainsKey(section + "::" + key);
        }

        public void Save()
        {
            if (ThrowOnSave) throw new System.IO.IOException("the stub's file is locked");
            SaveCount++;
            EnsureLoaded();

            var all = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in _entries) all[kv.Key.Section + "::" + kv.Key.Key] = kv.Value.GetSerializedValue();
            foreach (var kv in _orphans) all[kv.Key] = kv.Value;

            if (!WriteOnSave || string.IsNullOrEmpty(ConfigFilePath))
            {
                if (ThrowMidSave) throw new System.IO.IOException("the stub's disk filled part-way through the save");
                LastSaved = all;
                return;
            }

            var bySection = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in all)
            {
                int i = kv.Key.IndexOf("::", StringComparison.Ordinal);
                string section = kv.Key.Substring(0, i);
                if (!bySection.TryGetValue(section, out var lines)) bySection[section] = lines = new List<string>();
                lines.Add(kv.Key.Substring(i + 2) + " = " + kv.Value);
            }
            var file = new List<string>();
            foreach (var s in bySection)
            {
                file.Add("[" + s.Key + "]");
                file.AddRange(s.Value);
                file.Add("");
            }
            if (ThrowMidSave)
            {
                System.IO.File.WriteAllLines(ConfigFilePath, file.GetRange(0, file.Count / 2));
                throw new System.IO.IOException("the stub's disk filled part-way through the save");
            }
            LastSaved = all;
            System.IO.File.WriteAllLines(ConfigFilePath, file);
        }
    }
}
