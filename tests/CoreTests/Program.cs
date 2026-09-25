using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using BepInEx.Configuration;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;

namespace RagnaroksWrath.Tests
{
    /// <summary>
    /// Off-game harness for the pure-logic core. No test framework by design — a console
    /// program that returns a nonzero exit code is enough, and adds no dependency to keep
    /// current.
    ///
    /// What this is actually for: ZoneClock's credit math fails SILENTLY. A wrong cap or a
    /// mishandled negative delta does not throw, it just makes the world drift oddly weeks
    /// later on someone else's server. That is precisely the class of bug worth catching
    /// without launching the game.
    /// </summary>
    public static class Program
    {
        private static int _passed;
        private static int _failed;

        public static int Main()
        {
            Console.WriteLine("Ragnarok's Wrath — core tests\n");

            // Bind config first: ZoneClock reads MaxCreditSeconds on every call.
            ModConfig.Bind(new ConfigFile(), new ConfigFile());

            ZoneKeyTests();
            ZoneClockTests();
            ZoneStateTests();
            PersistenceTests();
            BiomeStateTests();
            StormAreaTests();
            StormLookTests();
            ConfigLedgerTests();
            WindStateTests();
            FireScorchTests();
            PlagueTests();
            WorldStateTests();
            EcologyTests();
            TitleTests();
            FogTests();
            ExposureTests();
            HealthStoreTests();
            ConsequenceTests();
            RivalryTests();
            ContestTests();
            NemesisTests();
            RelicTests();
            WrathAdminTests();
            FarmingGrowthTests();
            PlagueGenesisTests();
            LightningStrikeTests();
            WorldResetTests();

            Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
            return _failed == 0 ? 0 : 1;
        }

        // ---- ZoneKey ----------------------------------------------------------------

        private static void ZoneKeyTests()
        {
            Console.WriteLine("ZoneKey");

            Check("equality is by value",
                new ZoneKey(3, -7) == new ZoneKey(3, -7));

            Check("inequality distinguishes swapped coords",
                new ZoneKey(3, -7) != new ZoneKey(-7, 3));

            Check("equal keys hash equally",
                new ZoneKey(12, 34).GetHashCode() == new ZoneKey(12, 34).GetHashCode());

            // Not a correctness requirement, but a collision here would quietly degrade every
            // per-zone dictionary in the mod, so it is worth knowing if it ever changes.
            Check("swapped coords do not collide",
                new ZoneKey(12, 34).GetHashCode() != new ZoneKey(34, 12).GetHashCode());

            Check("negative coords round-trip through TryParse",
                ZoneKey.TryParse(new ZoneKey(-15, -200).ToString(), out ZoneKey parsed)
                && parsed == new ZoneKey(-15, -200));

            Check("zero round-trips",
                ZoneKey.TryParse(new ZoneKey(0, 0).ToString(), out ZoneKey zero)
                && zero == new ZoneKey(0, 0));

            Check("garbage is rejected rather than silently parsed",
                !ZoneKey.TryParse("not-a-key", out _)
                && !ZoneKey.TryParse("", out _)
                && !ZoneKey.TryParse(null, out _));

            // ---- Valheim 1.0.7: zone ids became the short-backed Vector2s ----------------
            // ZoneKey keeps int fields so the on-disk format is untouched, which means every
            // trip out to the game and back now narrows. These pin that the narrowing is a
            // no-op across the range a real world can produce.

            Check("a zone key survives a round-trip through Vector2s",
                new ZoneKey(new ZoneKey(-15, -200).ToVector2s()) == new ZoneKey(-15, -200));

            Check("zero survives the Vector2s round-trip",
                new ZoneKey(new ZoneKey(0, 0).ToVector2s()) == new ZoneKey(0, 0));

            // A full world is roughly ±160 zones. This is an order of magnitude past the edge
            // of any real map and still nowhere near a short, so the conversion is lossless
            // for anything the game can hand us.
            Check("coords far beyond a real world still round-trip losslessly",
                new ZoneKey(new ZoneKey(-2000, 2000).ToVector2s()) == new ZoneKey(-2000, 2000));

            // The failure this guards against: if ZoneKey ever stored shorts, or the game
            // widened Vector2s, a key that parsed one way and converted another would split
            // a zone's drift history in two without any error.
            Check("Vector2s and Vector2i views of a key agree",
                new ZoneKey(-15, -200).ToVector2s().ToVector2i().x == new ZoneKey(-15, -200).ToVector2i().x
                && new ZoneKey(-15, -200).ToVector2s().ToVector2i().y == new ZoneKey(-15, -200).ToVector2i().y);
        }

        // ---- ZoneClock --------------------------------------------------------------

        private static void ZoneClockTests()
        {
            Console.WriteLine("\nZoneClock");

            ZoneClock.Clear();

            // A zone with no history has no backlog. If this ever returns elapsed time, a
            // brand-new world instantly drifts by however long the save file has existed.
            var fresh = new ZoneKey(1, 1);
            Check("first contact credits nothing",
                Math.Abs(ZoneClock.CreditOnContact(fresh)) < 0.001);

            Check("first contact establishes history",
                ZoneClock.HasHistory(fresh));

            // Restore() is the seam that makes elapsed time testable without waiting for it.
            var twoHours = new ZoneKey(2, 2);
            ZoneClock.Restore(twoHours, DateTime.UtcNow.AddHours(-2).Ticks);
            double credited = ZoneClock.CreditOnContact(twoHours);
            Check($"two hours away credits ~7200s (got {credited:F1})",
                credited > 7195 && credited < 7205);

            Check("crediting consumes the backlog",
                ZoneClock.CreditOnContact(twoHours) < 1.0);

            // The cap is what keeps a long-idle world playable.
            var longGone = new ZoneKey(3, 3);
            ZoneClock.Restore(longGone, DateTime.UtcNow.AddDays(-30).Ticks);
            double capped = ZoneClock.CreditOnContact(longGone);
            Check($"30 days is capped at MaxCreditSeconds (got {capped:F0}s, cap {ModConfig.MaxCreditSeconds.Value:F0}s)",
                Math.Abs(capped - ModConfig.MaxCreditSeconds.Value) < 1.0);

            // NTP correction, host reboot, or a save copied between machines.
            var future = new ZoneKey(4, 4);
            ZoneClock.Restore(future, DateTime.UtcNow.AddHours(1).Ticks);
            Check("a backwards clock credits zero, not a negative",
                Math.Abs(ZoneClock.CreditOnContact(future)) < 0.001);

            // Peek must not consume, or diagnostics would silently eat real drift.
            var peeked = new ZoneKey(5, 5);
            ZoneClock.Restore(peeked, DateTime.UtcNow.AddHours(-1).Ticks);
            double peek1 = ZoneClock.PeekElapsed(peeked);
            double peek2 = ZoneClock.PeekElapsed(peeked);
            Check($"PeekElapsed does not consume the backlog ({peek1:F0}s then {peek2:F0}s)",
                peek1 > 3595 && Math.Abs(peek1 - peek2) < 1.0);

            Check("PeekElapsed is also capped",
                PeekIsCapped());

            Check("PeekElapsed on an unknown zone is zero",
                Math.Abs(ZoneClock.PeekElapsed(new ZoneKey(999, 999))) < 0.001);

            var forgotten = new ZoneKey(6, 6);
            ZoneClock.Restore(forgotten, DateTime.UtcNow.AddHours(-5).Ticks);
            ZoneClock.Forget(forgotten);
            Check("Forget clears history so the next contact credits zero",
                !ZoneClock.HasHistory(forgotten)
                && Math.Abs(ZoneClock.CreditOnContact(forgotten)) < 0.001);

            // Sparse by construction — only contacted zones exist. A registry that grows to
            // every zone in the world is the thing this design exists to avoid.
            ZoneClock.Clear();
            ZoneClock.MarkContact(new ZoneKey(10, 10));
            ZoneClock.MarkContact(new ZoneKey(11, 11));
            Check($"only contacted zones are tracked (got {ZoneClock.TrackedZoneCount})",
                ZoneClock.TrackedZoneCount == 2);

            Check("MarkContact establishes history without crediting",
                Math.Abs(ZoneClock.CreditOnContact(new ZoneKey(10, 10))) < 1.0);

            // Snapshot is what persistence will serialise.
            int snapshotCount = 0;
            foreach (var _ in ZoneClock.Snapshot()) snapshotCount++;
            Check($"Snapshot exposes every tracked zone (got {snapshotCount})",
                snapshotCount == ZoneClock.TrackedZoneCount);
        }

        private static bool PeekIsCapped()
        {
            var z = new ZoneKey(7, 7);
            ZoneClock.Restore(z, DateTime.UtcNow.AddDays(-30).Ticks);
            return Math.Abs(ZoneClock.PeekElapsed(z) - ModConfig.MaxCreditSeconds.Value) < 1.0;
        }

        // ---- ZoneState --------------------------------------------------------------

        private static void ZoneStateTests()
        {
            Console.WriteLine("\nZoneState");

            Check("a fresh state is default (and so is never written to disk)",
                new ZoneState().IsDefault);

            var touched = new ZoneState { Scorch = 0.1f };
            Check("any non-zero field makes it non-default",
                !touched.IsDefault);

            var over = new ZoneState { Fertility = 5f, Corruption = -3f };
            over.Clamp();
            Check("Clamp bounds values into 0..1",
                Math.Abs(over.Fertility - 1f) < 0.001f && Math.Abs(over.Corruption) < 0.001f);

            // NaN is the dangerous one: it survives every later multiply and silently poisons
            // whatever gameplay value it feeds.
            var nan = new ZoneState { Plague = float.NaN };
            nan.Clamp();
            Check("Clamp converts NaN to zero rather than propagating it",
                !float.IsNaN(nan.Plague) && Math.Abs(nan.Plague) < 0.001f);
        }

        // ---- Persistence ------------------------------------------------------------

        private static void PersistenceTests()
        {
            Console.WriteLine("\nPersistence");

            string dir = Path.Combine(Path.GetTempPath(), "rw_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                Persistence.OverrideDirectory = dir;
                Persistence.OverrideWorldUid = 123456789UL;

                Persistence.Load();
                Check("loading a world with no store yields an empty, usable state",
                    Persistence.IsLoaded && Persistence.TrackedZoneCount == 0);

                // Sparseness is the core guarantee: a default state must not create an entry.
                Persistence.Set(new ZoneKey(1, 1), new ZoneState());
                Check("storing a default state does not create an entry",
                    Persistence.TrackedZoneCount == 0);

                var burnt = new ZoneState { Scorch = 0.75f, Fertility = 0.25f };
                Persistence.Set(new ZoneKey(10, -20), burnt);
                Persistence.Set(new ZoneKey(-5, 7), new ZoneState { Plague = 0.5f });
                Check($"non-default states are stored (got {Persistence.TrackedZoneCount})",
                    Persistence.TrackedZoneCount == 2);

                // A zone that heals must stop costing disk space, or the file only ever grows.
                Persistence.Set(new ZoneKey(-5, 7), new ZoneState());
                Check("a zone returning to default is removed entirely",
                    Persistence.TrackedZoneCount == 1);

                Persistence.Save(force: true);

                // The round-trip is the thing that fails silently in production.
                Persistence.Clear();
                Persistence.Load();

                ZoneState back = Persistence.Get(new ZoneKey(10, -20));
                Check($"values survive a save/load round-trip ({back})",
                    Math.Abs(back.Scorch - 0.75f) < 0.0001f &&
                    Math.Abs(back.Fertility - 0.25f) < 0.0001f);

                Check("negative zone coordinates round-trip",
                    Persistence.TrackedZoneCount == 1);

                Check("an untouched zone reads back as pristine default",
                    Persistence.Get(new ZoneKey(999, 999)).IsDefault);

                // Per-line isolation: one bad line must not discard the rest of the file.
                string path = Directory.GetFiles(dir, "*.dat")[0];
                var lines = new List<string>(File.ReadAllLines(path));
                lines.Insert(2, "this\tis\tnot\ta\tvalid\tline");
                lines.Add("42\t42\t0\t0.5\t0.5\t0.5\t0.5\t0.5");
                File.WriteAllLines(path, lines);

                Persistence.Clear();
                Persistence.Load();
                Check($"a corrupt line is skipped, others survive (got {Persistence.TrackedZoneCount})",
                    Persistence.TrackedZoneCount == 2);

                // Values out of range in the file must be clamped on READ, not trusted.
                File.WriteAllLines(path, new[]
                {
                    "version\t1",
                    "7\t7\t0\t9.0\t-9.0\t0.5\t0.5\t0.5"
                });
                Persistence.Clear();
                Persistence.Load();
                ZoneState clamped = Persistence.Get(new ZoneKey(7, 7));
                Check($"out-of-range values in the file are clamped on read ({clamped})",
                    Math.Abs(clamped.Fertility - 1f) < 0.001f &&
                    Math.Abs(clamped.Corruption) < 0.001f);

                // A wholly unreadable file must leave the world playable.
                File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x00, 0xFF });
                Persistence.Clear();
                Persistence.Load();
                Check("a binary-garbage file degrades to empty rather than throwing",
                    Persistence.IsLoaded);

                // The damage has to survive as evidence. Left in place it reads as a fresh world
                // and the next autosave writes over it.
                Check("a wholly unreadable file is quarantined instead of left to be overwritten",
                    Directory.GetFiles(dir, "*.corrupt").Length == 1 && !File.Exists(path));

                // A header and nothing else is a world that has simply never drifted.
                foreach (string dead in Directory.GetFiles(dir, "*.corrupt")) File.Delete(dead);
                File.WriteAllLines(path, new[]
                {
                    "version\t1",
                    "# zoneX\tzoneY\tcontactTicks\tfert\tcorr\tscorch\tfrost\tplague"
                });
                Persistence.Clear();
                Persistence.Load();
                Check("a file holding only a header is an empty world, not a damaged one",
                    Persistence.IsLoaded && Directory.GetFiles(dir, "*.corrupt").Length == 0);

                // Partial damage stays on the per-line path: one bad line costs one zone.
                File.WriteAllLines(path, new[]
                {
                    "version\t1",
                    "3\t4\t0\t0.5\t0.5\t0.5\t0.5\t0.5",
                    "not a zone",
                    "still\tnot\ta\tzone"
                });
                Persistence.Clear();
                Persistence.Load();
                Check($"a file with any readable zone is kept, not quarantined (got {Persistence.TrackedZoneCount})",
                    Persistence.TrackedZoneCount == 1
                    && Directory.GetFiles(dir, "*.corrupt").Length == 0);

                // ---- what the mod writes, the mod must be able to read ----------------------
                // Every fixture above was written by the harness with File.WriteAllLines, which
                // emits no BOM. The shipped writer used Encoding.UTF8, which does — so the tests
                // agreed with each other and disagreed with the file on disk.

                foreach (string dead in Directory.GetFiles(dir, "*.corrupt")) File.Delete(dead);

                Persistence.Clear();
                Persistence.Set(new ZoneKey(5, 6), new ZoneState { Frost = 0.4f });
                Persistence.Save(force: true);

                byte[] raw = File.ReadAllBytes(path);
                Check("the store is written without a byte-order mark",
                    raw.Length >= 3 && !(raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF));

                Persistence.Clear();
                Persistence.Load();
                ZoneState frosted = Persistence.Get(new ZoneKey(5, 6));
                Check($"a store written by Save round-trips through Load ({frosted})",
                    Persistence.TrackedZoneCount == 1
                    && Math.Abs(frosted.Frost - 0.4f) < 0.0001f
                    && Directory.GetFiles(dir, "*.corrupt").Length == 0);

                // The thinnest real store: headers and nothing else. Guards the corruption rule
                // against its worst false positive, on a file the MOD wrote rather than one the
                // harness hand-built.
                Persistence.Clear();
                Persistence.Save(force: true);
                Persistence.Load();
                Check("a store the mod wrote with no zones loads as empty, not corrupt",
                    Persistence.IsLoaded
                    && Persistence.TrackedZoneCount == 0
                    && Directory.GetFiles(dir, "*.corrupt").Length == 0);

                // Stores written by v0.1.5 and earlier carry a BOM. File.ReadAllLines consumes it,
                // so they load fine — this pins that down, and fails if the read path is ever
                // swapped for something less forgiving.
                File.WriteAllText(path,
                    "version\t1\n8\t9\t0\t0.5\t0\t0\t0\t0\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                Persistence.Clear();
                Persistence.Load();
                Check("a store carrying a legacy BOM still loads its zones",
                    Persistence.TrackedZoneCount == 1
                    && Directory.GetFiles(dir, "*.corrupt").Length == 0);

                // Different worlds must not share drift.
                Persistence.OverrideWorldUid = 987654321UL;
                Persistence.Load();
                Check("a different world uid sees none of the first world's drift",
                    Persistence.TrackedZoneCount == 0);
            }
            finally
            {
                Persistence.OverrideDirectory = null;
                Persistence.OverrideWorldUid = null;
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---- BiomeState -------------------------------------------------------------

        private static void BiomeStateTests()
        {
            Console.WriteLine("\nBiomeState");

            const float Hour = 3600f;

            // Recovery is linear in elapsed time. Exponential decay would look more natural and
            // never reach zero, which is what would keep every visited zone in the store forever.
            var damaged = new ZoneState { Scorch = 0.5f, Corruption = 0.5f };
            ZoneState afterHour = BiomeDrift.Apply(damaged, Hour, 0.1f, 0f, 0f, 1f);
            Check($"an hour of recovery removes one hour's worth ({afterHour})",
                Math.Abs(afterHour.Corruption - 0.4f) < 0.0001f);

            ZoneState afterTwo = BiomeDrift.Apply(damaged, 2 * Hour, 0.1f, 0f, 0f, 1f);
            Check($"twice the time removes twice as much ({afterTwo})",
                Math.Abs(afterTwo.Corruption - 0.3f) < 0.0001f);

            // The whole point of the snap: a zone must be able to become default again, or it
            // never leaves the store and the file grows without bound.
            var nearlyHealed = new ZoneState { Corruption = 0.0001f };
            ZoneState healed = BiomeDrift.Apply(nearlyHealed, Hour, 0.02f, 0f, 0f, 1f);
            Check("decay terminates at exactly zero rather than approaching it",
                healed.IsDefault);

            // ...and that healed zone must actually leave the store, which is the acceptance
            // criterion rather than an implementation detail.
            string dir = Path.Combine(Path.GetTempPath(), "rw_biome_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Persistence.OverrideDirectory = dir;
                Persistence.OverrideWorldUid = 555UL;
                Persistence.Load();

                var zone = new ZoneKey(4, 4);
                Persistence.Set(zone, new ZoneState { Corruption = 0.0001f });
                bool wasStored = Persistence.TrackedZoneCount == 1;

                Persistence.Set(zone, BiomeDrift.Apply(
                    Persistence.Get(zone), Hour, 0.02f, 0f, 0f, 1f));

                Check("a zone that drifts back to pristine is removed from the store",
                    wasStored && Persistence.TrackedZoneCount == 0);
            }
            finally
            {
                Persistence.OverrideDirectory = null;
                Persistence.OverrideWorldUid = null;
                try { Directory.Delete(dir, true); } catch { }
            }

            Check("recovery never drives a value negative",
                BiomeDrift.Apply(new ZoneState { Frost = 0.1f }, 100 * Hour, 0.5f, 0f, 0f, 1f)
                    .IsDefault);

            Check("zero elapsed time changes nothing",
                BiomeDrift.Apply(damaged, 0.0, 0.1f, 0.1f, 1.6f, 1f).Scorch == damaged.Scorch);

            // Frost is the one field that builds with no event behind it.
            ZoneState winter = BiomeDrift.Apply(default, Hour, 0.02f, 0.05f, 1.6f, 0.4f);
            Check($"winter accumulates frost on a pristine zone ({winter})",
                winter.Frost > 0f && winter.IsDefault == false);

            ZoneState summer = BiomeDrift.Apply(default, Hour, 0.02f, 0.05f, 0f, 1.6f);
            Check("summer accumulates no frost, and invents nothing else either",
                summer.IsDefault);

            // Net effect, not order of operations: recovery and pressure are applied to the same
            // field, so winter must still be a net gain and the thaw a net loss.
            var frosted = new ZoneState { Frost = 0.5f };
            Check("winter is a net gain against recovery",
                BiomeDrift.Apply(frosted, Hour, 0.02f, 0.05f, 1.6f, 1f).Frost > 0.5f);
            Check("the thaw is a net loss",
                BiomeDrift.Apply(frosted, Hour, 0.02f, 0.05f, 0.3f, 1f).Frost < 0.5f);

            // Dry season slows healing rather than causing new damage.
            var burnt = new ZoneState { Scorch = 0.5f };
            float summerScorch = BiomeDrift.Apply(burnt, Hour, 0.1f, 0f, 0f, 1.6f).Scorch;
            float springScorch = BiomeDrift.Apply(burnt, Hour, 0.1f, 0f, 0f, 0.8f).Scorch;
            Check($"scorch recovers more slowly at higher fire risk ({summerScorch:F3} vs {springScorch:F3})",
                summerScorch > springScorch);

            // Clamp on the way out, same as everywhere else that touches ZoneState.
            ZoneState piled = BiomeDrift.Apply(
                new ZoneState { Frost = 0.9f }, 100 * Hour, 0f, 0.5f, 1.6f, 1f);
            Check($"accumulated frost is clamped to 1 ({piled.Frost:F3})",
                Math.Abs(piled.Frost - 1f) < 0.0001f);

            // REGRESSION. A tick's gain is far smaller than any value a player would notice, so
            // a rounding floor anywhere near it does not slow accumulation, it stops it dead: the
            // next pass's decay zeroes what the last one added, forever, and the store fills with
            // dust while looking healthy. These two run at the real default rates and interval.
            ZoneState acc = default;
            for (int i = 0; i < 40; i++)
                acc = BiomeDrift.Apply(acc, 30f, 0f, 0.015f, 0.3f, 1f);
            Check($"small per-tick gains accumulate instead of being rounded away each pass ({acc.Frost:E2})",
                acc.Frost > 0.001f);

            ZoneState winterAcc = default;
            for (int i = 0; i < 200; i++)
                winterAcc = BiomeDrift.Apply(winterAcc, 30f, 0.02f, 0.015f, 1.6f, 1f);
            Check($"winter accumulates against recovery at default rates ({winterAcc.Frost:E2})",
                winterAcc.Frost > 0.001f);

            Check("a NaN that reaches the drift math is neutralised, not propagated",
                !float.IsNaN(BiomeDrift.Apply(
                    new ZoneState { Plague = float.NaN }, Hour, 0.02f, 0f, 0f, 1f).Plague));
        }

        // ---- ConfigLedger -----------------------------------------------------------

        private static void ConfigLedgerTests()
        {
            Console.WriteLine("\nConfigLedger");

            // ParseIni against what BepInEx actually writes: a header banner in ## comments,
            // sections whose names contain spaces and digits, blank lines, and descriptions as
            // # comments above each key.
            var lines = new[]
            {
                "## Settings file was created by plugin Ragnarok's Wrath v0.27.0",
                "## Plugin GUID: com.raveniron.ragnarokswrath",
                "",
                "[6 - Weather]",
                "",
                "# Setting type: Boolean",
                "# Default value: false",
                "StormsForceWeather = true",
                "",
                "StormForcedEnvironment = ThunderStorm",
                "",
                "[1 - Core]",
                "TickBudgetMs = 2",
            };
            var snap = ConfigLedger.ParseIni(lines);

            Check("a section with spaces and digits parses",
                snap[ConfigLedger.Slot("6 - Weather", "StormsForceWeather")] == "true");
            Check("a key after a blank line and comments still parses",
                snap[ConfigLedger.Slot("6 - Weather", "StormForcedEnvironment")] == "ThunderStorm");
            Check("a later section does not swallow an earlier one",
                snap[ConfigLedger.Slot("1 - Core", "TickBudgetMs")] == "2");
            Check("## banner lines are not mistaken for keys",
                !snap.ContainsKey(ConfigLedger.Slot("", "## Settings file was created by plugin Ragnarok's Wrath v0")));
            // CORRECTED 2026-09-18. This asserted the opposite, and stated it as a fact about
            // BepInEx: "keys are matched ignoring case, as BepInEx writes them". BepInEx's
            // ConfigDefinition.Equals is the two-argument string.Equals over a case-sensitive
            // GetHashCode, so a mis-cased line is a DIFFERENT key there - it binds nothing and
            // becomes an orphan. An ignore-case snapshot answers "present" for a key BepInEx
            // considers absent, which silently cancels a backfill and then stamps the version,
            // making it permanent. This mod ships a live backfill, so that was reachable.
            Check("the snapshot is ORDINAL and case-SENSITIVE, matching BepInEx's own key comparison",
                !snap.ContainsKey(ConfigLedger.Slot("6 - WEATHER", "stormsforceweather")));
            Check("and still finds the key spelled the way BepInEx wrote it",
                snap.ContainsKey(ConfigLedger.Slot("6 - Weather", "StormsForceWeather")));
            Check("parsing null never throws", ConfigLedger.ParseIni(null).Count == 0);

            // ---- A hand-edited or corrupt stamp must not become a loop bound. ConfigVersion
            //      carries no AcceptableValueRange (removed 2026-09-18, because BepInEx clamps
            //      silently and a ceiling would one day refuse the stamp), so a large negative
            //      number is reachable by hand - and an unclamped window ran from there.
            var negativeSnapshot = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ConfigLedger.Slot("1 - Core", "TickBudgetMs"), "2" },
            };
            var negativeClock = System.Diagnostics.Stopwatch.StartNew();
            var fromNegative = ConfigLedger.Plan(negativeSnapshot, -2000000000);
            negativeClock.Stop();
            Check("a wildly negative stamp still plans the version 1 backfill",
                fromNegative.Backfilled.Count == 1);
            Check($"and costs ONE step rather than two billion ({negativeClock.ElapsedMilliseconds} ms)",
                negativeClock.ElapsedMilliseconds < 250);

            // ---- One slot, one decision. Two rungs naming the same key judged it twice against
            //      the same unchanged snapshot, so one key could be reported and reset once per
            //      rung. The shipped Rebases table is empty, so this needs the table seam.
            var twoRungs = new Dictionary<int, ConfigLedger.Rebase[]>
            {
                { 1, new[] { new ConfigLedger.Rebase { Section = "1 - Core", Key = "TickBudgetMs", OldDefaults = new[] { "2" } } } },
                { 2, new[] { new ConfigLedger.Rebase { Section = "1 - Core", Key = "TickBudgetMs", OldDefaults = new[] { "2" } } } },
            };
            var decidedOnce = ConfigLedger.Plan(negativeSnapshot, 0, 2, twoRungs,
                new Dictionary<int, ConfigLedger.Backfill[]>());
            Check($"a key named by two rungs is decided ONCE, by the first that matches ({decidedOnce.ResetToDefault.Count})",
                decidedOnce.ResetToDefault.Count == 1);

            // ---- The rebase rules themselves. The shipped table is EMPTY, so none of this had
            //      ever executed: the first real rebase would have been its first run anywhere.
            var adminSnapshot = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { ConfigLedger.Slot("1 - Core", "TickBudgetMs"), "4" },
            };
            var oneRung = new Dictionary<int, ConfigLedger.Rebase[]>
            {
                { 1, new[] { new ConfigLedger.Rebase { Section = "1 - Core", Key = "TickBudgetMs", OldDefaults = new[] { "1", "2" } } } },
            };
            var noBackfills = new Dictionary<int, ConfigLedger.Backfill[]>();
            Check("a stored value equal to an old shipped default is moved to the new one",
                ConfigLedger.Plan(negativeSnapshot, 0, 1, oneRung, noBackfills).ResetToDefault.Count == 1);
            Check("a value the admin chose is KEPT, and reported with what it holds",
                ConfigLedger.Plan(adminSnapshot, 0, 1, oneRung, noBackfills).Kept[0].Value == "4");
            Check("old defaults are compared as exact TEXT, so '2.0' is not '2'",
                ConfigLedger.Plan(new Dictionary<string, string>(StringComparer.Ordinal)
                    { { ConfigLedger.Slot("1 - Core", "TickBudgetMs"), "2.0" } },
                    0, 1, oneRung, noBackfills).Kept.Count == 1);

            // A value may itself contain '=' - only the FIRST one separates.
            var eq = ConfigLedger.ParseIni(new[] { "[S]", "K = a=b=c" });
            Check("only the first = separates key from value", eq[ConfigLedger.Slot("S", "K")] == "a=b=c");

            // Version stamp.
            Check("an unstamped file reads as version 0", ConfigLedger.ReadVersion(snap) == 0);
            Check("a null snapshot reads as version 0", ConfigLedger.ReadVersion(null) == 0);
            Check("garbage in the version stamp reads as 0, not as a crash",
                ConfigLedger.ReadVersion(ConfigLedger.ParseIni(new[] { "[Meta]", "ConfigVersion = banana" })) == 0);
            Check("a stamped file reads its version",
                ConfigLedger.ReadVersion(ConfigLedger.ParseIni(new[] { "[Meta]", "ConfigVersion = 1" })) == 1);

            // THE MIGRATION ITSELF. An existing pre-version file must come out behaving exactly as
            // it did: StormDryChance backfilled to 0, so every storm keeps the single sky it had.
            var planned = ConfigLedger.Plan(snap, 0);
            Check("an unstamped file plans the storm backfill", planned.Backfilled.Count == 1);
            Check("the backfill names the right key",
                planned.Backfilled[0].Slot == ConfigLedger.Slot(ModConfig.WeatherSection, "StormDryChance"));
            Check("the backfill preserves old behaviour with 0, not the shipped 0.5",
                planned.Backfilled[0].Value == "0");
            Check("the plan reports where it came from and where it goes",
                planned.FromVersion == 0 && planned.ToVersion == ConfigLedger.CurrentVersion);

            // A FRESH INSTALL MUST NOT BE MIGRATED. This is the case that decides whether new
            // worlds get the new feature at all: plan anything here and the shipped default is
            // dead on arrival for everyone.
            Check("a fresh install (no file, empty snapshot) plans nothing",
                ConfigLedger.Plan(new Dictionary<string, string>(), 0).IsEmpty);
            Check("a null snapshot plans nothing", ConfigLedger.Plan(null, 0).IsEmpty);

            // An already-migrated file must not be migrated twice - the second pass would stamp
            // over an admin's own later edit of the same key.
            Check("a file already at the current version plans nothing",
                ConfigLedger.Plan(snap, ConfigLedger.CurrentVersion).IsEmpty);
            Check("a file from the future plans nothing",
                ConfigLedger.Plan(snap, ConfigLedger.CurrentVersion + 5).IsEmpty);

            // VERSION 3: a 0.28.0 file (layout 2) loses exactly the spawn war's dead cap, from the
            // advanced file where 0.28.0 wrote it, and nothing else moves.
            var v2 = ConfigLedger.ParseIni(new[]
            {
                "[11 - Rivalry]",
                "EnableRivalry = true",
                "",
                "[Meta]",
                "ConfigVersion = 2",
            });
            foreach (var kv in ConfigLedger.ParseIni(new[]
                     {
                         "[11 - Rivalry]",
                         "ContestWildSpawnChance = 100",
                         "ContestWildMaxSpawned = 15",
                     }))
                v2[ConfigLedger.AdvancedPrefix + kv.Key] = kv.Value;
            var v3Plan = ConfigLedger.Plan(v2, 2);
            Check("a version-2 file drops ContestWildMaxSpawned from the advanced file, as a retirement",
                v3Plan.Dropped.Count == 1 &&
                v3Plan.Dropped[0].Slot == ConfigLedger.AdvancedSlot("11 - Rivalry", "ContestWildMaxSpawned") &&
                v3Plan.Dropped[0].Because.StartsWith("retired", StringComparison.Ordinal));
            Check("and carries, resets or backfills nothing: the chance stays exactly as the owner set it",
                v3Plan.Moved.Count == 0 && v3Plan.ResetToDefault.Count == 0 &&
                v3Plan.Backfilled.Count == 0 && v3Plan.Relocated.Count == 0 && !v3Plan.ChangesBehaviour);
            v2.Remove(ConfigLedger.AdvancedSlot("11 - Rivalry", "ContestWildMaxSpawned"));
            Check("a version-2 file without the key has nothing to do but the stamp",
                ConfigLedger.Plan(v2, 2).IsEmpty);

            // THE SAFETY OF A BACKFILL: present means untouched. An admin who already set the key
            // (or a half-finished earlier run that wrote it) must never be overwritten.
            var already = ConfigLedger.ParseIni(new[]
            {
                "[6 - Weather]",
                "StormDryChance = 0.8",
            });
            Check("a key the file already has is never backfilled over",
                ConfigLedger.Plan(already, 0).Backfilled.Count == 0);

            // THE RELOCATION RUNG, added after the live Storm10 run on 2026-09-18 showed the plain
            // backfill preserving BEHAVIOUR but not MEANING. An owner who had set the only sky
            // there was to Eikthyr — the DRY one — ended up with that value sitting in the key
            // that now means specifically the WET one, and the mod's own boot line read
            // "wet 'Eikthyr' or dry 'Eikthyr'".
            string Val(ConfigLedger.MigrationPlan p, string key)
            {
                foreach (var r in p.Relocated)
                    if (r.Slot == ConfigLedger.Slot(ModConfig.WeatherSection, key)) return r.Value;
                return null;
            }

            var eikthyrOwner = ConfigLedger.ParseIni(new[]
            {
                "[6 - Weather]",
                "StormsForceWeather = true",
                "StormForcedEnvironment = Eikthyr",
            });
            var moved = ConfigLedger.Plan(eikthyrOwner, 0);

            Check("an Eikthyr owner's sky moves to the key that now means dry",
                Val(moved, "StormDryEnvironment") == "Eikthyr");
            Check("the wet key goes back to the shipped wet default",
                Val(moved, "StormForcedEnvironment") == ConfigLedger.WetEnvironmentDefault);
            Check("every storm still rolls dry, so behaviour is unchanged",
                Val(moved, "StormDryChance") == "1");
            Check("the relocation replaces the plain backfill rather than fighting it",
                moved.Backfilled.Count == 0);
            Check("a relocation is not reported as a backfill", moved.Relocated.Count == 3);

            // Case matters in a config file that humans type into.
            var lowerCase = ConfigLedger.ParseIni(new[] { "[6 - Weather]", "StormForcedEnvironment = eikthyr" });
            Check("the dry sky is recognised whatever case it was typed in",
                ConfigLedger.Plan(lowerCase, 0).Relocated.Count == 3);

            // A ThunderStorm owner, or anyone on a custom sky, keeps the simple path: their value
            // already means what the key says, so only the new chance needs pinning.
            var custom = ConfigLedger.ParseIni(new[] { "[6 - Weather]", "StormForcedEnvironment = Mistlands_clear" });
            var customPlan = ConfigLedger.Plan(custom, 0);
            Check("a custom sky is never relocated", customPlan.Relocated.Count == 0);
            Check("a custom sky still gets the chance pinned to 0", customPlan.Backfilled.Count == 1);

            // THE ONE PLACE THIS MIGRATION WRITES OVER A KEY THE OWNER SET, so the guard on it
            // matters more than the feature: a file already carrying either new key is somebody's
            // own choice, or an earlier run's, and is never second-guessed.
            var halfDone = ConfigLedger.ParseIni(new[]
            {
                "[6 - Weather]",
                "StormForcedEnvironment = Eikthyr",
                "StormDryChance = 0.25",
            });
            Check("a file that already has StormDryChance is never relocated",
                ConfigLedger.Plan(halfDone, 0).Relocated.Count == 0);
            var halfDone2 = ConfigLedger.ParseIni(new[]
            {
                "[6 - Weather]",
                "StormForcedEnvironment = Eikthyr",
                "StormDryEnvironment = ThunderStorm",
            });
            Check("a file that already has StormDryEnvironment is never relocated",
                ConfigLedger.Plan(halfDone2, 0).Relocated.Count == 0);

            string movedLine = ConfigLedger.Describe(moved);
            Check("the boot line says a value MOVED, not merely that it was set",
                movedLine.Contains("moved to"));
            Check("the boot line names both ends of the move",
                movedLine.Contains("StormDryEnvironment") && movedLine.Contains("StormForcedEnvironment"));

            // Describe is what the owner reads; it must say the key, the value and the reason.
            string described = ConfigLedger.Describe(planned);
            Check("the boot line names the key it changed", described.Contains("StormDryChance"));
            Check("the boot line names the value it wrote", described.Contains("0"));
            Check("the boot line names both versions",
                described.Contains("version 0") && described.Contains("-> " + ConfigLedger.CurrentVersion));
            Check("an empty plan describes itself as nothing to migrate",
                ConfigLedger.Describe(ConfigLedger.Plan(null, 0)).Contains("nothing to migrate"));
            Check("describing a null plan never throws",
                ConfigLedger.Describe(null).Contains("nothing to migrate"));

            ConfigMigrationEndToEndTests();
            ConfigLayoutTests();
        }

        /// <summary>
        /// The plan being right is not the same as the plan being APPLIED, and the join between
        /// them is a pair of bare strings: ConfigLedger names "6 - Weather"/"StormDryChance" and
        /// ModConfig binds them from its own separate literals. Mistype either and Finish looks up
        /// a key that does not exist, logs one warning, stamps the version anyway — and the
        /// migration never runs again on that file, because it now reads as current. Silent, and
        /// permanent. Nothing but an end-to-end bind catches it.
        /// </summary>
        private static void ConfigMigrationEndToEndTests()
        {
            string dir = Path.Combine(Path.GetTempPath(), "rw_cfgmig_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // A pre-migration file, as 0.27.0 would have written it: no Meta section, no
                // StormDryChance, and an owner who had turned the storm look on.
                string path = Path.Combine(dir, "com.raveniron.ragnarokswrath.cfg");
                File.WriteAllLines(path, new[]
                {
                    "## Settings file was created by plugin Ragnarok's Wrath v0.27.0",
                    "",
                    "[6 - Weather]",
                    "StormsForceWeather = true",
                    "StormForcedEnvironment = ThunderStorm",
                });

                var upgraded = new ConfigFile { ConfigFilePath = path };
                ModConfig.Bind(upgraded, new ConfigFile());

                Check("an upgraded config keeps its storms single-sky (StormDryChance backfilled to 0)",
                    Math.Abs(ModConfig.StormDryChance.Value - 0f) < 0.0001f);
                Check("an upgraded config is stamped at the current version",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);
                Check("the owner's own value is left alone by the migration",
                    ModConfig.StormsForceWeather.Value);
                Check("the migration saved the file it changed", upgraded.SaveCount > 0);
                Check("a backup of the pre-migration config was written beside it",
                    File.Exists(path + ".v0.bak"));

                // Running again over the now-stamped file must be a no-op, not a second migration:
                // an admin who raises StormDryChance after upgrading must keep their value.
                //
                // The stamp has to be written HERE. The stub's Save() is a counter, not a writer,
                // so the file on disk is still the pre-migration one and a second Bind against it
                // would simply migrate again — which is what this assertion was quietly measuring
                // before 2026-09-18. Writing the post-migration state makes it test the
                // short-circuit it claims to test.
                File.WriteAllLines(path, new[]
                {
                    "[" + ConfigLedger.MetaSection + "]",
                    ConfigLedger.VersionKey + " = " + ConfigLedger.CurrentVersion.ToString(CultureInfo.InvariantCulture),
                    "",
                    "[" + ModConfig.WeatherSection + "]",
                    "StormsForceWeather = true",
                    "StormForcedEnvironment = ThunderStorm",
                    "StormDryChance = 0",
                });
                var second = new ConfigFile { ConfigFilePath = path };
                ModConfig.Bind(second, new ConfigFile());
                Check("a second boot does not migrate an already-stamped file again",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);
                // The stamp alone cannot prove the short-circuit: re-planning a current file finds
                // the backfilled key PRESENT, skips it, and leaves the same stamp behind. The
                // summary is the only thing that differs, and it is what `wrath status` now reads.
                Check("and reports NO migration summary, because it short-circuited before planning one",
                    ConfigMigration.LastSummary == "");

                // ---- A REFUSED STEP MUST REACH THE LINE `wrath status` PRINTS. LastSummary is
                //      written in Begin from the plan's INTENT, before a single step has run, and
                //      Apply can then refuse one: a ledger row naming a key this build no longer
                //      binds warns and moves on. Without a correction the status line claims a
                //      value was moved that was never found, and the only contradiction is a
                //      warning hundreds of log lines earlier. RW's tables hold no such row today,
                //      so the plan is hand-built; that is the point, because the row that
                //      introduces one will be a release, on somebody else's config file.
                var refusing = new ConfigFile { ConfigFilePath = Path.Combine(dir, "does_not_exist.cfg") };
                ConfigMigration.Begin(refusing, null);
                var refusedPlan = new ConfigLedger.MigrationPlan();
                refusedPlan.ResetToDefault.Add(ConfigLedger.Slot("Nowhere", "NoSuchKey"));
                ConfigMigration.Apply(refusing, null, refusedPlan);
                ConfigMigration.Finish(refusing, null, refusing.Bind(ConfigLedger.MetaSection, ConfigLedger.VersionKey, 0));
                Check("a refused step corrects the summary `wrath status` prints, rather than leaving it claiming the step happened",
                    ConfigMigration.LastSummary.IndexOf("REFUSED", StringComparison.Ordinal) >= 0);

                ConfigMigration.Begin(refusing, null);
                ConfigMigration.Finish(refusing, null, refusing.Bind(ConfigLedger.MetaSection, ConfigLedger.VersionKey, 0));
                Check("and the count is per boot, so a clean migration after a refused one does not inherit its complaint",
                    ConfigMigration.LastSummary.IndexOf("REFUSED", StringComparison.Ordinal) < 0);

                // A FRESH INSTALL must get the shipped default instead, or the feature ships dead.
                var fresh = new ConfigFile { ConfigFilePath = Path.Combine(dir, "does_not_exist.cfg") };
                ModConfig.Bind(fresh, new ConfigFile());
                Check("a fresh install gets the shipped StormDryChance, not the legacy value",
                    Math.Abs(ModConfig.StormDryChance.Value - 0.5f) < 0.0001f);
                Check("a fresh install is stamped too, so it never migrates later",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);

                // ConfigLedger keeps its own copy of the two sky names so Core need not depend on
                // Config. Two copies of a string drift; this is the pin that stops them.
                Check("the ledger's wet default matches the one ModConfig actually ships",
                    (string)ModConfig.StormForcedEnvironment.DefaultValue == ConfigLedger.WetEnvironmentDefault);
                Check("the ledger's dry default matches the one ModConfig actually ships",
                    (string)ModConfig.StormDryEnvironment.DefaultValue == ConfigLedger.DryEnvironmentDefault);

                // THE STORM10 CASE, end to end: the real config that exposed this, migrated by the
                // shipping ModConfig rather than described. Behaviour must be identical — every
                // storm dry and wearing Eikthyr — while the keys finally say what they mean.
                string eikPath = Path.Combine(dir, "eikthyr.cfg");
                File.WriteAllLines(eikPath, new[]
                {
                    "[6 - Weather]",
                    "StormsForceWeather = true",
                    "StormForcedEnvironment = Eikthyr",
                });
                var eik = new ConfigFile { ConfigFilePath = eikPath };
                ModConfig.Bind(eik, new ConfigFile());

                Check("Storm10's owner keeps an all-dry storm after migrating",
                    Math.Abs(ModConfig.StormDryChance.Value - 1f) < 0.0001f);
                Check("and the sky they actually see is still Eikthyr",
                    ModConfig.StormDryEnvironment.Value == "Eikthyr");
                Check("while the wet key finally holds a wet sky",
                    ModConfig.StormForcedEnvironment.Value == ConfigLedger.WetEnvironmentDefault);
                Check("their own StormsForceWeather is still untouched",
                    ModConfig.StormsForceWeather.Value);

                // ---- THE STAMP ONLY GOES UP (fixed 2026-09-18). A file written by a NEWER build
                //      has already had rungs this build knows nothing about. An unconditional
                //      assignment drags it down on a rollback, and the next upgrade then replays
                //      those rungs against values the owner has since chosen - which a rebase
                //      cannot tell from the old default it happens to equal.
                string futurePath = Path.Combine(dir, "from_the_future.cfg");
                File.WriteAllLines(futurePath, new[]
                {
                    "[" + ConfigLedger.MetaSection + "]",
                    ConfigLedger.VersionKey + " = 7",
                    "",
                    "[" + ModConfig.WeatherSection + "]",
                    "StormDryChance = 0.75",
                });
                var future = new ConfigFile { ConfigFilePath = futurePath };
                ModConfig.Bind(future, new ConfigFile());
                Check("a file stamped ABOVE this build's layout keeps its own stamp rather than being dragged back",
                    ModConfig.ConfigVersion.Value == 7);
                Check("and a newer file's values are left alone by an older build",
                    Math.Abs(ModConfig.StormDryChance.Value - 0.75f) < 0.0001f);

                // ---- A MIS-CASED line is a different key to BepInEx, so it must NOT satisfy the
                //      backfill's absence test. Before the fix this file skipped the backfill and
                //      took the shipped 0.5, silently changing a live world, and then stamped.
                string misCasedPath = Path.Combine(dir, "mis_cased.cfg");
                File.WriteAllLines(misCasedPath, new[]
                {
                    "[6 - Weather]",
                    "stormdrychance = 0.5",
                });
                var misCased = new ConfigFile { ConfigFilePath = misCasedPath };
                ModConfig.Bind(misCased, new ConfigFile());
                Check("a mis-cased line does not satisfy the backfill's absence test, so the world still keeps its one sky",
                    Math.Abs(ModConfig.StormDryChance.Value - 0f) < 0.0001f);
                Check("and the mis-cased line, which nothing ever read, is not left behind under a section that no longer exists",
                    !misCased.HasOrphan("6 - Weather", "stormdrychance"));

                // ---- THE APPLY PATH. The shipped ledger has one rung, so the reset loop and every
                //      failure branch had never run anywhere. Driven here with synthetic plans.
                var live = new ConfigFile();
                ModConfig.Bind(live, new ConfigFile());

                ModConfig.StormDryChance.Value = 0.9f;
                var resetPlan = new ConfigLedger.MigrationPlan();
                resetPlan.ResetToDefault.Add(ConfigLedger.Slot(ModConfig.WeatherSection, "StormDryChance"));
                ConfigMigration.Apply(live, null, resetPlan);
                Check("applying a rebase puts the entry back to its SHIPPED default",
                    Math.Abs(ModConfig.StormDryChance.Value - 0.5f) < 0.0001f);

                // A value BepInEx cannot parse is swallowed by its own SetSerializedValue, which
                // is why the try/catch that used to wrap it was unreachable code. The mod must SAY
                // the backfill did not land, or it stamps the version in silence.
                float beforeBad = ModConfig.StormDryChance.Value;
                var badPlan = new ConfigLedger.MigrationPlan();
                badPlan.Backfilled.Add(new ConfigLedger.BackfilledSlot
                {
                    Slot = ConfigLedger.Slot(ModConfig.WeatherSection, "StormDryChance"), Value = "not-a-number", Because = "test",
                });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ConfigMigration.Apply(live, null, badPlan);
                Check("an unparseable backfill leaves the entry alone rather than corrupting it",
                    Math.Abs(ModConfig.StormDryChance.Value - beforeBad) < 0.0001f);
                Check("and the mod NAMES the slot it could not set, rather than stamping in silence",
                    RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said(ModConfig.WeatherSection + "::StormDryChance"));

                // A CLAMPED value still MOVES the entry, so a did-it-move check calls it success.
                // StormDryChance is a 0..1 share, so 9 lands as 1 - a live world would start
                // rolling a dry storm every single time, silently.
                var clampPlan = new ConfigLedger.MigrationPlan();
                clampPlan.Backfilled.Add(new ConfigLedger.BackfilledSlot
                {
                    Slot = ConfigLedger.Slot(ModConfig.WeatherSection, "StormDryChance"), Value = "9", Because = "test",
                });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ConfigMigration.Apply(live, null, clampPlan);
                Check("an out-of-range backfill is CLAMPED by the config system rather than refused",
                    Math.Abs(ModConfig.StormDryChance.Value - 1f) < 0.0001f);
                Check("and the mod says it stored something other than what the ledger asked for",
                    RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("rather than the '9'"));

                // A ledger row naming a key this build does not bind must warn and carry on, not
                // throw: one bad row would otherwise abandon every step after it.
                bool threw = false;
                var ghostPlan = new ConfigLedger.MigrationPlan();
                ghostPlan.ResetToDefault.Add(ConfigLedger.Slot("9 - Nope", "NoSuchKey"));
                ghostPlan.Backfilled.Add(new ConfigLedger.BackfilledSlot { Slot = "also::missing", Value = "1", Because = "test" });
                try { ConfigMigration.Apply(live, null, ghostPlan); } catch { threw = true; }
                Check("a ledger row naming an unknown key warns and continues rather than throwing", !threw);

                threw = false;
                try { ConfigMigration.Apply(null, null, resetPlan); ConfigMigration.Apply(live, null, null); } catch { threw = true; }
                Check("Apply survives a null config or a null plan", !threw);
            }
            finally
            {
                // Leave the harness's static config the way every other test expects to find it.
                ModConfig.Bind(new ConfigFile(), new ConfigFile());
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>
        /// THE TWO-FILE LAYOUT (config version 2, 0.28.0). Every setting changed section and a hundred
        /// of them changed FILE, and BepInEx addresses a setting by section and key alone: to it a
        /// moved key is a new setting at its shipped default and the owner's value an orphan line it
        /// never reads again. So the one thing that must hold is that every value an owner ever set
        /// lands where this build reads it, and that no failure part-way can lose one.
        ///
        /// The fixture is not hand-written. It is a config the shipped 0.27.5 build wrote on Storm10,
        /// with that server's own custom values in it (storms every 1-3 h, forced Eikthyr sky, a
        /// three-hour outbreak clock), so the test carries exactly the bytes an owner's file holds.
        /// </summary>
        private static void ConfigLayoutTests()
        {
            Console.WriteLine("\nConfig layout (version 2)");

            string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "written-by-0.27.5.cfg");
            Check("the 0.27.5 fixture is present beside the harness", File.Exists(fixture));
            if (!File.Exists(fixture)) return;

            var old = ConfigLedger.ParseIni(File.ReadAllLines(fixture));
            Check("the fixture is a version 1 file, as 0.27.5 stamps it", ConfigLedger.ReadVersion(old) == 1);

            // ---- the table against the build ------------------------------------------------------
            // Bind ModConfig into two empty files and read back every definition it binds. The move
            // table's destinations must be EXACTLY that set: a destination nothing binds would carry
            // a value into the void, and a bound key no move reaches would start every upgraded
            // server at its shipped default.
            var mainProbe = new ConfigFile();
            var advProbe = new ConfigFile();
            ModConfig.Bind(mainProbe, advProbe);
            var bound = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in mainProbe.Keys) bound.Add(ConfigLedger.Slot(d.Section, d.Key));
            foreach (var d in advProbe.Keys) bound.Add(ConfigLedger.AdvancedSlot(d.Section, d.Key));

            var plan = ConfigLedger.Plan(old, 1);
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in plan.Moved) destinations.Add(m.To);

            bool everyDestinationBound = true;
            foreach (string to in destinations)
                if (!bound.Contains(to)) { everyDestinationBound = false; Console.WriteLine("      unbound destination: " + to); }
            Check($"every one of the {destinations.Count} moved settings lands on a key this build binds", everyDestinationBound);

            // A setting whose section kept its name across the renumbering ("13 - Titles") is
            // already where this build reads it: it stays, which counts as arriving.
            bool everyBoundReached = true;
            int stayed = 0;
            foreach (string b in bound)
            {
                if (b == ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey)) continue;
                if (destinations.Contains(b)) continue;
                if (old.ContainsKey(b) && !plan.Dropped.Exists(d => d.Slot == b)) { stayed++; continue; }
                everyBoundReached = false;
                Console.WriteLine("      bound but never carried to: " + b);
            }
            Check($"and every key this build binds is reached by one, or never had to move ({bound.Count - 1} bound besides the stamp, {stayed} stayed put)",
                everyBoundReached && stayed == 1);

            // Every line of the old file has a fate: carried, retired, or the stamp. Nothing is left
            // behind under a section name the new layout no longer has.
            var fate = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in plan.Moved) fate.Add(m.From);
            foreach (var d in plan.Dropped) fate.Add(d.Slot);
            bool everyLineHasAFate = true;
            foreach (string slot in old.Keys)
            {
                if (slot == ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey)) continue;
                if (fate.Contains(slot) || bound.Contains(slot)) continue;   // carried, dropped, or already where it is read
                everyLineHasAFate = false;
                Console.WriteLine("      no fate for: " + slot);
            }
            Check($"every one of the fixture's {old.Count - 1} settings is carried or retired, none forgotten", everyLineHasAFate);

            int retired = 0;
            foreach (var d in plan.Dropped) if (d.Because.StartsWith("retired", StringComparison.Ordinal)) retired++;
            Check("exactly the two storm multipliers and the spawn war's dead cap are retired", retired == 3 &&
                plan.Dropped.Exists(d => d.Slot == ConfigLedger.Slot("6 - Weather", "StormFireRiskMultiplier")) &&
                plan.Dropped.Exists(d => d.Slot == ConfigLedger.Slot("6 - Weather", "StormWindMultiplier")) &&
                plan.Dropped.Exists(d => d.Slot == ConfigLedger.Slot("17 - Rivalry", "ContestWildMaxSpawned") &&
                                         d.Because.StartsWith("retired", StringComparison.Ordinal)));
            Check("and none of them is carried anywhere",
                !plan.Moved.Exists(m => m.From.EndsWith("::StormFireRiskMultiplier", StringComparison.Ordinal) ||
                                        m.From.EndsWith("::StormWindMultiplier", StringComparison.Ordinal) ||
                                        m.From.EndsWith("::ContestWildMaxSpawned", StringComparison.Ordinal)));
            Check("while the spawn war's chance, which still means something, is carried",
                plan.Moved.Exists(m => m.From == ConfigLedger.Slot("17 - Rivalry", "ContestWildSpawnChance") &&
                                       m.To == ConfigLedger.AdvancedSlot(ModConfig.RivalrySection, "ContestWildSpawnChance")));
            Check("a layout move changes where values live, not how the world behaves", !plan.ChangesBehaviour);

            // BepInEx writes sections sorted by NAME. The whole point of the renumbering is that the
            // sort now IS the intended order; a single-digit number anywhere would undo it.
            string[] intended =
            {
                ModConfig.GeneralSection, ModConfig.SeasonSection, ModConfig.WeatherSection, ModConfig.BiomeSection,
                ModConfig.FireSection, ModConfig.PlagueSection, ModConfig.EcologySection, ModConfig.FarmingSection,
                ModConfig.HealthSection, ModConfig.ConsequenceSection, ModConfig.RivalrySection, ModConfig.RelicSection,
                ModConfig.TitlesSection, ModConfig.WorldSection, ModConfig.VisualsSection, ConfigLedger.MetaSection,
            };
            var sorted = (string[])intended.Clone();
            Array.Sort(sorted, StringComparer.CurrentCulture);
            Check("sorting the section names as BepInEx does gives the intended order", string.Join("|", sorted) == string.Join("|", intended));

            // The descriptions are what an owner reads. They were essays; pin that they stay short and
            // free of the internal shorthand that leaked into them.
            int longest = 0; string longestKey = "";
            bool jargonFree = true;
            foreach (var file in new[] { mainProbe, advProbe })
            {
                foreach (var d in file.Keys)
                {
                    var entry = (ConfigEntryBase)file[d];
                    string text = GetDescription(entry);
                    if (text.Length > longest) { longest = text.Length; longestKey = d.Key; }
                    foreach (string bad in new[] { "task ", "Task ", "Phase ", "2026-", "ZDO", "RPC", "WorldTick", "EnvMan" })
                        if (text.Contains(bad)) { jargonFree = false; Console.WriteLine("      " + d.Key + " says '" + bad + "'"); }
                }
            }
            Check($"no setting's description runs past 300 characters (longest: {longestKey}, {longest})", longest <= 300);
            Check("no description carries internal shorthand (task numbers, dates, engine class names)", jargonFree);
            Check($"the main file holds the settings an owner changes and no more ({mainProbe.Keys.Count} keys)",
                mainProbe.Keys.Count >= 30 && mainProbe.Keys.Count <= 50);

            // ---- the whole migration, through the shipping ModConfig -----------------------------
            string dir = Path.Combine(Path.GetTempPath(), "rw_layout_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string mainPath = Path.Combine(dir, "com.raveniron.ragnarokswrath.cfg");
                string advPath = Path.Combine(dir, ConfigLedger.AdvancedFileName);
                File.Copy(fixture, mainPath);

                var main = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var adv = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                ModConfig.Bind(main, adv);

                bool everyValueLanded = true;
                foreach (var m in plan.Moved)
                {
                    ConfigLedger.SplitSlot(m.To, out bool isAdv, out string section, out string key);
                    var file = isAdv ? adv : main;
                    string landed = file[new ConfigDefinition(section, key)].GetSerializedValue();
                    if (!SameConfigText(m.Value, landed))
                    {
                        everyValueLanded = false;
                        Console.WriteLine("      " + key + ": file held '" + m.Value + "', bound '" + landed + "'");
                    }
                }
                Check("EVERY value in the owner's file is the value this build now reads", everyValueLanded);
                Check("the owner's custom sickness tuning survived into the advanced file (0.4 and 0.5, not the shipped 0.3 and 0.38)",
                    Math.Abs(ModConfig.SicknessStaminaRegenAtMax.Value - 0.4f) < 0.0001f &&
                    Math.Abs(ModConfig.SicknessHealthRegenAtMax.Value - 0.5f) < 0.0001f);
                Check("the owner's forced Eikthyr sky survived", ModConfig.StormsForceWeather.Value &&
                    ModConfig.StormDryEnvironment.Value == "Eikthyr" && Math.Abs(ModConfig.StormDryChance.Value - 1f) < 0.0001f);
                Check("the owner's three-hour outbreak clock survived into the main file",
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f);
                Check("the file is stamped at the current version", ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);

                var mainAfter = ConfigLedger.ParseIni(File.ReadAllLines(mainPath));
                var advAfter = ConfigLedger.ParseIni(File.ReadAllLines(advPath));
                bool noOldSection = true;
                foreach (string slot in mainAfter.Keys)
                    if (!slot.StartsWith("0", StringComparison.Ordinal) && !slot.StartsWith("1", StringComparison.Ordinal) &&
                        !slot.StartsWith(ConfigLedger.MetaSection + "::", StringComparison.Ordinal)) noOldSection = false;
                foreach (string slot in old.Keys)
                {
                    // "13 - Titles" is a section name in BOTH layouts, so a line there that never
                    // moved is exactly where it belongs.
                    ConfigLedger.SplitSlot(slot, out _, out string section, out _);
                    if (section == ModConfig.TitlesSection) continue;
                    if (mainAfter.ContainsKey(slot) && slot != ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey))
                        noOldSection = false;
                }
                Check("the main file written to disk holds no old-layout line at all", noOldSection);
                Check("the retired settings are gone from the file, not just unbound",
                    !mainAfter.ContainsKey(ConfigLedger.Slot("6 - Weather", "StormWindMultiplier")));
                Check("the written main file carries the current version stamp",
                    mainAfter.TryGetValue(ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey), out string stamp) && stamp == ConfigLedger.CurrentVersion.ToString(CultureInfo.InvariantCulture));
                Check($"the advanced file was written with its settings ({advAfter.Count})", advAfter.Count >= 90);
                Check("the owner's tuning landed in the advanced file on disk (SicknessHealthRegenAtMax, FarmingCropPrefabs)",
                    advAfter.TryGetValue(ConfigLedger.Slot(ModConfig.HealthSection, "SicknessHealthRegenAtMax"), out string regen) &&
                    SameConfigText("0.5", regen) &&
                    advAfter.TryGetValue(ConfigLedger.Slot(ModConfig.FarmingSection, "FarmingCropPrefabs"), out string crops) &&
                    crops == old[ConfigLedger.Slot("12 - Farming", "FarmingCropPrefabs")]);
                Check("a backup of the version 1 file was written beside it", File.Exists(mainPath + ".v1.bak"));
                Check("BepInEx's save-on-change is handed back on after the migration",
                    main.SaveOnConfigSet && adv.SaveOnConfigSet);

                // The second boot reads what the first one wrote, and must find nothing to do.
                var main2 = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var adv2 = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                ModConfig.Bind(main2, adv2);
                Check("the next boot finds a current file and migrates nothing", ConfigMigration.LastSummary == "");
                Check("and reads the same values back from the two files",
                    Math.Abs(ModConfig.SicknessStaminaRegenAtMax.Value - 0.4f) < 0.0001f &&
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f &&
                    ModConfig.StormDryEnvironment.Value == "Eikthyr");

                // ---- A LOCKED ADVANCED FILE. Its save fails, so nothing may be dropped and nothing
                //      stamped: the main file must still hold every old line for the next boot.
                File.Copy(fixture, mainPath, true);
                File.Delete(advPath);
                byte[] beforeA = File.ReadAllBytes(mainPath);
                var mainA = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var advA = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true, ThrowOnSave = true };
                ModConfig.Bind(mainA, advA);
                var mainAfterA = ConfigLedger.ParseIni(File.ReadAllLines(mainPath));
                Check("when the advanced file cannot be saved, the version is NOT stamped",
                    mainAfterA.TryGetValue(ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey), out string stampA) && stampA == "1");
                Check("and every old line is still in the main file for the next boot to carry",
                    mainAfterA.ContainsKey(ConfigLedger.Slot("1 - Core", "TickBudgetMs")) &&
                    mainAfterA.ContainsKey(ConfigLedger.Slot("9 - Plague", "PlagueGenesisMeanHours")));
                Check("while this boot still runs on the owner's values",
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f);
                Check("and the main file is not saved at all, so no setting is written in both its old and new place",
                    BytesMatch(mainPath, beforeA));

                var mainA2 = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var advA2 = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                ModConfig.Bind(mainA2, advA2);
                Check("the next boot, with the file free, finishes the migration and stamps it",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion &&
                    ConfigLedger.ParseIni(File.ReadAllLines(mainPath)).ContainsKey(ConfigLedger.Slot(ModConfig.WeatherSection, "StormsForceWeather")));
                Check("with the owner's tuning in the advanced file",
                    SameConfigText("0.5",
                        ConfigLedger.ParseIni(File.ReadAllLines(advPath))[ConfigLedger.Slot(ModConfig.HealthSection, "SicknessHealthRegenAtMax")]));

                // ---- A LOCKED MAIN FILE. The advanced file is saved first, the main one fails: the
                //      main file on disk must be exactly as it was, and the retry must converge.
                File.Copy(fixture, mainPath, true);
                File.Delete(advPath);
                byte[] before = File.ReadAllBytes(mainPath);
                var mainB = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true, ThrowOnSave = true };
                var advB = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                ModConfig.Bind(mainB, advB);
                byte[] after = File.ReadAllBytes(mainPath);
                bool untouched = before.Length == after.Length;
                for (int i = 0; untouched && i < before.Length; i++) untouched = before[i] == after[i];
                Check("when the main file cannot be saved, it is left byte-for-byte as it was", untouched);

                var mainB2 = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var advB2 = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                ModConfig.Bind(mainB2, advB2);
                Check("and the retry converges on the same result as a clean run",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion &&
                    Math.Abs(ModConfig.SicknessStaminaRegenAtMax.Value - 0.4f) < 0.0001f &&
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f);

                // ---- A SAVE THAT FAILS PART-WAY. BepInEx opens the file with append:false, which
                //      empties it, so a disk that fills mid-save leaves it truncated. The engine keeps
                //      the bytes and puts them back. (The locked-file tests above cannot see this:
                //      their save fails before anything is written.)
                File.Copy(fixture, mainPath, true);
                File.Delete(advPath);
                byte[] beforeTorn = File.ReadAllBytes(mainPath);
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true, ThrowMidSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true });
                Check("a main save that fails part-way leaves the file put back byte-for-byte, not truncated",
                    BytesMatch(mainPath, beforeTorn) && RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("put back exactly as it was"));
                Check("and wrath status says the migration runs again", ConfigMigration.LastSummary.Contains("could not be saved"));
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true });
                Check("and the retry converges, finding every setting already in its new place with the same value",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion &&
                    Math.Abs(ModConfig.SicknessStaminaRegenAtMax.Value - 0.4f) < 0.0001f &&
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f &&
                    !RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("in the new layout and") && !RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("at its shipped default"));

                File.Copy(fixture, mainPath, true);
                File.Delete(advPath);
                byte[] beforeTornAdv = File.ReadAllBytes(mainPath);
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true, ThrowMidSave = true });
                Check("an advanced save that fails part-way on a first migration leaves no half-written advanced file",
                    !File.Exists(advPath));
                Check("and the main file, never saved, is byte-for-byte as it was", BytesMatch(mainPath, beforeTornAdv));
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true });
                Check("and the retry converges", ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion && File.Exists(advPath) &&
                    Math.Abs(ModConfig.SicknessHealthRegenAtMax.Value - 0.5f) < 0.0001f);

                File.Copy(fixture, mainPath, true);                 // an interrupted run: advanced saved, main not
                byte[] beforeAdvKept = File.ReadAllBytes(advPath);
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true, ThrowMidSave = true });
                Check("an advanced file that already existed is put back byte-for-byte when its save fails part-way",
                    BytesMatch(advPath, beforeAdvKept));

                // ---- A SETTING IN BOTH PLACES. An interrupted run saved the advanced file and never
                //      stamped the main one; since then the new place was edited, by hand or through a
                //      config manager, and is what the mod has been reading. The review reproduced the
                //      old line silently reverting that edit.
                string cMain = Path.Combine(dir, "conflict.cfg");
                string cAdv = Path.Combine(dir, "conflict.advanced.cfg");
                File.WriteAllLines(cMain, new[] { "[1 - Core]", "TickBudgetMs = 5", "", "[9 - Plague]", "PlagueGenesisMeanHours = 3", "", "[Meta]", "ConfigVersion = 1" });
                File.WriteAllLines(cAdv, new[] { "[01 - General]", "TickBudgetMs = 8" });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = cMain, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = cAdv, WriteOnSave = true });
                Check("a value edited in the NEW place since an interrupted run is kept, not reverted by the old line",
                    Math.Abs(ModConfig.TickBudgetMs.Value - 8f) < 0.0001f && SameConfigText("8",
                        ConfigLedger.ParseIni(File.ReadAllLines(cAdv))[ConfigLedger.Slot(ModConfig.GeneralSection, "TickBudgetMs")]));
                Check("and the log names both values", RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("= '8' in the new layout") && RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("= '5' in the old one"));
                Check("and wrath status says a value was kept", ConfigMigration.LastSummary.Contains("already had a different value"));
                Check("while the rest still migrates: the old line is gone, a plain carry landed, the stamp is current",
                    !ConfigLedger.ParseIni(File.ReadAllLines(cMain)).ContainsKey(ConfigLedger.Slot("1 - Core", "TickBudgetMs")) &&
                    Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f && ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);

                File.WriteAllLines(cMain, new[] { "[1 - Core]", "TickBudgetMs = 5", "", "[Meta]", "ConfigVersion = 1" });
                File.WriteAllLines(cAdv, new[] { "[01 - General]", "TickBudgetMs = 2" });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = cMain, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = cAdv, WriteOnSave = true });
                Check("a new place holding only the shipped default does not beat the owner's old value",
                    Math.Abs(ModConfig.TickBudgetMs.Value - 5f) < 0.0001f && RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("at its shipped default") &&
                    !ConfigMigration.LastSummary.Contains("already had a different value"));

                File.WriteAllLines(cMain, new[] { "[1 - Core]", "TickBudgetMs = 5", "", "[Meta]", "ConfigVersion = 1" });
                File.WriteAllLines(cAdv, new[] { "[01 - General]", "TickBudgetMs = 5.0" });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = cMain, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = cAdv, WriteOnSave = true });
                Check("the same value in both places, however it is spelled, is simply carried with nothing to report",
                    Math.Abs(ModConfig.TickBudgetMs.Value - 5f) < 0.0001f &&
                    !RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("in the new layout and") && !RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("at its shipped default"));

                // ---- A FILE THE MIGRATION CANNOT READ. BepInEx has already read it (its ConfigFile
                //      loads in the constructor), then something takes it without sharing. Since
                //      0.28.0 renamed every section, a boot that cannot plan binds almost nothing, so
                //      it must write NOTHING and say so, rather than save defaults over the old lines.
                File.Copy(fixture, mainPath, true);
                File.Delete(advPath);
                byte[] beforeLocked = File.ReadAllBytes(mainPath);
                var mainL = new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true };
                var advL = new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true };
                mainL.HasOrphan("-", "-");   // load it now, as BepInEx's constructor does
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                using (new FileStream(mainPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    ModConfig.Bind(mainL, advL);
                Check("a config the migration cannot read is not written at all: main byte-for-byte, no advanced file",
                    BytesMatch(mainPath, beforeLocked) && !File.Exists(advPath));
                Check("and the failure is said out loud, in the log and in wrath status",
                    RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("could not read your config file") && ConfigMigration.LastSummary.Contains("FAILED"));
                Check("and BepInEx's save-on-change is still handed back", mainL.SaveOnConfigSet && advL.SaveOnConfigSet);
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true });
                Check("and the next boot, able to read it, migrates it properly",
                    ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion && Math.Abs(ModConfig.PlagueGenesisMeanHours.Value - 3f) < 0.001f);

                // ---- The backup note is judged per file: the main file's backup is the one an owner
                //      restores, and an advanced-file failure must not make the note deny it exists.
                File.Copy(fixture, mainPath, true);
                File.WriteAllLines(advPath, new[] { "[01 - General]", "TickBudgetMs = 2" });
                foreach (string bak in Directory.GetFiles(dir, "*.bak")) File.Delete(bak);
                Directory.CreateDirectory(advPath + ".v1.bak");    // a backup that cannot be written
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ModConfig.Bind(new ConfigFile { ConfigFilePath = mainPath, WriteOnSave = true },
                               new ConfigFile { ConfigFilePath = advPath, WriteOnSave = true });
                Check("the note names the main file's backup even when the advanced file's could not be written",
                    File.Exists(mainPath + ".v1.bak") &&
                    RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("backed up beside it as .v1.bak; the advanced file's own backup could not be written") &&
                    !RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("no backup of the config file"));
                Directory.Delete(advPath + ".v1.bak");

                // ---- EVERY SETTING, NON-DEFAULT. Most of the fixture's values equal their defaults,
                //      and a carry that silently failed on one of those would be invisible - the key
                //      would bind at the default it already held. So build an old-layout file in which
                //      every carried setting holds a value its default is not, and check each one.
                var chosen = new Dictionary<string, string>(StringComparer.Ordinal);   // destination -> text
                var synthetic = new List<string>();
                string lastSection = null;
                var byFrom = new List<ConfigLedger.MovedSlot>(plan.Moved);
                byFrom.Sort((a, b) => string.CompareOrdinal(a.From, b.From));
                foreach (var m in byFrom)
                {
                    ConfigLedger.SplitSlot(m.From, out _, out string fromSection, out string key);
                    ConfigLedger.SplitSlot(m.To, out bool toAdv, out string toSection, out _);
                    var probeEntry = (ConfigEntryBase)(toAdv ? advProbe : mainProbe)[new ConfigDefinition(toSection, key)];
                    string text = NotTheDefault(probeEntry);
                    chosen[m.To] = text;
                    if (fromSection != lastSection) { synthetic.Add("[" + fromSection + "]"); lastSection = fromSection; }
                    synthetic.Add(key + " = " + text);
                }
                synthetic.Add("[" + ConfigLedger.MetaSection + "]");
                synthetic.Add(ConfigLedger.VersionKey + " = 1");
                string synthPath = Path.Combine(dir, "every-setting.cfg");
                File.WriteAllLines(synthPath, synthetic);
                var synthMain = new ConfigFile { ConfigFilePath = synthPath, WriteOnSave = true };
                var synthAdv = new ConfigFile { ConfigFilePath = Path.Combine(dir, "every-setting.advanced.cfg"), WriteOnSave = true };
                ModConfig.Bind(synthMain, synthAdv);
                int arrived = 0;
                foreach (var kv in chosen)
                {
                    ConfigLedger.SplitSlot(kv.Key, out bool toAdv, out string section, out string key);
                    string landed = (toAdv ? synthAdv : synthMain)[new ConfigDefinition(section, key)].GetSerializedValue();
                    if (SameConfigText(kv.Value, landed)) arrived++;
                    else Console.WriteLine("      " + key + ": wrote '" + kv.Value + "', bound '" + landed + "'");
                }
                Check($"all {chosen.Count} settings, each set to a value its default is not, arrive intact ({arrived})",
                    arrived == chosen.Count && chosen.Count == plan.Moved.Count);

                // ---- A version 0 file two rungs behind: the 0.27.1 storm relocation must land in the
                //      version 2 sections, and must beat the plain carry of the same key.
                string v0Path = Path.Combine(dir, "v0.cfg");
                File.WriteAllLines(v0Path, new[]
                {
                    "[6 - Weather]",
                    "StormsForceWeather = true",
                    "StormForcedEnvironment = Eikthyr",
                    "StormRangeMeters = 128",
                    "",
                    "[4 - Systems]",
                    "EnableWind = false",
                });
                var v0 = new ConfigFile { ConfigFilePath = v0Path, WriteOnSave = true };
                var v0adv = new ConfigFile { ConfigFilePath = Path.Combine(dir, "v0.advanced.cfg"), WriteOnSave = true };
                ModConfig.Bind(v0, v0adv);
                Check("a version 0 Eikthyr owner still gets an all-dry Eikthyr storm after both rungs",
                    ModConfig.StormDryEnvironment.Value == "Eikthyr" &&
                    ModConfig.StormForcedEnvironment.Value == ConfigLedger.WetEnvironmentDefault &&
                    Math.Abs(ModConfig.StormDryChance.Value - 1f) < 0.0001f);
                Check("their storm range was carried into the advanced file",
                    Math.Abs(ModConfig.StormRangeMeters.Value - 128f) < 0.001f);
                Check("and their wind switch, which moved section AND file, kept its value", !ModConfig.EnableWind.Value);
                Check("the file ends at the current version", ModConfig.ConfigVersion.Value == ConfigLedger.CurrentVersion);

                // ---- Pieces of the plan, on hand-built snapshots.
                var leftover = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey), "1" },
                    { ConfigLedger.Slot("6 - Weather", "StormDurationSeconds"), "900" },
                    { ConfigLedger.Slot(ModConfig.WeatherSection, "StormDurationSeconds"), "300" },
                    { ConfigLedger.Slot("6 - Weather", "SomethingNobodyBinds"), "1" },
                    { ConfigLedger.Slot(ModConfig.WeatherSection, "SomethingElse"), "1" },
                    { ConfigLedger.AdvancedSlot(ModConfig.WeatherSection, "StormRangeMeters"), "200" },
                };
                var p2 = ConfigLedger.Plan(leftover, 1);
                Check("while an old line still exists it is the owner's value, and wins over a half-finished run's default",
                    p2.Moved.Exists(m => m.To == ConfigLedger.Slot(ModConfig.WeatherSection, "StormDurationSeconds") && m.Value == "900"));
                Check("and the planner hands the engine what the new place already held, so it can judge the two",
                    p2.Moved.Exists(m => m.To == ConfigLedger.Slot(ModConfig.WeatherSection, "StormDurationSeconds") && m.AlreadyThere == "300") &&
                    !plan.Moved.Exists(m => m.AlreadyThere != null));
                Check("a stray line under an emptied section is removed and named",
                    p2.Dropped.Exists(d => d.Slot == ConfigLedger.Slot("6 - Weather", "SomethingNobodyBinds") && d.Because == "nothing reads it") &&
                    ConfigLedger.Describe(p2).Contains("SomethingNobodyBinds"));
                Check("a stray line under a CURRENT section is left alone - it is not ours to judge",
                    !p2.Dropped.Exists(d => d.Slot.EndsWith("::SomethingElse", StringComparison.Ordinal)));
                Check("a line already in the advanced file is never moved or dropped",
                    !p2.Moved.Exists(m => m.From.StartsWith(ConfigLedger.AdvancedPrefix, StringComparison.Ordinal)) &&
                    !p2.Dropped.Exists(d => d.Slot.StartsWith(ConfigLedger.AdvancedPrefix, StringComparison.Ordinal)));

                // ---- A SECTION NAME BOTH LAYOUTS SHARE. "13 - Titles" was version 1's and is version
                //      2's, and the sweep of emptied sections once ate what the rung had just carried
                //      into it. Values chosen unlike their defaults, so a lost carry cannot hide.
                var shared = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { ConfigLedger.Slot(ConfigLedger.MetaSection, ConfigLedger.VersionKey), "1" },
                    { ConfigLedger.Slot("13 - Titles", "AnnounceTitles"), "false" },
                    { ConfigLedger.Slot("4 - Systems", "EnableTitle"), "false" },
                };
                var p3 = ConfigLedger.Plan(shared, 1);
                Check("a setting that never left a surviving section is neither carried nor dropped",
                    !p3.Moved.Exists(m => m.From.EndsWith("::AnnounceTitles", StringComparison.Ordinal)) &&
                    !p3.Dropped.Exists(d => d.Slot.EndsWith("::AnnounceTitles", StringComparison.Ordinal)));
                Check("a setting carried INTO a surviving section stays carried",
                    p3.Moved.Exists(m => m.To == ConfigLedger.Slot(ModConfig.TitlesSection, "EnableTitle") && m.Value == "false"));
                string sharedPath = Path.Combine(dir, "shared.cfg");
                File.WriteAllLines(sharedPath, new[]
                {
                    "[Meta]", "ConfigVersion = 1", "", "[13 - Titles]", "AnnounceTitles = false", "", "[4 - Systems]", "EnableTitle = false",
                });
                var sharedMain = new ConfigFile { ConfigFilePath = sharedPath, WriteOnSave = true };
                ModConfig.Bind(sharedMain, new ConfigFile());
                Check("and end to end both keep the owner's value", !ModConfig.AnnounceTitles.Value && !ModConfig.EnableTitle.Value);

                // ---- Drop's guard: a line this build still binds is never removed.
                var guarded = new ConfigFile();
                var guardedAdv = new ConfigFile();
                ModConfig.Bind(guarded, guardedAdv);
                var badDrop = new ConfigLedger.MigrationPlan();
                badDrop.Dropped.Add(new ConfigLedger.DroppedSlot { Slot = ConfigLedger.Slot(ModConfig.WeatherSection, "StormsForceWeather"), Because = "test" });
                RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Clear();
                ConfigMigration.Drop(guarded, guardedAdv, badDrop);
                Check("a drop naming a key this build still reads is refused, and the setting stays bound",
                    guarded.ContainsKey(new ConfigDefinition(ModConfig.WeatherSection, "StormsForceWeather")) &&
                    RavenIron.RagnaroksWrath.RagnaroksWrath.Log.Said("still reads that setting"));
            }
            finally
            {
                ModConfig.Bind(new ConfigFile(), new ConfigFile());
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>
        /// Text for a setting that its default is not, and that its allowed range will not clamp: a
        /// bool flipped, a number moved to whichever end of its range the default is not at, a string
        /// changed. InvariantCulture, and lower-case bools, as BepInEx writes them.
        /// </summary>
        private static string NotTheDefault(ConfigEntryBase entry)
        {
            object def = entry.DefaultValue;
            object range = ((ConfigDescription)entry.GetType().GetProperty("Description").GetValue(entry))?.AcceptableValues;
            object min = range?.GetType().GetField("MinValue").GetValue(range);
            object max = range?.GetType().GetField("MaxValue").GetValue(range);

            switch (def)
            {
                case bool b: return b ? "false" : "true";
                case int i:
                    if (range == null) return (i + 1).ToString(CultureInfo.InvariantCulture);
                    return ((int)max != i ? (int)max : (int)min).ToString(CultureInfo.InvariantCulture);
                case float f:
                    if (range == null) return (f + 1f).ToString(CultureInfo.InvariantCulture);
                    return (Math.Abs((float)max - f) > 1e-6f ? (float)max : (float)min).ToString(CultureInfo.InvariantCulture);
                case string str: return str + "_moved";
                default: return Convert.ToString(def, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Config text compared the way the migration compares it: numbers as numbers, the rest ignoring case.</summary>
        private static bool BytesMatch(string path, byte[] expected)
        {
            byte[] actual = File.ReadAllBytes(path);
            if (actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) return false;
            return true;
        }

        private static bool SameConfigText(string a, string b)
        {
            if (a == null || b == null) return false;
            if (double.TryParse(a.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(b.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                return Math.Abs(x - y) <= 1e-6 * Math.Max(1.0, Math.Abs(x));
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDescription(ConfigEntryBase entry)
        {
            var property = entry.GetType().GetProperty("Description");
            var description = property?.GetValue(entry) as ConfigDescription;
            return description?.Description ?? "";
        }


        // ---- StormLook --------------------------------------------------------------

        private static void StormLookTests()
        {
            Console.WriteLine("\nStormLook");

            // The whole point of the type. If liveness stops recognising either name, that
            // storm reads as NO STORM: every multiplier off, no lightning, nothing in the log.
            Check("the wet storm is recognised as a storm", StormLook.IsStorm(StormLook.WetEventName));
            Check("the dry storm is recognised as a storm", StormLook.IsStorm(StormLook.DryEventName));
            Check("somebody else's event is not our storm", !StormLook.IsStorm("random_event_foo"));
            Check("no active event is not a storm", !StormLook.IsStorm(""));
            Check("a null event name is not a storm", !StormLook.IsStorm(null));

            Check("the dry storm reads as dry", StormLook.IsDry(StormLook.DryEventName));
            Check("the wet storm does not read as dry", !StormLook.IsDry(StormLook.WetEventName));
            Check("a null event name does not read as dry", !StormLook.IsDry(null));

            // The original name must never change: vanilla serialises the active event into the
            // world file, so a rename strands every storm in flight at the moment of an update.
            Check("the wet storm keeps the name older worlds have saved",
                StormLook.WetEventName == "ragnarokswrath_devastating_storm");
            Check("the two storms have distinct names, the only thing vanilla replicates",
                StormLook.WetEventName != StormLook.DryEventName);

            // Boundaries, because a config file can hold anything and each of these is a silent
            // gameplay change rather than an error if it goes wrong.
            Check("chance 0 is always wet, even on a draw of 0",
                StormLook.Roll(0.0, 0f) == StormLook.WetEventName);
            Check("chance 0 is always wet on a high draw",
                StormLook.Roll(0.999, 0f) == StormLook.WetEventName);
            Check("chance 1 is always dry on a draw of 0",
                StormLook.Roll(0.0, 1f) == StormLook.DryEventName);
            Check("chance 1 is always dry on a high draw",
                StormLook.Roll(0.999, 1f) == StormLook.DryEventName);
            Check("a nonsense negative chance still yields a real storm",
                StormLook.Roll(0.5, -5f) == StormLook.WetEventName);
            Check("a chance above 1 clamps to always dry",
                StormLook.Roll(0.5, 2f) == StormLook.DryEventName);

            // NextDouble() returns [0,1), so a draw exactly at the chance must fall to WET —
            // otherwise chance 0.5 would be very slightly biased toward dry.
            Check("a draw exactly at the chance rolls wet, not dry",
                StormLook.Roll(0.5, 0.5f) == StormLook.WetEventName);
            Check("a draw just under the chance rolls dry",
                StormLook.Roll(0.4999, 0.5f) == StormLook.DryEventName);
            Check("a draw of 0 rolls dry at even odds",
                StormLook.Roll(0.0, 0.5f) == StormLook.DryEventName);
        }

        // ---- StormArea --------------------------------------------------------------

        private static void StormAreaTests()
        {
            Console.WriteLine("\nStormArea");

            var centre = new Vector3(100f, 30f, 100f);
            const float range = 96f;

            Check("a position at the centre is inside",
                StormArea.Contains(centre, range, centre));

            // XZ only. A storm reaches up the mountain above it - using Vector3.Distance here
            // would shrink the area for anyone climbing, and vanilla's banner would disagree.
            Check("height does not shrink the area",
                StormArea.Contains(centre, range, new Vector3(100f, 900f, 100f)));

            Check("a position beyond the range is outside",
                !StormArea.Contains(centre, range, new Vector3(100f + range + 1f, 30f, 100f)));

            // Strictly less-than, so the perimeter itself is out. Pinned because "<" vs "<=" is
            // the kind of boundary that differs silently between two implementations.
            Check("the perimeter is outside, not inside",
                !StormArea.Contains(centre, range, new Vector3(100f + range, 30f, 100f)));

            Check("just inside the perimeter is inside",
                StormArea.Contains(centre, range, new Vector3(100f + range - 0.5f, 30f, 100f)));

            // Vanilla's guard for anyone mid-teleport or otherwise off the map.
            Check("above the sky ceiling nothing is inside",
                !StormArea.Contains(centre, range, new Vector3(100f, StormArea.SkyCeiling + 1f, 100f)));

            Check("exactly at the sky ceiling is still inside",
                StormArea.Contains(centre, range, new Vector3(100f, StormArea.SkyCeiling, 100f)));

            // The storm event registers this as m_pauseIfNoPlayerInArea. True froze the clock of any
            // storm nobody stood in, so it never ended, blocked every later storm and burdened the
            // world forever (fixed in 0.27.5). Pinned because the old comment sold the freeze as a
            // vanilla feature worth inheriting, which is exactly how it gets switched back on.
            bool pauses = StormArea.ClockPausesWithNobodyInside;
            Check("a storm's clock runs on with nobody inside it", !pauses);

            // 3-4-5: proves the formula rather than merely its comparisons.
            Check($"DistanceXZ matches the flat distance ({StormArea.DistanceXZ(new Vector3(0f, 0f, 0f), new Vector3(3f, 999f, 4f)):F2})",
                Math.Abs(StormArea.DistanceXZ(new Vector3(0f, 0f, 0f), new Vector3(3f, 999f, 4f)) - 5f) < 0.0001f);

            Check("a zero-range storm contains nothing, not even its centre",
                !StormArea.Contains(centre, 0f, centre));
        }

        // ---- WindState --------------------------------------------------------------

        private static void WindStateTests()
        {
            Console.WriteLine("\nWindState");

            Check("wind passes through unchanged with no storm",
                Math.Abs(WindState.Combine(0.4f, 1f) - 0.4f) < 0.0001f);

            Check("a storm amplifies wind",
                Math.Abs(WindState.Combine(0.3f, 2f) - 0.6f) < 0.0001f);

            // Everything downstream multiplies by this, so it must stay in range.
            Check("amplified wind is clamped to 1",
                Math.Abs(WindState.Combine(0.8f, 5f) - 1f) < 0.0001f);

            Check("calm stays calm however strong the storm",
                Math.Abs(WindState.Combine(0f, 10f)) < 0.0001f);

            Check("a negative multiplier cannot drive wind below zero",
                Math.Abs(WindState.Combine(0.5f, -3f)) < 0.0001f);

            // A NaN survives every later multiply and silently poisons fire spread. Same reason
            // ZoneState.Clamp neutralises it rather than passing it on.
            Check("a NaN reading is neutralised, not propagated",
                !float.IsNaN(WindState.Combine(float.NaN, 2f))
                && !float.IsNaN(WindState.Combine(0.5f, float.NaN)));
        }

        // ---- FireScorch -------------------------------------------------------------

        private static void FireScorchTests()
        {
            Console.WriteLine("\nFireScorch");

            // Arson follows the igniter's own fire (review 2026-09-24): FireFront's igniter is one
            // global, so an unrelated fire elsewhere must not be billed to it.
            {
                var arson = new ArsonFootprint();
                var burning = new List<ZoneKey>();
                var billed = new List<ZoneKey>();

                burning.Add(new ZoneKey(0, 0));
                arson.Observe(111L, burning, billed);
                Check("the zones burning when an igniter appears are theirs",
                    billed.Count == 1 && billed[0] == new ZoneKey(0, 0));

                burning.Clear();
                burning.Add(new ZoneKey(0, 0));
                burning.Add(new ZoneKey(1, 1));     // spread, diagonal contact
                burning.Add(new ZoneKey(20, -5));   // an unrelated fire across the map
                arson.Observe(111L, burning, billed);
                Check("spread by contact is billed, a separate fire is not",
                    billed.Count == 2 && billed.Contains(new ZoneKey(1, 1))
                    && !billed.Contains(new ZoneKey(20, -5)));

                burning.Clear();
                burning.Add(new ZoneKey(3, 1));     // listed first: only reachable through (2,1)
                burning.Add(new ZoneKey(2, 1));
                burning.Add(new ZoneKey(20, -5));
                arson.Observe(111L, burning, billed);
                Check("a front that crossed two zones in one tick is followed, even after its origin went out",
                    billed.Count == 2 && billed.Contains(new ZoneKey(3, 1)) && billed.Contains(new ZoneKey(2, 1)));

                burning.Add(new ZoneKey(21, -5));
                arson.Observe(111L, burning, billed);
                Check("the unrelated fire's own spread stays unbilled",
                    !billed.Contains(new ZoneKey(20, -5)) && !billed.Contains(new ZoneKey(21, -5)));

                arson.Observe(0L, burning, billed);
                Check("a natural fire (igniter 0) bills nobody and forgets the footprint",
                    billed.Count == 0 && arson.ZoneCount == 0 && arson.Igniter == 0);

                burning.Clear();
                burning.Add(new ZoneKey(50, 50));
                arson.Observe(222L, burning, billed);
                Check("a new igniter starts a new footprint where their fire is",
                    arson.Igniter == 222L && billed.Count == 1 && billed[0] == new ZoneKey(50, 50));

                arson.Clear();
                arson.Observe(222L, burning, billed);
                Check("after every fire went out, the same igniter's next fire is seeded fresh",
                    billed.Count == 1 && arson.ZoneCount == 1);
            }

            // Per-fire blame from FireFront 1.0.2+: each fire bills only whoever lit it.
            {
                var blame = new List<KeyValuePair<ZoneKey, long>>();
                var pos = new List<Vector3>
                {
                    new Vector3(10f, 0f, 10f),     // zone (0,0), A
                    new Vector3(20f, 0f, 20f),     // zone (0,0), A again
                    new Vector3(30f, 0f, 30f),     // zone (0,0), B
                    new Vector3(700f, 0f, 700f),   // far away, natural
                    new Vector3(-700f, 0f, 5f),    // far away, B
                };
                var ign = new List<long> { 111L, 111L, 222L, 0L, 222L };
                bool ok = FireBlame.Collect(pos, ign, blame);
                var z0 = new ZoneKey(0, 0);
                Check("per-fire blame: one row per zone and igniter, natural fires bill nobody",
                    ok && blame.Count == 3
                    && blame.Contains(new KeyValuePair<ZoneKey, long>(z0, 111L))
                    && blame.Contains(new KeyValuePair<ZoneKey, long>(z0, 222L))
                    && blame.Contains(new KeyValuePair<ZoneKey, long>(ZoneKey.FromWorldPos(pos[4]), 222L))
                    && !blame.Exists(p => p.Key == ZoneKey.FromWorldPos(pos[3])));
                Check("A is never billed for B's fire far away",
                    !blame.Contains(new KeyValuePair<ZoneKey, long>(ZoneKey.FromWorldPos(pos[4]), 111L)));

                ign.RemoveAt(0);
                Check("lists that disagree book nothing",
                    !FireBlame.Collect(pos, ign, blame) && blame.Count == 0);
            }

            // Zone size is 64; positions 10m apart share a zone, 100m apart do not.
            var fires = new List<Vector3>
            {
                new Vector3(10f, 30f, 10f),
                new Vector3(20f, 30f, 20f),     // same zone as the first
                new Vector3(100f, 30f, 100f),   // a different zone
            };
            var zones = new List<ZoneKey>();
            FireScorch.CollectBurningZones(fires, zones);
            Check($"fires in one zone count once, fires apart count separately (got {zones.Count})",
                zones.Count == 2);

            // Binary per zone is the contract: severity already shows up as more zones burning,
            // and scaling by count as well would double-count it.
            zones.Clear();
            FireScorch.CollectBurningZones(new List<Vector3>
            {
                new Vector3(1f, 0f, 1f), new Vector3(2f, 0f, 2f), new Vector3(3f, 0f, 3f),
            }, zones);
            Check("forty fires in one zone are still one burning zone",
                zones.Count == 1);

            Check("no fires means no zones",
                (new Func<bool>(() => { zones.Clear();
                    FireScorch.CollectBurningZones(new List<Vector3>(), zones);
                    return zones.Count == 0; }))());

            // Rate is per minute; a 10s tick delivers a sixth of it.
            Check($"a 10s tick delivers a sixth of the per-minute rate ({FireScorch.ScorchDelta(0.06f, 10f):F4})",
                Math.Abs(FireScorch.ScorchDelta(0.06f, 10f) - 0.01f) < 0.0001f);

            Check("zero rate scorches nothing",
                FireScorch.ScorchDelta(0f, 10f) == 0f);

            Check("negative or NaN inputs scorch nothing rather than poisoning the store",
                FireScorch.ScorchDelta(-1f, 10f) == 0f
                && FireScorch.ScorchDelta(0.02f, -5f) == 0f
                && FireScorch.ScorchDelta(float.NaN, 10f) == 0f
                && FireScorch.ScorchDelta(0.02f, float.NaN) == 0f);
        }

        // ---- Plague -----------------------------------------------------------------

        private static void PlagueTests()
        {
            Console.WriteLine("\nPlague");

            const float Hour = 3600f;
            // recovery 0.02/h; growth passed in already season-multiplied, boost as stated.

            // Growth needs a seed. A pristine zone must stay pristine through any weather, or
            // the store stops being sparse and plague appears from nowhere.
            ZoneState pristine = BiomeDrift.Apply(default, Hour, 0.02f, 0f, 0f, 1f, 0.042f, 1f);
            Check("an unseeded zone grows no plague", pristine.IsDefault);

            // Spring at defaults: growth 0.042/h beats recovery 0.02/h.
            var seeded = new ZoneState { Plague = 0.05f };
            ZoneState spring = BiomeDrift.Apply(seeded, Hour, 0.02f, 0f, 0f, 1f, 0.042f, 0f);
            Check($"a seeded zone grows in spring ({spring.Plague:F4})",
                Math.Abs(spring.Plague - (0.05f - 0.02f + 0.042f)) < 0.0005f);

            // Winter at defaults: growth 0.015/h loses to recovery 0.02/h — the seasonal cure.
            ZoneState winter = BiomeDrift.Apply(seeded, Hour, 0.02f, 0f, 0f, 1f, 0.015f, 0f);
            Check($"winter is a net cure ({winter.Plague:F4})",
                winter.Plague < seeded.Plague);

            // ...and driving it through zero KILLS it: the gate is the post-decay value, so a
            // cured zone cannot be resurrected by the next warm season without re-infection.
            ZoneState cured = BiomeDrift.Apply(new ZoneState { Plague = 0.01f }, 2 * Hour,
                0.02f, 0f, 0f, 1f, 0.015f, 0f);
            Check("a cure that reaches zero is permanent, not a low ebb", cured.IsDefault);
            ZoneState afterCure = BiomeDrift.Apply(cured, Hour, 0.02f, 0f, 0f, 1f, 0.1f, 1f);
            Check("warm weather does not resurrect a cured zone", afterCure.IsDefault);

            // Corruption feeds plague: boost 1 with corruption 0.5 is x1.5 growth.
            var corrupt = new ZoneState { Plague = 0.05f, Corruption = 0.5f };
            var clean   = new ZoneState { Plague = 0.05f };
            float corruptGrown = BiomeDrift.Apply(corrupt, Hour, 0f, 0f, 0f, 1f, 0.04f, 1f).Plague;
            float cleanGrown   = BiomeDrift.Apply(clean,   Hour, 0f, 0f, 0f, 1f, 0.04f, 1f).Plague;
            Check($"corruption accelerates plague ({corruptGrown:F4} vs {cleanGrown:F4})",
                corruptGrown > cleanGrown
                && Math.Abs((corruptGrown - 0.05f) - 1.5f * (cleanGrown - 0.05f)) < 0.0005f);

            // Spread targeting: only zones at threshold seed, only pristine neighbours, once.
            var hot = new List<KeyValuePair<ZoneKey, float>>
            {
                new KeyValuePair<ZoneKey, float>(new ZoneKey(0, 0), 0.6f),   // source
                new KeyValuePair<ZoneKey, float>(new ZoneKey(1, 0), 0.6f),   // adjacent source
                new KeyValuePair<ZoneKey, float>(new ZoneKey(5, 5), 0.1f),   // below threshold
            };
            var infected = new HashSet<ZoneKey> { new ZoneKey(0, 0), new ZoneKey(1, 0), new ZoneKey(5, 5) };
            var targets = new List<ZoneKey>();
            PlagueSpread.CollectSpreadTargets(hot, 0.5f, infected, targets);

            // Two sources in a row: 8 orthogonal neighbours minus each other, minus the shared
            // duplicates — (−1,0),(0,1),(0,−1),(2,0),(1,1),(1,−1) = 6. The weak zone adds none.
            Check($"frontier is uninfected orthogonal neighbours of hot zones only (got {targets.Count})",
                targets.Count == 6
                && !targets.Contains(new ZoneKey(0, 0))
                && !targets.Contains(new ZoneKey(1, 0))
                && !targets.Contains(new ZoneKey(4, 5)));

            Check("a zone between two hot sources is listed once",
                (new Func<bool>(() => {
                    var two = new List<KeyValuePair<ZoneKey, float>>
                    {
                        new KeyValuePair<ZoneKey, float>(new ZoneKey(0, 0), 0.9f),
                        new KeyValuePair<ZoneKey, float>(new ZoneKey(2, 0), 0.9f),
                    };
                    var inf = new HashSet<ZoneKey> { new ZoneKey(0, 0), new ZoneKey(2, 0) };
                    var t = new List<ZoneKey>();
                    PlagueSpread.CollectSpreadTargets(two, 0.5f, inf, t);
                    int middle = 0;
                    foreach (ZoneKey z in t) if (z == new ZoneKey(1, 0)) middle++;
                    return middle == 1;
                }))());

            Check("no hot zones means no frontier",
                (new Func<bool>(() => {
                    var t = new List<ZoneKey>();
                    PlagueSpread.CollectSpreadTargets(
                        new List<KeyValuePair<ZoneKey, float>>
                        { new KeyValuePair<ZoneKey, float>(new ZoneKey(3, 3), 0.49f) },
                        0.5f, new HashSet<ZoneKey> { new ZoneKey(3, 3) }, t);
                    return t.Count == 0;
                }))());
        }

        // ---- WorldState -------------------------------------------------------------

        private static void WorldStateTests()
        {
            Console.WriteLine("\nWorldState");

            var zones = new List<KeyValuePair<ZoneKey, ZoneState>>
            {
                new KeyValuePair<ZoneKey, ZoneState>(new ZoneKey(0, 0),
                    new ZoneState { Plague = 0.6f, Corruption = 0.5f }),
                new KeyValuePair<ZoneKey, ZoneState>(new ZoneKey(1, 0),
                    new ZoneState { Scorch = 0.2f }),
                new KeyValuePair<ZoneKey, ZoneState>(new ZoneKey(2, 0),
                    new ZoneState { Frost = 0.4f }),
            };
            BiomeMetrics m = BiomeMetrics.Compute(zones);

            Check($"metrics count what they should ({m.TrackedZones} tracked, {m.InfectedZones} infected)",
                m.TrackedZones == 3 && m.InfectedZones == 1);

            // Burden is the weighted sum: 0.6*1.5 + 0.5*1 + 0.2*1 + 0.4*0.5 = 1.8.
            Check($"burden weights each field as documented ({m.Burden():F3})",
                Math.Abs(m.Burden() - 1.8f) < 0.0005f);

            Check("an empty store carries no burden",
                BiomeMetrics.Compute(new List<KeyValuePair<ZoneKey, ZoneState>>()).Burden() == 0f);

            // Worsening is prompt: at the threshold, the condition turns.
            Check("ailing begins at its threshold",
                WorldConditionRules.Derive(4f, WorldCondition.Stable, 0.25f, 4f, 12f)
                    == WorldCondition.Ailing);

            Check("stricken begins at its threshold",
                WorldConditionRules.Derive(12f, WorldCondition.Ailing, 0.25f, 4f, 12f)
                    == WorldCondition.Stricken);

            // Improvement needs the hysteresis band. This is the no-flap guarantee: a burden
            // hovering exactly at a boundary announces once, not every pass forever.
            Check("just under the threshold does NOT improve (hysteresis)",
                WorldConditionRules.Derive(3.9f, WorldCondition.Ailing, 0.25f, 4f, 12f)
                    == WorldCondition.Ailing);

            Check("clearing the hysteresis band improves",
                WorldConditionRules.Derive(3.3f, WorldCondition.Ailing, 0.25f, 4f, 12f)
                    == WorldCondition.Stable);

            // A collapse can improve several steps in one pass, but only through boundaries it
            // has genuinely cleared.
            Check("a full collapse improves straight to flourishing",
                WorldConditionRules.Derive(0.1f, WorldCondition.Stricken, 0.25f, 4f, 12f)
                    == WorldCondition.Flourishing);

            Check("a partial collapse stops at the band it has not cleared",
                WorldConditionRules.Derive(3.8f, WorldCondition.Stricken, 0.25f, 4f, 12f)
                    == WorldCondition.Ailing);

            // The calm end has the same protection: flourishing is not re-entered at its
            // ceiling, only below the band under it.
            Check("flourishing needs its own band cleared",
                WorldConditionRules.Derive(0.24f, WorldCondition.Stable, 0.25f, 4f, 12f)
                    == WorldCondition.Stable
                && WorldConditionRules.Derive(0.2f, WorldCondition.Stable, 0.25f, 4f, 12f)
                    == WorldCondition.Flourishing);
        }

        // ---- Ecology ----------------------------------------------------------------

        private static void EcologyTests()
        {
            Console.WriteLine("\nEcology");

            const float Hour = 3600f;

            Check("clean land does not corrupt",
                EcologyPressure.Apply(new ZoneState { Plague = 0.29f, Scorch = 0.29f },
                    Hour, 0.01f, 0.3f, 0.3f).Corruption == 0f);

            // At the threshold exactly, pressure is a trickle (quarter rate), not full-on: the
            // effect ramps in rather than switching at one epsilon past the line.
            ZoneState atLine = EcologyPressure.Apply(new ZoneState { Plague = 0.3f },
                Hour, 0.01f, 0.3f, 0.3f);
            Check($"pressure starts as a trickle at the threshold ({atLine.Corruption:E2})",
                Math.Abs(atLine.Corruption - 0.0025f) < 0.0001f);

            // Plague at double the threshold: excess 1, so rate x1.25.
            ZoneState hot = EcologyPressure.Apply(new ZoneState { Plague = 0.6f },
                Hour, 0.01f, 0.3f, 0.3f);
            Check($"pressure scales with how far past the line ({hot.Corruption:E2})",
                Math.Abs(hot.Corruption - 0.0125f) < 0.0001f);

            Check("scorch pressure corrupts too",
                EcologyPressure.Apply(new ZoneState { Scorch = 0.5f },
                    Hour, 0.01f, 0.3f, 0.3f).Corruption > 0f);

            Check("zero rate or zero time changes nothing",
                EcologyPressure.Apply(new ZoneState { Plague = 0.9f }, 0f, 0.01f, 0.3f, 0.3f).Corruption == 0f
                && EcologyPressure.Apply(new ZoneState { Plague = 0.9f }, Hour, 0f, 0.3f, 0.3f).Corruption == 0f);

            Check("NaN inputs corrupt nothing rather than poisoning the store",
                EcologyPressure.Apply(new ZoneState { Plague = 0.9f }, float.NaN, 0.01f, 0.3f, 0.3f).Corruption == 0f
                && !float.IsNaN(EcologyPressure.Apply(new ZoneState { Plague = 0.9f }, Hour, float.NaN, 0.3f, 0.3f).Corruption));
        }

        // ---- Titles -----------------------------------------------------------------

        private static void TitleTests()
        {
            Console.WriteLine("\nTitles");

            // Rising edges: two conditions that hold together for many ticks award once, not
            // every tick (review 2026-09-24: Plaguewalker and Winterborn swapped every 10 s).
            {
                var plagueHeld = new HashSet<long>();
                var winterHeld = new HashSet<long>();
                int awards = 0;
                string last = null;
                for (int tick = 0; tick < 6; tick++)
                {
                    string earned = null;
                    if (TitleEdge.Rises(plagueHeld, 7L, true)) earned = "Plaguewalker";
                    if (TitleEdge.Rises(winterHeld, 7L, true)) earned = "Winterborn";
                    if (earned != null) { awards++; last = earned; }
                }
                Check($"two titles held together award once, latest wins (awards {awards}, '{last}')",
                    awards == 1 && last == "Winterborn");

                Check("a condition that drops re-arms its title",
                    !TitleEdge.Rises(plagueHeld, 7L, false) && TitleEdge.Rises(plagueHeld, 7L, true));
                Check("edges are per player",
                    TitleEdge.Rises(plagueHeld, 8L, true) && !TitleEdge.Rises(plagueHeld, 7L, true));
            }

            // Winterborn's clock restarts every winter (fix review 2026-09-24): a server up
            // through two winters must not award it the moment the second one begins.
            {
                var clock = new Dictionary<long, float>();
                var held = new HashSet<long>();
                const float need = 1800f;
                int awards = 0;
                bool Tick(bool winter, float dt)
                {
                    float s = TitleEdge.WinterSeconds(clock, 7L, winter, dt);
                    bool rose = TitleEdge.Rises(held, 7L, winter && s >= need);
                    if (rose) awards++;
                    return rose;
                }

                for (int i = 0; i < 200; i++) Tick(true, 10f);          // 2000 s of winter one
                Check("Winterborn is earned once in a long winter", awards == 1);
                for (int i = 0; i < 50; i++) Tick(false, 10f);          // spring to autumn
                Check("the winter clock is empty outside winter", clock.Count == 0);
                bool instant = Tick(true, 10f);                         // winter two begins
                Check("a second winter does not award on its first tick", !instant && awards == 1);
                for (int i = 0; i < 179; i++) Tick(true, 10f);          // 1800 s into winter two
                Check("a second winter awards again after its own full stretch", awards == 2);
            }

            string dir = Path.Combine(Path.GetTempPath(), "rw_titles_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "titles.dat");

            try
            {
                TitleStore.OverridePath = path;

                TitleStore.Load();
                Check("a fresh world has no titles and a usable store",
                    TitleStore.IsLoaded && TitleStore.Count == 0);

                TitleStore.Set(123456789L, "Stormrider");
                TitleStore.Set(987654321L, "Winterborn");
                TitleStore.Set(123456789L, "Plaguewalker");   // latest earned wins
                TitleStore.Load();
                Check($"titles survive a save/load round-trip, latest wins (got '{TitleStore.Get(123456789L)}')",
                    TitleStore.Count == 2 && TitleStore.Get(123456789L) == "Plaguewalker");

                // The placeholder id a dedicated server's own profile would produce.
                TitleStore.Set(0L, "Stormrider");
                Check("player id 0 is never recorded", TitleStore.Get(0L) == null);

                TitleStore.Set(987654321L, null);
                TitleStore.Load();
                Check("clearing a title removes the row entirely", TitleStore.Count == 1);

                byte[] raw = File.ReadAllBytes(path);
                Check("the title store is written without a BOM",
                    raw.Length >= 3 && !(raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF));

                File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x00, 0xFF });
                TitleStore.Load();
                Check("a corrupt title store degrades to empty and is quarantined",
                    TitleStore.IsLoaded && TitleStore.Count == 0
                    && File.Exists(path + ".corrupt") && !File.Exists(path));

                Check("a titled suffix renders on its own smaller line",
                    TitleFormat.Suffix("Stormrider").StartsWith("\n")
                    && TitleFormat.Suffix("Stormrider").Contains("Stormrider"));

                Check("no title means no suffix at all",
                    TitleFormat.Suffix(null) == "" && TitleFormat.Suffix("  ") == "");
            }
            finally
            {
                TitleStore.OverridePath = null;
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---- Fog --------------------------------------------------------------------

        private static void FogTests()
        {
            Console.WriteLine("\nFog");

            // The floor is the discovery mechanic: fresh seeds (0.05) must not telegraph the
            // frontier the tick it spreads.
            Check("a fresh seed shows no fog",
                FogMath.EmissionFor(0.05f, 1f) == 0f);

            Check("below the visible floor shows no fog",
                FogMath.EmissionFor(0.1499f, 1f) == 0f);

            Check($"full plague fogs at the full rate ({FogMath.EmissionFor(1f, 1f):F1})",
                Math.Abs(FogMath.EmissionFor(1f, 1f) - FogMath.FullRate) < 0.01f);

            float half = FogMath.EmissionFor(0.575f, 1f);
            Check($"halfway up the ramp is half the rate ({half:F1})",
                Math.Abs(half - FogMath.FullRate / 2f) < 0.5f);

            Check("density scales and is capped",
                FogMath.EmissionFor(1f, 2f) > FogMath.EmissionFor(1f, 1f)
                && Math.Abs(FogMath.EmissionFor(1f, 99f) - FogMath.FullRate * 4f) < 0.01f);

            Check("zero density, NaN plague and NaN density all mean no fog",
                FogMath.EmissionFor(1f, 0f) == 0f
                && FogMath.EmissionFor(float.NaN, 1f) == 0f
                && FogMath.EmissionFor(1f, float.NaN) == 0f);

            // Ash: FogMath's shape at the burn scars — but with a non-zero BASE at the
            // floor, because a ramp-from-zero rendered real scars invisible (0.14.0).
            Check("trace scorch dusts nothing, real burns visibly show, NaN shows nothing",
                AshMath.EmissionFor(0.09f, 1f) == 0f
                && Math.Abs(AshMath.EmissionFor(0.1f, 1f) - AshMath.BaseRate) < 0.01f
                && Math.Abs(AshMath.EmissionFor(1f, 1f) - AshMath.FullRate) < 0.01f
                && AshMath.EmissionFor(float.NaN, 1f) == 0f
                && AshMath.EmissionFor(1f, 0f) == 0f);

            float halfAsh = AshMath.EmissionFor(0.55f, 1f);
            Check($"ash ramps linearly from base to full ({halfAsh:F1})",
                Math.Abs(halfAsh - (AshMath.BaseRate + (AshMath.FullRate - AshMath.BaseRate) * 0.5f)) < 0.5f);
        }

        // ---- Exposure (HealthSystem's pure half) --------------------------------------

        private static void ExposureTests()
        {
            Console.WriteLine("\nExposure");

            // Predict before reading: 60s on plague 0.95 at 30min-to-max is
            // 0.95 / (30*60) * 60 = 0.031667 exposure.
            float oneMinute = ExposureMath.Accrue(0f, 0.95f, 30f, false, 0.5f, 60f);
            Check($"a minute in the outbreak matches prediction ({oneMinute:F5})",
                Math.Abs(oneMinute - 0.95f / 30f) < 1e-5f);

            Check("below the fog floor nothing accrues — the sickness keeps the fog's secret",
                ExposureMath.Accrue(0.2f, 0.1499f, 30f, false, 0.5f, 600f) == 0.2f);

            float resisted = ExposureMath.Accrue(0f, 0.95f, 30f, true, 0.5f, 60f);
            Check($"poison resistance halves the taking hold ({resisted:F5})",
                Math.Abs(resisted - 0.95f / 60f) < 1e-5f);

            float e = 0f;
            for (int i = 0; i < 30 * 12; i++)   // 30 minutes of 5s ticks at full plague
                e = ExposureMath.Accrue(e, 1f, 30f, false, 0.5f, 5f);
            Check($"full plague maxes out in the configured minutes ({e:F3})",
                Math.Abs(e - 1f) < 1e-3f);

            Check("exposure is capped at 1",
                ExposureMath.Accrue(0.999f, 1f, 5f, false, 0.5f, 3600f) == 1f);

            float drained = ExposureMath.Decay(1f, 20f, false, 2f, 60f);
            Check($"a minute of recovery matches prediction ({drained:F3})",
                Math.Abs(drained - 0.95f) < 1e-5f);

            float rested = ExposureMath.Decay(1f, 20f, true, 2f, 60f);
            Check($"rested doubles the drain ({rested:F3})",
                Math.Abs(rested - 0.90f) < 1e-5f);

            Check("recovery terminates at exactly zero, not almost-zero",
                ExposureMath.Decay(0.001f, 20f, false, 2f, 60f) == 0f);

            Check("NaN inputs are neutralised, not propagated",
                !float.IsNaN(ExposureMath.Accrue(float.NaN, float.NaN, 30f, false, float.NaN, 60f))
                && ExposureMath.Decay(float.NaN, 20f, false, 2f, 60f) == 0f
                && ExposureMath.TierFor(float.NaN, 0.25f, 0.5f, 0.8f) == 0
                && ExposureMath.StaminaRegenMultiplier(float.NaN, 0.25f, 0.85f, 0.3f) == 1f);

            Check("tiers begin at their thresholds, inclusive",
                ExposureMath.TierFor(0.2f, 0.25f, 0.5f, 0.8f) == 0
                && ExposureMath.TierFor(0.25f, 0.25f, 0.5f, 0.8f) == 1
                && ExposureMath.TierFor(0.5f, 0.25f, 0.5f, 0.8f) == 2
                && ExposureMath.TierFor(0.8f, 0.25f, 0.5f, 0.8f) == 3);

            // The owner's palette call: stamina fails FIRST. At exposure 0.4 (past tier 1,
            // short of tier 2) stamina already sags while health regen is untouched.
            Check("stamina fails before health regen",
                ExposureMath.StaminaRegenMultiplier(0.4f, 0.25f, 0.85f, 0.3f) < 1f
                && ExposureMath.HealthRegenMultiplier(0.4f, 0.5f, 0.8f, 0.38f) == 1f);

            // THE 0.8.0 REGRESSION, pinned. A ramp starting at 1.0 on the threshold gave
            // x0.98 here — announced as "a sickness takes root in you" and imperceptible in
            // the hands. Crossing a tier must be FELT on the pass it is announced.
            float atTier1 = ExposureMath.StaminaRegenMultiplier(0.25f, 0.25f, 0.85f, 0.3f);
            Check($"crossing tier 1 is felt immediately, not approached ({atTier1:F2})",
                Math.Abs(atTier1 - 0.85f) < 1e-4f);

            float atTier2 = ExposureMath.HealthRegenMultiplier(0.5f, 0.5f, 0.8f, 0.38f);
            Check($"crossing tier 2 lands the wound half at once ({atTier2:F2})",
                Math.Abs(atTier2 - 0.8f) < 1e-4f);

            // The three points the owner agreed to, reproduced by one ramp off the step.
            float s50 = ExposureMath.StaminaRegenMultiplier(0.5f, 0.25f, 0.85f, 0.3f);
            float s80 = ExposureMath.StaminaRegenMultiplier(0.8f, 0.25f, 0.85f, 0.3f);
            Check($"the agreed stamina table holds at every tier (0.85 / {s50:F2} / {s80:F2})",
                Math.Abs(s50 - 0.667f) < 0.02f && Math.Abs(s80 - 0.45f) < 0.02f);

            float h80 = ExposureMath.HealthRegenMultiplier(0.8f, 0.5f, 0.8f, 0.38f);
            Check($"the agreed health table holds too (0.80 / {h80:F2})",
                Math.Abs(h80 - 0.55f) < 0.02f);

            Check("the ramp still reaches its floor at full exposure",
                Math.Abs(ExposureMath.StaminaRegenMultiplier(1f, 0.25f, 0.85f, 0.3f) - 0.3f) < 1e-4f
                && Math.Abs(ExposureMath.HealthRegenMultiplier(1f, 0.5f, 0.8f, 0.38f) - 0.38f) < 1e-4f);

            Check("below its start tier every multiplier is exactly 1",
                ExposureMath.StaminaRegenMultiplier(0.2499f, 0.25f, 0.85f, 0.3f) == 1f
                && ExposureMath.HealthRegenMultiplier(0.45f, 0.5f, 0.8f, 0.38f) == 1f);

            Check("quantized sync fires on a full step or a zero transition, not on dust",
                !ExposureMath.QuantizedDiffer(0.5f, 0.505f)
                && ExposureMath.QuantizedDiffer(0.5f, 0.511f)
                && ExposureMath.QuantizedDiffer(0.005f, 0f)
                && !ExposureMath.QuantizedDiffer(0f, 0f));
        }

        // ---- HealthStore ---------------------------------------------------------------

        private static void HealthStoreTests()
        {
            Console.WriteLine("\nHealthStore");

            string dir = Path.Combine(Path.GetTempPath(), "rw_health_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "health.dat");

            try
            {
                HealthStore.OverridePath = path;

                HealthStore.Load();
                Check("a fresh world has no exposure and a usable store",
                    HealthStore.IsLoaded && HealthStore.Count == 0);

                HealthStore.SaveIfDirty();
                Check("a clean store writes nothing", !File.Exists(path));

                HealthStore.Set(123456789L, 0.4321f);
                HealthStore.Set(987654321L, 0.05f);
                HealthStore.SaveIfDirty();
                HealthStore.Load();
                Check($"exposure survives a round-trip through the shipping writer (got {HealthStore.Get(123456789L):F4})",
                    HealthStore.Count == 2 && Math.Abs(HealthStore.Get(123456789L) - 0.4321f) < 1e-4f);

                HealthStore.Set(0L, 0.9f);
                Check("player id 0 is never recorded", HealthStore.Get(0L) == 0f);

                HealthStore.Set(987654321L, 0f);
                HealthStore.SaveIfDirty();
                HealthStore.Load();
                Check("a recovered player's row is removed entirely", HealthStore.Count == 1);

                byte[] raw = File.ReadAllBytes(path);
                Check("the health store is written without a BOM",
                    raw.Length >= 3 && !(raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF));

                File.WriteAllLines(path, new[] { "version\t1", "555\t1.7" });
                HealthStore.Load();
                Check($"an out-of-range value in the file is clamped on read ({HealthStore.Get(555L):F1})",
                    HealthStore.Get(555L) == 1f);

                File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x00, 0xFF });
                HealthStore.Load();
                Check("a corrupt health store degrades to empty and is quarantined",
                    HealthStore.IsLoaded && HealthStore.Count == 0
                    && File.Exists(path + ".corrupt") && !File.Exists(path));
            }
            finally
            {
                HealthStore.OverridePath = null;
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---- Consequence ---------------------------------------------------------------

        private static void ConsequenceTests()
        {
            Console.WriteLine("\nConsequence");

            Check("plagued OR scorched ground is barren, inclusive at each threshold",
                ConsequenceMath.Barren(0.4f, 0f, 0.4f, 0.5f)
                && ConsequenceMath.Barren(0f, 0.5f, 0.4f, 0.5f)
                && !ConsequenceMath.Barren(0.39f, 0.49f, 0.4f, 0.5f));

            Check("blight for crops is the WORSE of plague and corruption",
                ConsequenceMath.WithersCrops(0.7f, 0f, 0.6f)
                && ConsequenceMath.WithersCrops(0f, 0.65f, 0.6f)
                && !ConsequenceMath.WithersCrops(0.3f, 0.3f, 0.6f));

            Check("wildlife sickens at its threshold and not below",
                ConsequenceMath.SickensWildlife(0.4f, 0.4f)
                && !ConsequenceMath.SickensWildlife(0.399f, 0.4f));

            float atFull = ConsequenceMath.EmpowerLevelUpMultiplier(1f, 0.5f, 6f);
            float mid = ConsequenceMath.EmpowerLevelUpMultiplier(0.75f, 0.5f, 6f);
            Check($"empower odds ramp from 1 at threshold to the dial at full ({mid:F1} / {atFull:F1})",
                ConsequenceMath.EmpowerLevelUpMultiplier(0.49f, 0.5f, 6f) == 1f
                && Math.Abs(mid - 3.5f) < 1e-4f
                && Math.Abs(atFull - 6f) < 1e-4f);

            Check("NaN inputs empower nothing, sicken nothing, wither nothing",
                ConsequenceMath.EmpowerLevelUpMultiplier(float.NaN, 0.5f, 6f) == 1f
                && !ConsequenceMath.SickensWildlife(float.NaN, 0.4f)
                && !ConsequenceMath.Barren(float.NaN, float.NaN, 0.4f, 0.5f)
                && !ConsequenceMath.WithersCrops(float.NaN, float.NaN, 0.6f));

            var hot = new ZoneState { Plague = 0.7f, Corruption = 0.6f, Scorch = 0f };
            ConsequenceFlags flags = ConsequenceMath.FlagsFor(hot, 0.4f, 0.5f, 0.4f, 0.5f, 0.6f);
            Check($"a hot zone earns every applicable flag ({flags})",
                flags == (ConsequenceFlags.Barren | ConsequenceFlags.Empowered
                        | ConsequenceFlags.Sickening | ConsequenceFlags.Withering));

            Check("a clean zone earns none",
                ConsequenceMath.FlagsFor(default, 0.4f, 0.5f, 0.4f, 0.5f, 0.6f)
                    == ConsequenceFlags.None);

            // Instantiated objects are named "Deer(Clone)"; the match must be exact after
            // truncation — "Boar" quietly sickening a "BoarPiggy" breeding pen is the bug
            // this test exists to forbid.
            Check("passive list matches clones exactly, case-insensitively, never by prefix",
                ConsequenceMath.IsPassivePrefab("Deer(Clone)", "Deer,Boar,Hare")
                && ConsequenceMath.IsPassivePrefab("deer", " Deer , Boar ")
                && !ConsequenceMath.IsPassivePrefab("BoarPiggy(Clone)", "Deer,Boar,Hare")
                && !ConsequenceMath.IsPassivePrefab("Deer(Clone)", "")
                && !ConsequenceMath.IsPassivePrefab("", "Deer"));

            // The wild answers a war (Patch_SpawnWar): the chance rises, it never falls, and
            // nothing moves at peace. Only the chance: the cap is vanilla's and is not an input.
            Check("at peace a wildlife spawner rolls vanilla's own chance",
                ConsequenceMath.WarSpawnChance(20f, 0f, 100f) == 20f
                && ConsequenceMath.WarSpawnChance(20f, -1f, 100f) == 20f);
            Check("at war it rolls the war chance, storm-escalated or not",
                ConsequenceMath.WarSpawnChance(20f, 1f, 100f) == 100f
                && ConsequenceMath.WarSpawnChance(20f, 2f, 60f) == 60f);
            Check("a war chance below vanilla's never lowers it: the wild answers, it does not retreat",
                ConsequenceMath.WarSpawnChance(50f, 1f, 30f) == 50f);
            Check("a war chance of 0 turns the answer off",
                ConsequenceMath.WarSpawnChance(20f, 1f, 0f) == 20f);
            Check("a war chance past 100 is held at 100",
                ConsequenceMath.WarSpawnChance(20f, 1f, 250f) == 100f);
            Check("NaN war or NaN chance leaves vanilla's chance alone",
                ConsequenceMath.WarSpawnChance(20f, float.NaN, 100f) == 20f
                && ConsequenceMath.WarSpawnChance(20f, 1f, float.NaN) == 20f);
        }

        // ---- Rivalry (phase A: the influence ledger) -----------------------------------

        private static void RivalryTests()
        {
            Console.WriteLine("\nRivalry");

            // Decay: exactly half after one half-life, compounding correctly, disabled at 0.
            float half = RivalryMath.DecayFactor(48f, 48f * 3600f);
            Check($"one half-life fades a row to exactly half ({half:F4})",
                Math.Abs(half - 0.5f) < 1e-4f);

            float quarter = RivalryMath.DecayFactor(48f, 96f * 3600f);
            Check($"two half-lives fade to a quarter ({quarter:F4})",
                Math.Abs(quarter - 0.25f) < 1e-4f);

            Check("zero elapsed, zero half-life and NaN all mean no decay",
                RivalryMath.DecayFactor(48f, 0f) == 1f
                && RivalryMath.DecayFactor(0f, 3600f) == 1f
                && RivalryMath.DecayFactor(float.NaN, 3600f) == 1f);

            // Healing care: only decreases book, and the split is even.
            Check("healing books care, worsening books nothing",
                RivalryMath.CareFromHealing(1.0f, 0.7f, 1f) > 0f
                && RivalryMath.CareFromHealing(0.7f, 1.0f, 1f) == 0f
                && Math.Abs(RivalryMath.CareFromHealing(1.0f, 0.7f, 2f) - 0.6f) < 1e-4f);

            Check("care splits evenly and nobody splits with zero people",
                Math.Abs(RivalryMath.SplitAmong(0.6f, 3) - 0.2f) < 1e-5f
                && RivalryMath.SplitAmong(0.6f, 0) == 0f
                && RivalryMath.SplitAmong(float.NaN, 2) == 0f);

            Check("zone damage sums every field and neutralises NaN",
                Math.Abs(RivalryMath.ZoneDamage(new ZoneState
                    { Fertility = 0.1f, Corruption = 0.2f, Scorch = 0.3f, Frost = 0.1f, Plague = 0.3f }) - 1.0f) < 1e-5f
                && RivalryMath.ZoneDamage(new ZoneState { Plague = float.NaN, Frost = 0.5f }) == 0.5f);

            Check("the watermark admits only genuinely newer plants",
                RivalryMath.IsNewPlant(100, 50)
                && !RivalryMath.IsNewPlant(50, 50)
                && !RivalryMath.IsNewPlant(0, 0));

            // Phase B: the grudge and its teeth.
            Check("grudge is net harm, clamped, and care genuinely mollifies",
                Math.Abs(RivalryMath.GrudgeFor(0.5f, 0.2f, 1f) - 0.3f) < 1e-5f
                && RivalryMath.GrudgeFor(0.2f, 0.5f, 1f) == 0f
                && RivalryMath.GrudgeFor(5f, 0f, 1f) == 1f
                && RivalryMath.GrudgeFor(0.25f, 0f, 2f) == 0.5f
                && RivalryMath.GrudgeFor(float.NaN, 0f, 1f) == 0f);

            Check("a full grudge halves recovery and doubles pressure, never more",
                Math.Abs(RivalryMath.GrudgedRecovery(0.02f, 1f) - 0.01f) < 1e-6f
                && Math.Abs(RivalryMath.GrudgedPressure(0.03f, 1f) - 0.06f) < 1e-6f
                && RivalryMath.GrudgedRecovery(0.02f, 0f) == 0.02f
                && RivalryMath.GrudgedPressure(0.03f, 0f) == 0.03f
                && Math.Abs(RivalryMath.GrudgedRecovery(0.02f, 9f) - 0.01f) < 1e-6f);

            Check("a half grudge sits exactly between",
                Math.Abs(RivalryMath.GrudgedRecovery(0.02f, 0.5f) - 0.015f) < 1e-6f
                && Math.Abs(RivalryMath.GrudgedPressure(0.02f, 0.5f) - 0.03f) < 1e-6f);

            // The ledger itself, through the shipping writer.
            string dir = Path.Combine(Path.GetTempPath(), "rw_rivalry_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "rivalry.dat");

            try
            {
                RivalryLedger.OverridePath = path;

                RivalryLedger.Load();
                Check("a fresh world has an empty, usable ledger with watermark 0",
                    RivalryLedger.IsLoaded && RivalryLedger.Count == 0
                    && RivalryLedger.PlantWatermark == 0);

                var zoneA = new ZoneKey(0, -1);
                RivalryLedger.AddHarm(zoneA, 111L, 0.5f);
                RivalryLedger.AddCare(zoneA, 111L, 0.25f);
                RivalryLedger.AddCare(zoneA, 222L, 1.0f);
                RivalryLedger.AddCare(new ZoneKey(3, 3), 111L, 0.1f);
                RivalryLedger.PlantWatermark = 987654321L;
                RivalryLedger.SaveIfDirty();
                RivalryLedger.Load();
                var row = RivalryLedger.Get(zoneA, 111L);
                Check($"rows and watermark survive the shipping writer (harm={row.Harm:F2} care={row.Care:F2})",
                    RivalryLedger.Count == 3
                    && Math.Abs(row.Harm - 0.5f) < 1e-4f && Math.Abs(row.Care - 0.25f) < 1e-4f
                    && RivalryLedger.PlantWatermark == 987654321L);

                RivalryLedger.PlantWatermark = 5L;   // an attempt to LOWER it
                Check("the watermark only rises", RivalryLedger.PlantWatermark == 987654321L);

                RivalryLedger.AddHarm(zoneA, 0L, 9f);
                Check("player id 0 is never recorded", RivalryLedger.Get(zoneA, 0L).Harm == 0f);

                float worst = RivalryLedger.MaxGrudgeFor(111L, 1f);
                Check($"the worst grudge finds the right zone and nets out care ({worst:F2})",
                    Math.Abs(worst - 0.25f) < 1e-4f     // zone A: 0.5 harm - 0.25 care
                    && RivalryLedger.MaxGrudgeFor(222L, 1f) == 0f    // pure carer, no grudge
                    && RivalryLedger.MaxGrudgeFor(999L, 1f) == 0f);  // stranger, no rows

                RivalryLedger.DecayAll(0.5f);
                Check($"decay halves every column ({RivalryLedger.Get(zoneA, 111L).Harm:F3})",
                    Math.Abs(RivalryLedger.Get(zoneA, 111L).Harm - 0.25f) < 1e-4f
                    && Math.Abs(RivalryLedger.Get(zoneA, 222L).Care - 0.5f) < 1e-4f);

                RivalryLedger.DecayAll(1e-6f);
                RivalryLedger.SaveIfDirty();
                RivalryLedger.Load();
                Check("rows that fade to nothing are pruned, the file stays sparse",
                    RivalryLedger.Count == 0);

                byte[] raw = File.ReadAllBytes(path);
                Check("the ledger is written without a BOM",
                    raw.Length >= 3 && !(raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF));

                File.WriteAllLines(path, new[] { "version\t1", "0\t0\t555\t-3\t-9" });
                RivalryLedger.Load();
                Check("hand-edited negatives are floored on read, not trusted",
                    RivalryLedger.Get(new ZoneKey(0, 0), 555L).Harm == 0f);

                File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x00, 0xFF });
                RivalryLedger.Load();
                Check("a corrupt ledger degrades to empty and is quarantined",
                    RivalryLedger.IsLoaded && RivalryLedger.Count == 0
                    && File.Exists(path + ".corrupt") && !File.Exists(path));
            }
            finally
            {
                RivalryLedger.OverridePath = null;
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---- Contest (phase C) ---------------------------------------------------------

        private static void ContestTests()
        {
            Console.WriteLine("\nContest");

            var zone = new ZoneKey(1, -1);
            var other = new ZoneKey(2, 2);

            Dictionary<ZoneKey, Dictionary<long, float>> Values(params (ZoneKey z, long p, float v)[] rows)
            {
                var d = new Dictionary<ZoneKey, Dictionary<long, float>>();
                foreach ((ZoneKey z, long p, float v) r in rows)
                {
                    if (!d.TryGetValue(r.z, out Dictionary<long, float> inner))
                        d[r.z] = inner = new Dictionary<long, float>();
                    inner[r.p] = r.v;
                }
                return d;
            }

            var holders = new Dictionary<ZoneKey, RivalryContest.Holder>();
            var flips = new List<RivalryContest.Flip>();

            // Below the floor: nobody holds anything, and nothing is announced.
            RivalryContest.Update(Values((zone, 111L, 0.1f)), holders, 0.2f, 0.15f, flips);
            Check("nobody wins ground they barely touched",
                holders.Count == 0 && flips.Count == 0);

            // First real claim: crowned SILENTLY (a walkover is not a contest).
            RivalryContest.Update(Values((zone, 111L, 0.5f)), holders, 0.2f, 0.15f, flips);
            Check("an unclaimed zone is crowned silently",
                holders.Count == 1 && holders[zone].Player == 111L && flips.Count == 0);

            // A challenger inside the hysteresis band does NOT take it.
            RivalryContest.Update(Values((zone, 111L, 0.5f), (zone, 222L, 0.55f)), holders, 0.2f, 0.15f, flips);
            Check("a challenger inside the band does not dethrone (0.55 < 0.5 x 1.15)",
                holders[zone].Player == 111L && flips.Count == 0);

            // Clearing the band takes the zone AND announces — both above the floor.
            RivalryContest.Update(Values((zone, 111L, 0.5f), (zone, 222L, 0.6f)), holders, 0.2f, 0.15f, flips);
            Check($"clearing the band flips the zone and announces ({flips.Count} flip)",
                holders[zone].Player == 222L && flips.Count == 1
                && flips[0].From == 111L && flips[0].To == 222L && flips[0].Zone == zone);

            // An incumbent who falls below the floor is replaced SILENTLY: no rival
            // genuinely contested them, they simply faded.
            flips.Clear();
            RivalryContest.Update(Values((zone, 222L, 0.1f), (zone, 333L, 0.9f)), holders, 0.2f, 0.15f, flips);
            Check("a faded incumbent is replaced without an announcement",
                holders[zone].Player == 333L && flips.Count == 0);

            // Everyone decays below the floor: the ground is unclaimed again, silently.
            flips.Clear();
            RivalryContest.Update(Values((zone, 333L, 0.05f)), holders, 0.2f, 0.15f, flips);
            Check("ground nobody shapes any more becomes unclaimed",
                holders.Count == 0 && flips.Count == 0);

            // A zone that vanishes from the ledger entirely vacates too.
            holders[other] = new RivalryContest.Holder { Player = 444L, Value = 1f };
            RivalryContest.Update(Values((zone, 111L, 0.5f)), holders, 0.2f, 0.15f, flips);
            Check("a fully decayed zone vacates its holder",
                !holders.ContainsKey(other));

            Check("ZonesHeld counts only that player's holdings",
                RivalryContest.ZonesHeld(holders, 111L) == 1
                && RivalryContest.ZonesHeld(holders, 999L) == 0
                && RivalryContest.ZonesHeld(holders, 0L) == 0);

            // Phase D: the spawn war's gates and its verdict.
            Check("contested needs BOTH sides strong — sick alone or loved alone is peace",
                RivalryContest.IsContested(0.6f, 0.4f, 0.5f, 0.3f)
                && !RivalryContest.IsContested(0.6f, 0.2f, 0.5f, 0.3f)
                && !RivalryContest.IsContested(0.3f, 0.9f, 0.5f, 0.3f)
                && !RivalryContest.IsContested(float.NaN, 0.9f, 0.5f, 0.3f));

            Check("blight is the worse of plague and corruption",
                Math.Abs(RivalryContest.BlightOf(new ZoneState { Plague = 0.3f, Corruption = 0.7f }) - 0.7f) < 1e-5f
                && RivalryContest.BlightOf(new ZoneState { Plague = float.NaN, Corruption = 0.4f }) == 0.4f);

            Check("storms escalate the war and peace has no intensity",
                RivalryContest.Intensity(contested: true, inStorm: false, 2f) == 1f
                && RivalryContest.Intensity(contested: true, inStorm: true, 2f) == 2f
                && RivalryContest.Intensity(contested: false, inStorm: true, 2f) == 0f
                && RivalryContest.Intensity(contested: true, inStorm: true, float.NaN) == 1f);

            Check("the wild wins when the blight itself broke; the blight wins otherwise",
                RivalryContest.Winner(0.3f, 0.5f) == RivalryContest.WarWinner.Wild
                && RivalryContest.Winner(0.8f, 0.5f) == RivalryContest.WarWinner.Blight
                && RivalryContest.Winner(float.NaN, 0.5f) == RivalryContest.WarWinner.Wild);

            // Mercy is decay-only and never a penalty.
            float plain = ExposureMath.Decay(1f, 20f, false, 2f, 60f);
            float merciful = ExposureMath.Decay(1f, 20f, false, 2f, 60f, 1.5f);
            Check($"mercy quickens recovery only ({plain:F3} -> {merciful:F3})",
                merciful < plain
                && Math.Abs((1f - merciful) - (1f - plain) * 1.5f) < 1e-5f
                && ExposureMath.Decay(1f, 20f, false, 2f, 60f, 0.1f) == plain
                && ExposureMath.Decay(1f, 20f, false, 2f, 60f, float.NaN) == plain);
        }

        private static void NemesisTests()
        {
            Console.WriteLine("\nNemesis");

            Check("a first kill lifts level 1 to 2", NemesisMark.NextLevel(1, 3) == 2);
            Check("the cap holds at the top", NemesisMark.NextLevel(3, 3) == 3);
            // Deliberate, and it has a consequence worth stating: because a lower cap never
            // walks a creature back down, LOWERING NemesisMaxLevel CANNOT REPAIR ANYTHING
            // ALREADY MARKED. When a twice-marked Queen turned out to be near-unbeatable on
            // 2026-09-23, that is why the fix had to be "never level a boss in the first
            // place" (0.27.3) rather than "cap bosses lower" — the latter would have shipped
            // a fix that provably does nothing to the boss anyone had already met.
            Check("a cap below current never demotes", NemesisMark.NextLevel(3, 2) == 3);
            Check("a garbage level is floored to 1 before stepping", NemesisMark.NextLevel(0, 3) == 2);
            Check("a garbage cap is floored to 1, and current still wins", NemesisMark.NextLevel(2, 0) == 2);

            Check("one kill reads as a single slaying",
                NemesisMark.Suffix("Nomad", 1) == "\n<size=70%><color=#b45050>slayer of Nomad</color></size>");
            Check("repeat kills carry the count",
                NemesisMark.Suffix("Nomad", 3) == "\n<size=70%><color=#b45050>slayer of Nomad x3</color></size>");
            Check("a padded victim name is trimmed",
                NemesisMark.Suffix("  Nomad ", 1) == "\n<size=70%><color=#b45050>slayer of Nomad</color></size>");
            Check("no name, no story", NemesisMark.Suffix("", 2) == "");
            Check("whitespace is not a name", NemesisMark.Suffix("   ", 2) == "");
            Check("no kills, no story", NemesisMark.Suffix("Nomad", 0) == "");
            Check("negative kills are no story either", NemesisMark.Suffix("Nomad", -1) == "");
        }

        private static void RelicTests()
        {
            Console.WriteLine("\nRelic");

            // The peak watermark.
            Check("below threshold records nothing", RelicMath.TrackPeak(0.4f, 0.5f, 0f) == 0f);
            Check("at threshold records the value", RelicMath.TrackPeak(0.5f, 0.5f, 0f) == 0.5f);
            Check("peaks only rise", RelicMath.TrackPeak(0.6f, 0.5f, 0.9f) == 0.9f);
            Check("NaN never writes", RelicMath.TrackPeak(float.NaN, 0.5f, 0.3f) == 0.3f);

            // Through-zero, not merely reduced.
            Check("a peak driven to zero consecrates", RelicMath.ShouldConsecrate(0.7f, 0f));
            Check("reduced-but-alive does not", !RelicMath.ShouldConsecrate(0.7f, 0.01f));
            Check("no recorded peak, no story", !RelicMath.ShouldConsecrate(0f, 0f));

            // Aura arithmetic.
            Check("blessed ground heals quicker",
                RelicMath.RecoveryMultiplier(RelicMath.Plague, false, 1.25f, 0.8f) == 1.25f);
            Check("cursed ground sulks",
                RelicMath.RecoveryMultiplier(RelicMath.Contest, true, 1.25f, 0.8f) == 0.8f);
            Check("no stone, no aura",
                RelicMath.RecoveryMultiplier(RelicMath.None, false, 1.25f, 0.8f) == 1f);
            Check("blessed drains exposure faster",
                RelicMath.ExposureDecayMultiplier(RelicMath.Fire, false, 1.5f) == 1.5f);
            Check("cursed does not slow healing",
                RelicMath.ExposureDecayMultiplier(RelicMath.Contest, true, 1.5f) == 1f);
            Check("cursed shrinks minutes-to-max",
                Math.Abs(RelicMath.ExposureMinutesMultiplier(RelicMath.Contest, true, 1.25f) - 0.8f) < 1e-6f);
            Check("blessed never shields from accrual",
                RelicMath.ExposureMinutesMultiplier(RelicMath.Fire, false, 1.25f) == 1f);
            Check("cursed ground breeds meaner things",
                RelicMath.StarMultiplier(RelicMath.Contest, true, 0.25f) == 1.25f);
            Check("blessed ground adds no stars",
                RelicMath.StarMultiplier(RelicMath.Fire, false, 0.25f) == 1f);

            // Every standing type tells a story; a missing stone says nothing.
            Check("the contest stone knows who won",
                RelicMath.Story(RelicMath.Contest, true).Contains("blight")
                && RelicMath.Story(RelicMath.Contest, false).Contains("wild"));
            Check("no stone, no words", RelicMath.Story(RelicMath.None, false) == "");

            // The ledger round-trips all four row kinds through the SHIPPING writer.
            string path = Path.Combine(Path.GetTempPath(), "rw_relic_test.dat");
            try
            {
                RelicLedger.OverridePath = path;
                if (File.Exists(path)) File.Delete(path);

                RelicLedger.Load();
                Check("a fresh ledger loads empty and usable",
                    RelicLedger.IsLoaded && RelicLedger.RelicCount == 0 && !RelicLedger.EraArmed);

                var zone = new ZoneKey(3, -7);
                RelicLedger.SetPeaks(zone, new RelicLedger.Peaks { Scorch = 0.61f, Plague = 0f });
                RelicLedger.SetRelic(new ZoneKey(0, -1),
                    new RelicLedger.Relic { Type = RelicMath.Contest, Cursed = true, Day = 214 });
                RelicLedger.AddPending(new ZoneKey(5, 5),
                    new RelicLedger.Relic { Type = RelicMath.Fire, Cursed = false, Day = 100 });
                RelicLedger.SetEraSnapshot(new ZoneKey(-2, 2), 1.75f);
                RelicLedger.SaveIfDirty();

                byte[] bytes = File.ReadAllBytes(path);
                Check("the relic ledger is written without a byte-order mark",
                    bytes.Length > 0 && bytes[0] != 0xEF);

                RelicLedger.Load();
                RelicLedger.Relic r = RelicLedger.RelicAt(new ZoneKey(0, -1));
                Check("a standing relic round-trips with type, verdict and day",
                    r.Standing && r.Type == RelicMath.Contest && r.Cursed && r.Day == 214);
                Check("an unconfirmed stone stays unplaced through the round-trip", !r.Placed);

                RelicLedger.MarkPlaced(new ZoneKey(0, -1));
                RelicLedger.SaveIfDirty();
                RelicLedger.Load();
                Check("a confirmed stone round-trips placed",
                    RelicLedger.RelicAt(new ZoneKey(0, -1)).Placed);

                Check("peaks round-trip", RelicLedger.PeaksFor(zone).Scorch == 0.61f);
                Check("pending rows round-trip", RelicLedger.PendingCount == 1);
                Check("the era snapshot round-trips armed", RelicLedger.EraArmed);

                Check("a standing stone ends peak tracking for its zone",
                    RelicLedger.PeaksFor(new ZoneKey(0, -1)).Empty);

                RelicLedger.RemoveRelic(new ZoneKey(0, -1));
                Check("desecration removes the stone",
                    !RelicLedger.RelicAt(new ZoneKey(0, -1)).Standing);

                // A 0.17.0-format row (no placed column) must load unplaced — that is what
                // re-arms the retry for a stone that never rose.
                File.WriteAllText(path,
                    "version\t1\nR\t9\t9\t0\t0\t5\n",
                    new System.Text.UTF8Encoding(false));
                RelicLedger.Load();
                RelicLedger.Relic old = RelicLedger.RelicAt(new ZoneKey(9, 9));
                Check("a six-column 0.17.0 row loads standing but unplaced",
                    old.Standing && !old.Placed);

                File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x13, 0x37, 0x00, 0xFF });
                RelicLedger.Load();
                Check("a garbage file is quarantined, world stays playable",
                    RelicLedger.IsLoaded && RelicLedger.RelicCount == 0 && File.Exists(path + ".corrupt"));
            }
            finally
            {
                RelicLedger.OverridePath = null;
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                try { if (File.Exists(path + ".corrupt")) File.Delete(path + ".corrupt"); } catch { }
            }
        }

        private static void WrathAdminTests()
        {
            Console.WriteLine("\nWrathAdmin");

            var s = default(ZoneState);
            Check("plague lands on the plague field",
                WrathAdmin.TrySetZoneField(s, "plague", 0.6f, out var r1) && r1.Plague == 0.6f && r1.Scorch == 0f);
            Check("scorch lands on the scorch field",
                WrathAdmin.TrySetZoneField(s, "scorch", 0.3f, out var r2) && r2.Scorch == 0.3f && r2.Plague == 0f);
            Check("corr and corruption are the same field",
                WrathAdmin.TrySetZoneField(s, "corr", 0.2f, out var r3) && r3.Corruption == 0.2f
                && WrathAdmin.TrySetZoneField(s, "corruption", 0.2f, out var r4) && r4.Corruption == 0.2f);
            Check("fert lands on fertility",
                WrathAdmin.TrySetZoneField(s, "fert", 0.4f, out var r5) && r5.Fertility == 0.4f);
            Check("frost lands on frost",
                WrathAdmin.TrySetZoneField(s, "frost", 0.9f, out var r6) && r6.Frost == 0.9f);
            Check("values clamp to the store's own bounds",
                WrathAdmin.TrySetZoneField(s, "plague", 3f, out var r7) && r7.Plague == 1f
                && WrathAdmin.TrySetZoneField(s, "plague", -1f, out var r8) && r8.Plague == 0f);
            Check("an unknown field refuses rather than guessing",
                !WrathAdmin.TrySetZoneField(s, "spice", 0.5f, out _));
            Check("NaN refuses", !WrathAdmin.TrySetZoneField(s, "plague", float.NaN, out _));

            Check("invariant parse reads what the store writes",
                WrathAdmin.TryParseValue("0.5086", out float v) && Math.Abs(v - 0.5086f) < 1e-6f);
            Check("garbage is not a value", !WrathAdmin.TryParseValue("blight", out _));
            Check("NaN text is not a value", !WrathAdmin.TryParseValue("NaN", out _));
        }

        private static void FarmingGrowthTests()
        {
            Console.WriteLine("\nFarmingGrowth");

            Check("pristine soil grows at vanilla speed",
                FarmingGrowth.GrowTimeMultiplier(0f, 2f) == 1f);
            Check("fully depleted soil hits the configured slowdown",
                FarmingGrowth.GrowTimeMultiplier(1f, 2f) == 2f);
            Check("half depletion lands halfway (linear, like the writer)",
                Math.Abs(FarmingGrowth.GrowTimeMultiplier(0.5f, 2f) - 1.5f) < 1e-6f);
            Check("depletion past the scale clamps rather than compounds",
                FarmingGrowth.GrowTimeMultiplier(1.7f, 2f) == 2f);
            Check("slowdown 1 disables the effect entirely",
                FarmingGrowth.GrowTimeMultiplier(0.8f, 1f) == 1f);
            Check("NaN depletion is pristine, never punitive",
                FarmingGrowth.GrowTimeMultiplier(float.NaN, 2f) == 1f);
            Check("NaN slowdown is vanilla, never punitive",
                FarmingGrowth.GrowTimeMultiplier(0.8f, float.NaN) == 1f);
        }

        private static void PlagueGenesisTests()
        {
            Console.WriteLine("\nPlagueGenesis");

            // 60s tick, 12h mean: chance = 60 / 43200.
            Check("the per-tick chance matches the configured mean",
                Math.Abs(PlagueGenesis.ChancePerTick(60f, 12f) - 60f / 43200f) < 1e-9f);
            Check("an absurd mean clamps to certainty, not beyond",
                PlagueGenesis.ChancePerTick(60f, 0.005f) == 1f);
            Check("a zero mean disables rather than floods",
                PlagueGenesis.ChancePerTick(60f, 0f) == 0f);
            Check("a NaN mean disables rather than floods",
                PlagueGenesis.ChancePerTick(60f, float.NaN) == 0f);
            Check("a garbage interval disables",
                PlagueGenesis.ChancePerTick(0f, 12f) == 0f);

            Check("clean ground is weight one", PlagueGenesis.Weight(0f, 0f) == 1f);
            Check("full blight is five times likelier", PlagueGenesis.Weight(1f, 1f) == 5f);
            Check("corruption and scorch weigh alike",
                PlagueGenesis.Weight(0.5f, 0f) == PlagueGenesis.Weight(0f, 0.5f));
            Check("out-of-range ground clamps", PlagueGenesis.Weight(7f, -3f) == 3f);
            Check("NaN ground is clean, never punitive",
                PlagueGenesis.Weight(float.NaN, float.NaN) == 1f);
        }

        private static void LightningStrikeTests()
        {
            Console.WriteLine("\nLightningStrike");

            // ---- the sky gate -------------------------------------------------------------
            // THE REGRESSION. Until 2026-09-18 FireSystem asked `EnvMan.IsWet()` directly. A
            // forced sky reaches CLIENTS only — vanilla's override path needs a local player,
            // which a dedicated server does not have — so the server kept answering with its
            // OWN unforced weather. Live result that day: a bolt landed under a forced
            // ThunderStorm one log line after the mod announced rain suppresses lightning,
            // in BOTH of that session's wet storms, while the connected client showed the
            // vanilla `Wet` status and visible rain. This first case is that exact scenario
            // and it fails against the old `if (EnvMan.IsWet()) return;`.
            Check("a forced WET sky refuses a bolt even where no rain is read",
                !LightningStrike.SkyAllows(forcedSky: true, stormIsDry: false, rainingWhereItLands: false));
            Check("a forced DRY sky allows a bolt",
                LightningStrike.SkyAllows(forcedSky: true, stormIsDry: true, rainingWhereItLands: false));

            // With a sky forced, the world's own weather is not the storm anyone is standing
            // in, so it must not get a vote in EITHER direction. The second of these is the
            // silent half of the same bug: a dry storm that never strikes because the SERVER
            // happened to be rained on looks exactly like lightning simply not rolling.
            Check("a forced WET sky still refuses where rain is read too",
                !LightningStrike.SkyAllows(forcedSky: true, stormIsDry: false, rainingWhereItLands: true));
            Check("a forced DRY sky still allows where rain is read",
                LightningStrike.SkyAllows(forcedSky: true, stormIsDry: true, rainingWhereItLands: true));
            Check("a forced sky needs no rain read at all",
                LightningStrike.SkyAllows(forcedSky: true, stormIsDry: true, rainingWhereItLands: null) &&
                !LightningStrike.SkyAllows(forcedSky: true, stormIsDry: false, rainingWhereItLands: null));

            // No forced sky: the storm imposes nothing, the world's own weather at the landing
            // spot is the truth, and the rolled look changes nothing anyone can see. Through
            // 0.27.5 FireSystem read that weather from EnvMan.IsWet(), which a dedicated server
            // never updates (no camera, so no weather roll): Clear, dry, for the whole run. It
            // now asks FireFront (FireSystem.TryReadRainAt). The harness cannot reach that read:
            // it pins the verdict, and only an in-game run can show the read resolves and answers.
            Check("with no forced sky, rain where the bolt would land refuses it",
                !LightningStrike.SkyAllows(forcedSky: false, stormIsDry: true, rainingWhereItLands: true));
            Check("with no forced sky, dry weather there allows it",
                LightningStrike.SkyAllows(forcedSky: false, stormIsDry: true, rainingWhereItLands: false));
            Check("with no forced sky, the rolled look does not decide — rain there does",
                !LightningStrike.SkyAllows(forcedSky: false, stormIsDry: false, rainingWhereItLands: true) &&
                 LightningStrike.SkyAllows(forcedSky: false, stormIsDry: false, rainingWhereItLands: false));

            // An answer that cannot be had is not a dry sky. A FireFront older than 0.21.0 has
            // no rain read and a reflected call can throw; either way the bolt is withheld,
            // never risked, which is the homestead standoff's fail-closed rule. A gate written
            // as `rainingWhereItLands != true` passes every case above and fails this one.
            Check("with no forced sky, a rain read that cannot answer withholds the bolt",
                !LightningStrike.SkyAllows(forcedSky: false, stormIsDry: true, rainingWhereItLands: null) &&
                !LightningStrike.SkyAllows(forcedSky: false, stormIsDry: false, rainingWhereItLands: null));

            // 10s tick, 15min mean: chance = 10 / 900.
            Check("the per-tick chance matches the configured mean",
                Math.Abs(LightningStrike.ChancePerTick(10f, 15f) - 10f / 900f) < 1e-9f);
            Check("an absurd mean clamps to certainty, not beyond",
                LightningStrike.ChancePerTick(60f, 0.005f) == 1f);
            Check("a zero mean disables rather than floods",
                LightningStrike.ChancePerTick(10f, 0f) == 0f);
            Check("a NaN mean disables rather than floods",
                LightningStrike.ChancePerTick(10f, float.NaN) == 0f);
            Check("a garbage interval disables",
                LightningStrike.ChancePerTick(float.NaN, 15f) == 0f);

            var anchor = new Vector3(100f, 37f, -200f);

            // u1=0 lands exactly on the inner edge, u1→1 approaches the outer edge.
            Vector3 inner = LightningStrike.StrikePoint(anchor, 0.0, 0.25, 15f, 40f);
            Check("u1=0 lands on the inner edge (dist 15)",
                Math.Abs(DistXZ(anchor, inner) - 15f) < 1e-3f);

            Vector3 outer = LightningStrike.StrikePoint(anchor, 0.9999999, 0.7, 15f, 40f);
            Check("u1=1 lands on the outer edge (dist 40)",
                Math.Abs(DistXZ(anchor, outer) - 40f) < 1e-2f);

            Check("every strike keeps the anchor's height",
                inner.y == 37f && outer.y == 37f);

            // u2=0 is straight +X — pins the angle convention so a change is deliberate.
            Vector3 east = LightningStrike.StrikePoint(anchor, 0.0, 0.0, 20f, 20f);
            Check("u2=0 strikes due +X",
                Math.Abs(east.x - 120f) < 1e-3f && Math.Abs(east.z - (-200f)) < 1e-3f);

            // A hundred samples all live inside the ring — area-uniform never escapes it.
            bool allInRing = true;
            var rng = new Random(12345);
            for (int i = 0; i < 100; i++)
            {
                float d = DistXZ(anchor,
                    LightningStrike.StrikePoint(anchor, rng.NextDouble(), rng.NextDouble(), 15f, 40f));
                if (d < 15f - 1e-3f || d > 40f + 1e-3f) { allInRing = false; break; }
            }
            Check("a hundred rolls all land inside the ring", allInRing);

            Check("NaN radii clamp to the anchor, never propagate",
                DistXZ(anchor, LightningStrike.StrikePoint(anchor, 0.5, 0.5, float.NaN, float.NaN)) < 1e-3f);
            Check("a negative inner radius clamps to zero",
                DistXZ(anchor, LightningStrike.StrikePoint(anchor, 0.0, 0.0, -10f, 40f)) < 1e-3f);
            Check("an inverted pair collapses to the surviving value",
                Math.Abs(DistXZ(anchor, LightningStrike.StrikePoint(anchor, 0.9999999, 0.3, 30f, 5f)) - 30f) < 1e-2f);
        }

        // ---- world reset (review 2026-09-24, must-fix 1) and the save's clock lookup ----

        private static void WorldResetTests()
        {
            Console.WriteLine("\nWorld reset");

            ZoneClock.Clear();
            var stamped = new ZoneKey(4, -9);
            ZoneClock.Restore(stamped, 638000000000000000L);
            Check("ZoneClock.TryGet finds a stored stamp",
                ZoneClock.TryGet(stamped, out long got) && got == 638000000000000000L);
            Check("ZoneClock.TryGet reports a zone it has never seen",
                !ZoneClock.TryGet(new ZoneKey(5, -9), out long none) && none == 0L);

            string dir = Path.Combine(Path.GetTempPath(), "rw_reset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                // World A: drift with a contact stamp, saved.
                Persistence.OverrideDirectory = dir;
                Persistence.OverrideWorldUid = 1111UL;
                ZoneClock.Clear();
                Persistence.Load();
                var zone = new ZoneKey(10, 20);
                Persistence.Set(zone, new ZoneState { Plague = 0.6f });
                Persistence.Set(new ZoneKey(11, 20), new ZoneState { Frost = 0.3f });   // no stamp
                ZoneClock.Restore(zone, 638111111111111111L);
                ZoneClock.Restore(new ZoneKey(99, 99), 638222222222222222L);   // contacted, default state
                Persistence.Save(force: true);

                ZoneClock.Clear();
                Persistence.Load();
                Check("a zone's contact stamp survives the save's lookup",
                    ZoneClock.TryGet(zone, out long back) && back == 638111111111111111L);
                Check("a stored zone with no stamp saves as 0 and reloads without one",
                    !ZoneClock.TryGet(new ZoneKey(11, 20), out _) && Persistence.TrackedZoneCount == 2);

                // The world closes: what EndWorld does to the zone store.
                Persistence.Unload();
                ZoneClock.Clear();
                Check("an unloaded store is empty and no longer claims to be the authority",
                    !Persistence.IsLoaded && Persistence.TrackedZoneCount == 0 && ZoneClock.TrackedZoneCount == 0);

                string fileA = Path.Combine(dir, "ragnarokswrath_zones_1111.dat");
                string before = File.ReadAllText(fileA);
                Persistence.Save(force: true);
                Check("saving after unload writes nothing", File.ReadAllText(fileA) == before);

                // World B in the same process: its own (empty) state, and A's file untouched.
                Persistence.OverrideWorldUid = 2222UL;
                Persistence.Load();
                Check("the next world starts from its own file, not the last world's memory",
                    Persistence.IsLoaded && Persistence.TrackedZoneCount == 0
                    && Persistence.Get(zone).Plague == 0f);
                Persistence.Set(new ZoneKey(-1, -1), new ZoneState { Scorch = 0.2f });
                Persistence.Save(force: true);

                Persistence.Unload();
                Persistence.OverrideWorldUid = 1111UL;
                Persistence.Load();
                Check("going back to the first world finds its own drift intact",
                    Persistence.TrackedZoneCount == 2 && Math.Abs(Persistence.Get(zone).Plague - 0.6f) < 1e-6f
                    && Persistence.Get(new ZoneKey(-1, -1)).Scorch == 0f);
                Persistence.Unload();
            }
            finally
            {
                Persistence.OverrideDirectory = null;
                Persistence.OverrideWorldUid = null;
                ZoneClock.Clear();
                try { Directory.Delete(dir, true); } catch { }
            }

            // Each ledger: Unload forgets, Load brings the file back.
            string ldir = Path.Combine(Path.GetTempPath(), "rw_reset_l_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ldir);
            try
            {
                TitleStore.OverridePath = Path.Combine(ldir, "titles.dat");
                TitleStore.Load();
                TitleStore.Set(42L, "Stormrider");
                TitleStore.Unload();
                Check("TitleStore.Unload forgets the world", !TitleStore.IsLoaded && TitleStore.Count == 0);
                TitleStore.Load();
                Check("TitleStore reloads its own file after an unload", TitleStore.Get(42L) == "Stormrider");

                // Winterborn's mark survives a restart (fix review 2, 2026-09-24), in a column an
                // older build ignores.
                TitleStore.Set(42L, "Winterborn");
                TitleStore.MarkWinterborn(42L);
                TitleStore.Set(42L, "Stormrider");          // a later title replaces it
                TitleStore.Unload();                        // restart mid-winter
                TitleStore.Load();
                Check("the Winterborn mark survives a restart after the title changed",
                    TitleStore.WinterbornThisWinter(42L) && TitleStore.Get(42L) == "Stormrider");
                string row = null;
                foreach (string l in File.ReadAllLines(TitleStore.OverridePath))
                    if (l.StartsWith("42\t", StringComparison.Ordinal)) row = l;
                string[] cols = row?.Split('\t');
                Check($"the mark is a third column, so columns 1-2 read as before (row '{row}')",
                    cols != null && cols.Length == 3 && cols[1] == "Stormrider" && cols[2] == "W");

                File.WriteAllText(TitleStore.OverridePath, "version\t1\n7\tPlaguewalker\n");   // pre-mark file
                TitleStore.Load();
                Check("a file from before the mark loads with no marks",
                    TitleStore.Get(7L) == "Plaguewalker" && !TitleStore.WinterbornThisWinter(7L)
                    && !TitleStore.WinterbornThisWinter(42L));

                TitleStore.Set(7L, "Winterborn");
                TitleStore.MarkWinterborn(7L);
                TitleStore.ClearWinterborn();               // winter ended
                TitleStore.Load();
                Check("clearing at winter's end is saved", !TitleStore.WinterbornThisWinter(7L));

                HealthStore.OverridePath = Path.Combine(ldir, "health.dat");
                HealthStore.Load();
                HealthStore.Set(42L, 0.5f);
                HealthStore.SaveIfDirty();
                HealthStore.Set(43L, 0.25f);   // dirty, unsaved: Unload must not carry it anywhere
                HealthStore.Unload();
                HealthStore.SaveIfDirty();
                Check("HealthStore.Unload forgets the world and saves nothing after",
                    !HealthStore.IsLoaded && HealthStore.Count == 0);
                HealthStore.Load();
                Check("HealthStore reloads its own file after an unload",
                    HealthStore.Count == 1 && Math.Abs(HealthStore.Get(42L) - 0.5f) < 1e-6f);

                RivalryLedger.OverridePath = Path.Combine(ldir, "rivalry.dat");
                RivalryLedger.Load();
                RivalryLedger.AddHarm(new ZoneKey(1, 1), 42L, 0.5f);
                RivalryLedger.PlantWatermark = 12345L;
                RivalryLedger.SaveIfDirty();
                RivalryLedger.Unload();
                Check("RivalryLedger.Unload forgets rows and the watermark",
                    !RivalryLedger.IsLoaded && RivalryLedger.Count == 0 && RivalryLedger.PlantWatermark == 0);
                RivalryLedger.Load();
                Check("RivalryLedger reloads its own file after an unload",
                    RivalryLedger.Count == 1 && RivalryLedger.PlantWatermark == 12345L);

                RelicLedger.OverridePath = Path.Combine(ldir, "relic.dat");
                RelicLedger.Load();
                RelicLedger.SetRelic(new ZoneKey(2, 2),
                    new RelicLedger.Relic { Type = RelicMath.Fire, Cursed = false, Day = 7 });
                RelicLedger.AddPending(new ZoneKey(3, 3),
                    new RelicLedger.Relic { Type = RelicMath.Fire, Cursed = false, Day = 8 });
                RelicLedger.SetEraSnapshot(new ZoneKey(4, 4), 1f);
                RelicLedger.SaveIfDirty();
                RelicLedger.Unload();
                Check("RelicLedger.Unload forgets relics, pending stones and the era",
                    !RelicLedger.IsLoaded && RelicLedger.RelicCount == 0
                    && RelicLedger.PendingCount == 0 && !RelicLedger.EraArmed);
                RelicLedger.Load();
                Check("RelicLedger reloads its own file after an unload",
                    RelicLedger.RelicCount == 1 && RelicLedger.PendingCount == 1 && RelicLedger.EraArmed);
            }
            finally
            {
                TitleStore.OverridePath = null;
                HealthStore.OverridePath = null;
                RivalryLedger.OverridePath = null;
                RelicLedger.OverridePath = null;
                TitleStore.Unload(); HealthStore.Unload(); RivalryLedger.Unload(); RelicLedger.Unload();
                try { Directory.Delete(ldir, true); } catch { }
            }
        }

        private static float DistXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        // ---- harness ----------------------------------------------------------------


        private static void Check(string what, bool ok)
        {
            if (ok) { _passed++; Console.WriteLine($"  PASS  {what}"); }
            else    { _failed++; Console.WriteLine($"  FAIL  {what}"); }
        }
    }
}
