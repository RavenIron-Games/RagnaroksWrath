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
    /// So this file distinguishes these shapes:
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
    ///   MOVE (version 2) — the key keeps its name and its meaning but lives somewhere else: a
    ///   renamed section, or the other file. BepInEx addresses a setting by SECTION AND KEY, so to
    ///   it a moved key is a brand-new setting at its shipped default and the owner's value is an
    ///   orphan line it writes back forever and never reads. A move therefore CARRIES the stored
    ///   text to the new place and then drops the old line.
    ///
    ///   RETIRE (version 2) — a key this build no longer binds at all. Its line is dropped, since
    ///   BepInEx would otherwise keep writing it back into every save.
    ///
    /// Version numbers (backfilled — nothing before 0.27.1 ever stamped one):
    ///   0 = any unstamped file: every config this mod has ever written, up to and including
    ///       0.27.0, whatever it carries.
    ///   1 = the two-sky storm (0.27.1).
    ///   2 = the two-file layout (0.28.0): sections renumbered so they sort in order, each system's
    ///       on/off switch in its own section, tuning moved to the advanced file, and the two
    ///       storm settings that never did anything retired.
    ///   3 = the wild answers again (unreleased): `ContestWildMaxSpawned` retired. Current.
    ///
    /// SLOTS. A slot is "Section::Key" in the main file, and the same behind
    /// <see cref="AdvancedPrefix"/> in the advanced one. The prefix cannot collide with a real
    /// section name: BepInEx reads a section from between square brackets and none of ours has one.
    /// </summary>
    public static class ConfigLedger
    {
        public const string MetaSection = "Meta";
        public const string VersionKey = "ConfigVersion";
        public const int CurrentVersion = 3;

        /// <summary>Marks a slot as living in the advanced file rather than the main one.</summary>
        public const string AdvancedPrefix = "advanced|";

        /// <summary>The advanced file's name beside the main one, for log lines an owner reads.</summary>
        public const string AdvancedFileName = "com.raveniron.ragnarokswrath.advanced.cfg";

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

        /// <summary>
        /// A key that keeps its name and meaning in a new place. Both ends are slots in the layout
        /// on either side of the rung: <see cref="From"/> in the layout the rung starts from,
        /// <see cref="To"/> in the one it produces.
        /// </summary>
        public sealed class Move
        {
            public string From;
            public string To;
        }

        /// <summary>A key no build binds any more. Its line is dropped rather than rewritten forever.</summary>
        public sealed class Retire
        {
            public string Slot;
            public string Because;
        }

        /// <summary>Keyed by the version the step produces: <c>Rebases[n]</c> takes a file at n-1 up to n.</summary>
        private static readonly Dictionary<int, Rebase[]> Rebases = new Dictionary<int, Rebase[]>();

        /// <summary>
        /// Version 1. Before it, every Devastating Storm wore the single `StormForcedEnvironment`
        /// sky; after it, each storm rolls wet or dry and the dry one can start fires that the wet
        /// one cannot. `StormDryChance` ships at 0.5 because that is the feature — but on a server
        /// that already exists, 0.5 means lightning begins striking where it never did (a
        /// ThunderStorm owner) or stops striking half the time where it always did (an Eikthyr
        /// owner). BOTH directions are a silent change to a live world, so an existing file gets 0:
        /// every storm keeps using the one sky it already used, whichever that was.
        ///
        /// That is only HALF the rung, and the other half is in <see cref="Plan"/> under
        /// `version == 1`. A file whose sky was Eikthyr needs its VALUES MOVED as well, because
        /// `StormForcedEnvironment` now means the wet storm specifically and Eikthyr is the dry
        /// one — see the comment there. This table handles every other file, where the stored sky
        /// still means what the key says and only the new chance needs pinning to 0.
        ///
        /// Slots in a rung's steps are written in the layout of THAT rung. A file two rungs behind
        /// has them carried forward by the later rungs' moves, so the section named here is the one
        /// the key had in version 1 and the value still lands where this build binds it.
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

        private const bool Main = false;
        private const bool Advanced = true;

        private static Move M(string fromSection, string key, bool toAdvanced, string toSection) => new Move
        {
            From = Slot(fromSection, key),
            To = toAdvanced ? AdvancedSlot(toSection, key) : Slot(toSection, key),
        };

        /// <summary>
        /// Version 2, the two-file layout. EVERY key moves, because every section was renamed:
        /// BepInEx sorts sections by name as text, so "1 - Core" was followed by "10 - World state"
        /// and "2 - Feedback" came twelfth. Two-digit numbers sort in the order they were meant to.
        /// Key names are unchanged, and so are types, defaults and ranges; ModConfig binds exactly
        /// the right-hand side of this table, and a test binds it to prove that every destination
        /// here is a key the build really reads.
        /// </summary>
        private static readonly Dictionary<int, Move[]> Moves = new Dictionary<int, Move[]>
        {
            { 2, new[]
                {
                // 01 - General
                M("1 - Core", "VerboseLogging", Main, "01 - General"),
                // 01 - General (advanced file)
                M("1 - Core", "TickBudgetMs", Advanced, "01 - General"),
                M("1 - Core", "MaxCreditSeconds", Advanced, "01 - General"),
                M("1 - Core", "AutosaveIntervalSeconds", Advanced, "01 - General"),
                M("2 - Feedback", "MessageMinIntervalSeconds", Advanced, "01 - General"),
                // 02 - Season
                M("3 - Season", "SeasonLengthDays", Main, "02 - Season"),
                M("3 - Season", "AnnounceSeasonChange", Main, "02 - Season"),
                M("4 - Systems", "EnableSeason", Main, "02 - Season"),
                // 02 - Season (advanced file)
                M("3 - Season", "SeasonIntervalSeconds", Advanced, "02 - Season"),
                // 03 - Weather
                M("4 - Systems", "EnableWeather", Main, "03 - Weather"),
                M("6 - Weather", "StormMinIntervalSeconds", Main, "03 - Weather"),
                M("6 - Weather", "StormMaxIntervalSeconds", Main, "03 - Weather"),
                M("6 - Weather", "StormDurationSeconds", Main, "03 - Weather"),
                M("6 - Weather", "StormsForceWeather", Main, "03 - Weather"),
                M("6 - Weather", "StormForcedEnvironment", Main, "03 - Weather"),
                M("6 - Weather", "StormDryEnvironment", Main, "03 - Weather"),
                M("6 - Weather", "StormDryChance", Main, "03 - Weather"),
                // 03 - Weather (advanced file)
                M("4 - Systems", "EnableWind", Advanced, "03 - Weather"),
                M("6 - Weather", "WeatherIntervalSeconds", Advanced, "03 - Weather"),
                M("6 - Weather", "StormRangeMeters", Advanced, "03 - Weather"),
                M("6 - Weather", "StormPlagueSpreadMultiplier", Advanced, "03 - Weather"),
                M("6 - Weather", "StormAvoidBaseMeters", Advanced, "03 - Weather"),
                M("7 - Wind", "WindIntervalSeconds", Advanced, "03 - Weather"),
                // 04 - Biome state
                M("4 - Systems", "EnableBiomeState", Main, "04 - Biome state"),
                // 04 - Biome state (advanced file)
                M("5 - Biome state", "BiomeStateIntervalSeconds", Advanced, "04 - Biome state"),
                M("5 - Biome state", "BiomeContactRadiusZones", Advanced, "04 - Biome state"),
                M("5 - Biome state", "BiomeMaxZonesPerTick", Advanced, "04 - Biome state"),
                M("5 - Biome state", "BiomeRecoveryPerHour", Advanced, "04 - Biome state"),
                M("5 - Biome state", "BiomeFrostPressurePerHour", Advanced, "04 - Biome state"),
                // 05 - Fire
                M("4 - Systems", "EnableFire", Main, "05 - Fire"),
                M("8 - Fire", "StormLightningEnabled", Main, "05 - Fire"),
                M("8 - Fire", "LightningMeanMinutes", Main, "05 - Fire"),
                // 05 - Fire (advanced file)
                M("8 - Fire", "FireScorchIntervalSeconds", Advanced, "05 - Fire"),
                M("8 - Fire", "FireScorchPerMinute", Advanced, "05 - Fire"),
                M("8 - Fire", "LightningRingMinMeters", Advanced, "05 - Fire"),
                M("8 - Fire", "LightningRingMaxMeters", Advanced, "05 - Fire"),
                M("8 - Fire", "LightningStandoffMeters", Advanced, "05 - Fire"),
                M("8 - Fire", "LightningIgniteRadiusMeters", Advanced, "05 - Fire"),
                // 06 - Plague
                M("4 - Systems", "EnablePlague", Main, "06 - Plague"),
                M("9 - Plague", "PlagueGenesisEnabled", Main, "06 - Plague"),
                M("9 - Plague", "PlagueGenesisMeanHours", Main, "06 - Plague"),
                // 06 - Plague (advanced file)
                M("9 - Plague", "PlagueSpreadIntervalSeconds", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueGrowthPerHour", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueCorruptionBoost", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueSpreadThreshold", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueSeedAmount", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueSpreadChance", Advanced, "06 - Plague"),
                M("9 - Plague", "PlagueMaxSpreadsPerTick", Advanced, "06 - Plague"),
                // 07 - Ecology
                M("4 - Systems", "EnableEcology", Main, "07 - Ecology"),
                // 07 - Ecology (advanced file)
                M("11 - Ecology", "EcologyIntervalSeconds", Advanced, "07 - Ecology"),
                M("11 - Ecology", "EcologyCorruptionPerHour", Advanced, "07 - Ecology"),
                M("11 - Ecology", "EcologyPlagueThreshold", Advanced, "07 - Ecology"),
                M("11 - Ecology", "EcologyScorchThreshold", Advanced, "07 - Ecology"),
                // 08 - Farming
                M("4 - Systems", "EnableFarming", Main, "08 - Farming"),
                // 08 - Farming (advanced file)
                M("12 - Farming", "FarmingIntervalSeconds", Advanced, "08 - Farming"),
                M("12 - Farming", "FarmingDepletionPerCropHour", Advanced, "08 - Farming"),
                M("12 - Farming", "FarmingGrowthSlowdownAtFull", Advanced, "08 - Farming"),
                M("12 - Farming", "FarmingCropPrefabs", Advanced, "08 - Farming"),
                // 09 - Health
                M("15 - Health", "FrostChillEnabled", Main, "09 - Health"),
                M("4 - Systems", "EnableHealth", Main, "09 - Health"),
                // 09 - Health (advanced file)
                M("15 - Health", "HealthIntervalSeconds", Advanced, "09 - Health"),
                M("15 - Health", "ExposureMinutesToMax", Advanced, "09 - Health"),
                M("15 - Health", "ExposureRecoveryMinutes", Advanced, "09 - Health"),
                M("15 - Health", "ExposureRestedRecoveryMultiplier", Advanced, "09 - Health"),
                M("15 - Health", "ExposurePoisonResistMultiplier", Advanced, "09 - Health"),
                M("15 - Health", "ExposureTier1", Advanced, "09 - Health"),
                M("15 - Health", "ExposureTier2", Advanced, "09 - Health"),
                M("15 - Health", "ExposureTier3", Advanced, "09 - Health"),
                M("15 - Health", "SicknessStaminaRegenAtTier1", Advanced, "09 - Health"),
                M("15 - Health", "SicknessStaminaRegenAtMax", Advanced, "09 - Health"),
                M("15 - Health", "SicknessHealthRegenAtTier2", Advanced, "09 - Health"),
                M("15 - Health", "SicknessHealthRegenAtMax", Advanced, "09 - Health"),
                M("15 - Health", "FrostChillThreshold", Advanced, "09 - Health"),
                M("15 - Health", "ChillStaminaRegenMultiplier", Advanced, "09 - Health"),
                M("15 - Health", "ChillHealthRegenMultiplier", Advanced, "09 - Health"),
                // 10 - Consequence
                M("16 - Consequence", "ConsequenceBarren", Main, "10 - Consequence"),
                M("16 - Consequence", "ConsequenceEmpower", Main, "10 - Consequence"),
                M("16 - Consequence", "ConsequenceSicken", Main, "10 - Consequence"),
                M("16 - Consequence", "ConsequenceWither", Main, "10 - Consequence"),
                M("16 - Consequence", "AnnounceConsequences", Main, "10 - Consequence"),
                M("4 - Systems", "EnableConsequence", Main, "10 - Consequence"),
                // 10 - Consequence (advanced file)
                M("16 - Consequence", "ConsequenceIntervalSeconds", Advanced, "10 - Consequence"),
                M("16 - Consequence", "BarrenPlagueThreshold", Advanced, "10 - Consequence"),
                M("16 - Consequence", "BarrenScorchThreshold", Advanced, "10 - Consequence"),
                M("16 - Consequence", "SickenPlagueThreshold", Advanced, "10 - Consequence"),
                M("16 - Consequence", "SickenSpeedPenalty", Advanced, "10 - Consequence"),
                M("16 - Consequence", "EmpowerCorruptionThreshold", Advanced, "10 - Consequence"),
                M("16 - Consequence", "EmpowerLevelUpMultiplierAtFull", Advanced, "10 - Consequence"),
                M("16 - Consequence", "CropWitherBlightThreshold", Advanced, "10 - Consequence"),
                M("16 - Consequence", "WildlifePrefabs", Advanced, "10 - Consequence"),
                // 11 - Rivalry
                M("17 - Rivalry", "AnnounceContests", Main, "11 - Rivalry"),
                M("17 - Rivalry", "EnableNemesis", Main, "11 - Rivalry"),
                M("17 - Rivalry", "NemesisMaxLevel", Main, "11 - Rivalry"),
                M("4 - Systems", "EnableRivalry", Main, "11 - Rivalry"),
                // 11 - Rivalry (advanced file)
                M("17 - Rivalry", "RivalryIntervalSeconds", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "RivalryHalfLifeHours", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "CarePerHealedPoint", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "TendingCarePerPlant", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ArsonHarmPerScorchPoint", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "GrudgeScale", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "GrudgePickRefuse", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "AshbringerGrudge", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "CareDominanceFloor", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "HarmDominanceFloor", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestHysteresis", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "MercyRecoveryBonus", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "MercySicknessBonus", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "WardenZonesHeld", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "DespoilerZonesHeld", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestBlightThreshold", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestCareThreshold", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "StormContestMultiplier", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestStarBonus", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestWildSpawnChance", Advanced, "11 - Rivalry"),
                M("17 - Rivalry", "ContestWildMaxSpawned", Advanced, "11 - Rivalry"),
                // 12 - Relic
                M("4 - Systems", "EnableRelic", Main, "12 - Relic"),
                // 12 - Relic (advanced file)
                M("18 - Relic", "RelicIntervalSeconds", Advanced, "12 - Relic"),
                M("18 - Relic", "FireRelicPeakThreshold", Advanced, "12 - Relic"),
                M("18 - Relic", "PlagueRelicPeakThreshold", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicBlessedRecoveryMult", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicCursedRecoveryMult", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicBlessedExposureDrainMult", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicCursedExposureAccrualMult", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicCursedStarBonus", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicVandalHarm", Advanced, "12 - Relic"),
                M("18 - Relic", "RelicPrefabCandidates", Advanced, "12 - Relic"),
                // 13 - Titles
                M("13 - Titles", "AnnounceTitles", Main, "13 - Titles"),
                M("4 - Systems", "EnableTitle", Main, "13 - Titles"),
                // 13 - Titles (advanced file)
                M("13 - Titles", "TitleIntervalSeconds", Advanced, "13 - Titles"),
                M("13 - Titles", "WinterbornSeconds", Advanced, "13 - Titles"),
                // 14 - World state
                M("4 - Systems", "EnableWorldState", Main, "14 - World state"),
                // 14 - World state (advanced file)
                M("10 - World state", "WorldStateIntervalSeconds", Advanced, "14 - World state"),
                M("10 - World state", "WorldFlourishingBurden", Advanced, "14 - World state"),
                M("10 - World state", "WorldAilingBurden", Advanced, "14 - World state"),
                M("10 - World state", "WorldStrickenBurden", Advanced, "14 - World state"),
                M("10 - World state", "WorldStormBurden", Advanced, "14 - World state"),
                // 15 - Visuals
                M("14 - Client sync and visuals", "PlagueFogEnabled", Main, "15 - Visuals"),
                M("14 - Client sync and visuals", "FrostBreathEnabled", Main, "15 - Visuals"),
                M("14 - Client sync and visuals", "ScorchAshEnabled", Main, "15 - Visuals"),
                M("18 - Relic", "RelicRunesEnabled", Main, "15 - Visuals"),
                M("4 - Systems", "EnableZoneSync", Main, "15 - Visuals"),
                // 15 - Visuals (advanced file)
                M("14 - Client sync and visuals", "ZoneSyncIntervalSeconds", Advanced, "15 - Visuals"),
                M("14 - Client sync and visuals", "ZoneSyncRadiusZones", Advanced, "15 - Visuals"),
                M("14 - Client sync and visuals", "PlagueFogDensity", Advanced, "15 - Visuals"),
                M("14 - Client sync and visuals", "FrostBreathFloor", Advanced, "15 - Visuals"),
                M("14 - Client sync and visuals", "ScorchAshDensity", Advanced, "15 - Visuals"),
                }
            },
        };

        /// <summary>
        /// Version 2's retirements: the two storm multipliers that were computed, printed in the
        /// storm's log line and read by nothing else, for as long as they existed. Retired rather
        /// than connected, because a dial that does nothing is worse than no dial.
        ///
        /// Version 3's: `ContestWildMaxSpawned`, the spawn war's pheromone instance override. It
        /// never added an animal even when vanilla read it (the group budget used the raw cap), and
        /// Valheim 1.0.7 stopped reading it at all. The wild side came back as a spawn-chance patch,
        /// which leaves vanilla's cap alone by design, so nothing is left for this key to mean.
        /// Named at its version-2 place; a file older than that has it carried there by version 2's
        /// move and dropped from wherever it was stored.
        /// </summary>
        private static readonly Dictionary<int, Retire[]> Retirements = new Dictionary<int, Retire[]>
        {
            { 2, new[]
                {
                    new Retire { Slot = Slot("6 - Weather", "StormFireRiskMultiplier"), Because = "it never did anything" },
                    new Retire { Slot = Slot("6 - Weather", "StormWindMultiplier"), Because = "it never did anything" },
                }
            },
            { 3, new[]
                {
                    new Retire
                    {
                        Slot = AdvancedSlot("11 - Rivalry", "ContestWildMaxSpawned"),
                        Because = "Valheim 1.0 stopped reading it, and the wild's answer never needed it",
                    },
                }
            },
        };

        /// <summary>
        /// Sections a rung empties. Whatever is still under one of these names once the rung's moves
        /// and retirements are done is a line nothing reads — a mis-cased key BepInEx never matched,
        /// a key from a build older than anything here — and it would sit under a section name the
        /// new layout no longer has, forever. It is dropped and named in the log. Nothing it held was
        /// in effect: BepInEx only ever reads a key by its exact section and name.
        ///
        /// A NAME CAN BE BOTH OLD AND NEW. "13 - Titles" is version 1's section and version 2's, so
        /// listing it here once made the sweep drop AnnounceTitles, which never moved, and EnableTitle
        /// the moment it had been carried in. Plan therefore never sweeps a section any move of the
        /// same rung lands in, whatever this list says; the name stays listed so the rule, not the
        /// list, is what protects it. Caught by the harness binding ModConfig against the table.
        /// </summary>
        private static readonly Dictionary<int, string[]> EmptiedSections = new Dictionary<int, string[]>
        {
            { 2, new[]
                {
                    "1 - Core", "2 - Feedback", "3 - Season", "4 - Systems", "5 - Biome state", "6 - Weather",
                    "7 - Wind", "8 - Fire", "9 - Plague", "10 - World state", "11 - Ecology", "12 - Farming",
                    "13 - Titles", "14 - Client sync and visuals", "15 - Health", "16 - Consequence",
                    "17 - Rivalry", "18 - Relic",
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

        /// <summary>A stored value carried from the line it was written under to the place this build reads it.</summary>
        public struct MovedSlot
        {
            public string From;
            public string To;
            public string Value;

            /// <summary>
            /// The text the file ALREADY held at <see cref="To"/>, or null when it held no line there.
            /// Only an interrupted earlier run leaves a setting in both places, and from then on the
            /// new place is the one the mod has been reading, so it can carry an edit made since. The
            /// planner cannot judge that, because it does not know the shipped defaults; the engine
            /// does, and decides (see ConfigMigration.ApplyCarry). Found by the 0.28.0 review.
            /// </summary>
            public string AlreadyThere;
        }

        /// <summary>A line dropped because nothing reads it any more, and why.</summary>
        public struct DroppedSlot
        {
            public string Slot;
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

            /// <summary>
            /// Writes that land on keys ALREADY IN THE FILE, because the key's MEANING changed
            /// under the owner rather than its value being wrong. Same shape as a backfill and
            /// applied the same way, but deliberately a separate list: a backfill is safe
            /// precisely because it only ever touches an absent key, and blurring the two would
            /// quietly licence overwriting settings. Everything here is a MOVE — the value goes
            /// somewhere it still means what the owner meant — and the boot line names both ends.
            /// </summary>
            public List<BackfilledSlot> Relocated = new List<BackfilledSlot>();

            /// <summary>
            /// Stored values carried to a key's new place. Applied BEFORE anything is dropped, and
            /// the advanced file is saved before the main one, so a failure part-way leaves every
            /// old line on disk for the next boot to carry again.
            /// </summary>
            public List<MovedSlot> Moved = new List<MovedSlot>();

            /// <summary>
            /// Lines to take out of the file: every moved key's old line, every retired key, and
            /// anything left in a section a rung emptied. Applied only once the moved values are
            /// safely saved.
            /// </summary>
            public List<DroppedSlot> Dropped = new List<DroppedSlot>();

            /// <summary>True when the plan would change nothing on disk beyond the version stamp.</summary>
            public bool IsEmpty => ResetToDefault.Count == 0 && Kept.Count == 0 &&
                                   Backfilled.Count == 0 && Relocated.Count == 0 &&
                                   Moved.Count == 0 && Dropped.Count == 0;

            /// <summary>
            /// True when a step changes how the world BEHAVES rather than where a value is written.
            /// Moves and drops keep every value in effect exactly as it was, so a plan of nothing
            /// else is news, not a warning.
            /// </summary>
            public bool ChangesBehaviour => ResetToDefault.Count > 0 || Backfilled.Count > 0 || Relocated.Count > 0;
        }

        // The shipped defaults of the two storm skies. They live here as well as in ModConfig
        // because this file must reason about them without depending on the config layer, and a
        // test pins the two copies together so they cannot drift.
        public const string WetEnvironmentDefault = "ThunderStorm";
        public const string DryEnvironmentDefault = "Eikthyr";

        // Version 1's section, the layout that rung was written in. Its steps are carried to the
        // current layout by version 2's moves, like anything else a v1 rung names.
        private const string WeatherSectionV1 = "6 - Weather";

        public static string Slot(string section, string key) => section + "::" + key;

        public static string AdvancedSlot(string section, string key) => AdvancedPrefix + Slot(section, key);

        /// <summary>Splits a slot into its file, section and key. False for text that is not a slot.</summary>
        public static bool SplitSlot(string slot, out bool advanced, out string section, out string key)
        {
            advanced = false;
            section = null;
            key = null;
            if (string.IsNullOrEmpty(slot)) return false;

            string rest = slot;
            if (rest.StartsWith(AdvancedPrefix, StringComparison.Ordinal))
            {
                advanced = true;
                rest = rest.Substring(AdvancedPrefix.Length);
            }

            int i = rest.IndexOf("::", StringComparison.Ordinal);
            if (i < 0) return false;
            section = rest.Substring(0, i);
            key = rest.Substring(i + 2);
            return key.Length > 0;
        }

        /// <summary>A slot as an owner would look for it: the section and key, and which file.</summary>
        public static string DescribeSlot(string slot)
        {
            if (!SplitSlot(slot, out bool advanced, out string section, out string key)) return slot ?? "";
            return "[" + section + "] " + key + (advanced ? " in " + AdvancedFileName : "");
        }

        /// <summary>
        /// One snapshot of both files: the main file's slots as they are, the advanced file's behind
        /// <see cref="AdvancedPrefix"/>. Either may be null. NEVER THROWS.
        /// </summary>
        public static Dictionary<string, string> Combine(Dictionary<string, string> main, Dictionary<string, string> advanced)
        {
            var into = new Dictionary<string, string>(StringComparer.Ordinal);
            if (main != null)
                foreach (var kv in main) into[kv.Key] = kv.Value;
            if (advanced != null)
                foreach (var kv in advanced) into[AdvancedPrefix + kv.Key] = kv.Value;
            return into;
        }

        /// <summary>
        /// BepInEx config files are plain INI: `[Section]` headers, `#` comments, blank lines, and
        /// `Key = value` where the value may itself contain `=`. Keyed "Section::Key"; the last
        /// duplicate wins; values trimmed. NEVER THROWS — a config this cannot read must not stop
        /// the mod loading, it must look like a fresh install.
        ///
        /// ORDINAL AND CASE-SENSITIVE, corrected 2026-09-18, and that was a real bug rather than a
        /// stylistic choice. BepInEx is not case-insensitive: `ConfigDefinition.Equals` is
        /// `string.Equals(Key, other.Key) && string.Equals(Section, other.Section)` — the
        /// two-argument overload — over a case-sensitive `GetHashCode` (read out of
        /// libs\BepInEx.dll with ilspycmd). So `stormdrychance` and `StormDryChance` are two
        /// DIFFERENT keys there: one binds, the other sits in the orphan table.
        ///
        /// A backfill's entire safety is its absence test, and an ignore-case snapshot answers
        /// "present" for a key BepInEx considers absent. A file carrying one mis-cased line would
        /// therefore have SKIPPED the version 1 backfill, taken the new shipped `StormDryChance` of
        /// 0.5, and stamped — making a silent change to a live world permanent, which is the one
        /// thing this file exists to prevent. Found by the adversarial review of Undertow's port of
        /// this same code; Undertow carries the identical fix.
        /// </summary>
        public static Dictionary<string, string> ParseIni(IEnumerable<string> lines)
        {
            var into = new Dictionary<string, string>(StringComparer.Ordinal);
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
        /// <see cref="CurrentVersion"/> does, against the shipped tables. An empty snapshot (a
        /// fresh install, or a file that could not be read) plans nothing, and so does a file
        /// already at or beyond the current version. NEVER THROWS.
        /// </summary>
        public static MigrationPlan Plan(Dictionary<string, string> snapshot, int fileVersion) =>
            Plan(snapshot, fileVersion, CurrentVersion, Rebases, Backfills, Moves, Retirements, EmptiedSections);

        /// <summary>The rebase-and-backfill tables alone, with no layout changes. Kept for the harness's rule tests.</summary>
        public static MigrationPlan Plan(
            Dictionary<string, string> snapshot,
            int fileVersion,
            int toVersion,
            Dictionary<int, Rebase[]> rebases,
            Dictionary<int, Backfill[]> backfills) =>
            Plan(snapshot, fileVersion, toVersion, rebases, backfills, null, null, null);

        /// <summary>
        /// The same thing against tables supplied by the caller, and the whole implementation — the
        /// overloads above are delegation, so this is not a parallel code path.
        ///
        /// Added 2026-09-18 so the harness can reach the rules rather than only the rungs that
        /// happen to ship.
        ///
        /// THE VIEW. From version 2 a rung can move keys, so a slot one rung names is not
        /// necessarily where the file stored it. <c>view</c> maps every slot in the CURRENT layout
        /// to the line it was read from, and is re-keyed by each rung's moves; every step reads and
        /// tests absence through it, and every decision already made is carried along with the key
        /// it names. Within one rung the order is: retire, move, then rebase, relocate and backfill
        /// in the layout the rung produces.
        /// </summary>
        public static MigrationPlan Plan(
            Dictionary<string, string> snapshot,
            int fileVersion,
            int toVersion,
            Dictionary<int, Rebase[]> rebases,
            Dictionary<int, Backfill[]> backfills,
            Dictionary<int, Move[]> moves,
            Dictionary<int, Retire[]> retirements,
            Dictionary<int, string[]> emptiedSections)
        {
            var plan = new MigrationPlan { FromVersion = fileVersion, ToVersion = toVersion };
            if (snapshot == null || snapshot.Count == 0) return plan;
            if (fileVersion >= toVersion) return plan;

            // A NEGATIVE stamp is a hand-edited or corrupt file, and it must not become a loop
            // bound. Nothing stops an owner typing a large negative number into ConfigVersion, and
            // without this the window below runs from there — measured at nearly eighteen seconds
            // on the boot thread for -2000000000. A file claiming to predate version 0 simply IS a
            // version 0 file.
            int from = fileVersion < 0 ? 0 : fileVersion;

            var view = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string stored in snapshot.Keys) view[stored] = stored;

            // A line a move found ALREADY sitting at its destination, keyed like the view. It stays
            // where it is (it is the line the bound entry reads), and its text goes into the carry as
            // MovedSlot.AlreadyThere so the engine can judge the two. Overwriting the view without
            // recording it is how an edit made at the new place was once silently reverted.
            var displaced = new Dictionary<string, string>(StringComparer.Ordinal);

            // One slot, one decision. Without this a key named by two rungs is judged twice against
            // the SAME unchanged snapshot — the value never advances along the ladder — so it can
            // be reported and reset once per rung, and a slot an earlier rung claimed can be
            // re-classified by a later one. The first rung that matches owns it.
            var decided = new HashSet<string>(StringComparer.Ordinal);

            // The decided slots a step WRITES. A carried value must never land on top of one: the
            // step read the same stored value and already chose what belongs there. A KEPT value
            // is decided but not written, so it still travels with its key.
            var written = new HashSet<string>(StringComparer.Ordinal);

            string Read(string slot) =>
                view.TryGetValue(slot, out string stored) && snapshot.TryGetValue(stored, out string value) ? value : null;

            for (int version = from + 1; version <= toVersion; version++)
            {
                if (retirements != null && retirements.TryGetValue(version, out var retireSteps) && retireSteps != null)
                {
                    foreach (Retire r in retireSteps)
                    {
                        if (r == null || r.Slot == null) continue;
                        Forget(plan, decided, written, r.Slot);
                        displaced.Remove(r.Slot);
                        if (view.TryGetValue(r.Slot, out string stored))
                        {
                            view.Remove(r.Slot);
                            plan.Dropped.Add(new DroppedSlot { Slot = stored, Because = "retired: " + (r.Because ?? "no longer used") });
                        }
                    }
                }

                // Main-file sections this rung moves settings INTO. The sweep below never touches
                // them, even when an old section had the same name (see EmptiedSections).
                var landedSections = new HashSet<string>(StringComparer.Ordinal);

                if (moves != null && moves.TryGetValue(version, out var moveSteps) && moveSteps != null)
                {
                    // Two passes, so a rung that swaps two keys cannot read a slot it has just
                    // written: everything leaving is lifted out first, then set down.
                    var landing = new List<KeyValuePair<string, string>>();
                    var landingDisplaced = new List<KeyValuePair<string, string>>();
                    foreach (Move m in moveSteps)
                    {
                        if (m == null || m.From == null || m.To == null) continue;
                        if (view.TryGetValue(m.From, out string stored))
                        {
                            view.Remove(m.From);
                            landing.Add(new KeyValuePair<string, string>(m.To, stored));
                        }
                        if (displaced.TryGetValue(m.From, out string earlier))
                        {
                            displaced.Remove(m.From);
                            landingDisplaced.Add(new KeyValuePair<string, string>(m.To, earlier));
                        }
                        Rename(plan, decided, written, m.From, m.To);
                        if (SplitSlot(m.To, out bool toAdvanced, out string toSection, out _) && !toAdvanced)
                            landedSections.Add(toSection);
                    }
                    foreach (var l in landingDisplaced) displaced[l.Key] = l.Value;

                    // Everything leaving has been lifted, so whatever the view still holds at a
                    // destination is a line that really lives there. Record it rather than lose it.
                    foreach (var l in landing)
                    {
                        if (view.TryGetValue(l.Key, out string already) && !string.Equals(already, l.Value, StringComparison.Ordinal))
                            displaced[l.Key] = already;
                        view[l.Key] = l.Value;
                    }
                }

                if (emptiedSections != null && emptiedSections.TryGetValue(version, out var emptied) && emptied != null)
                {
                    var gone = new HashSet<string>(emptied, StringComparer.Ordinal);
                    var leftovers = new List<string>();
                    foreach (string slot in view.Keys)
                    {
                        if (SplitSlot(slot, out bool advanced, out string section, out _) && !advanced &&
                            gone.Contains(section) && !landedSections.Contains(section))
                            leftovers.Add(slot);
                    }
                    leftovers.Sort(StringComparer.Ordinal);
                    foreach (string slot in leftovers)
                    {
                        plan.Dropped.Add(new DroppedSlot { Slot = view[slot], Because = "nothing reads it" });
                        view.Remove(slot);
                    }
                }

                if (rebases != null && rebases.TryGetValue(version, out var rebaseSteps) && rebaseSteps != null)
                {
                    foreach (Rebase r in rebaseSteps)
                    {
                        string slot = Slot(r.Section, r.Key);
                        if (decided.Contains(slot)) continue;

                        string stored = Read(slot);
                        if (stored == null) continue;

                        decided.Add(slot);

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

                        if (wasOldDefault)
                        {
                            plan.ResetToDefault.Add(slot);
                            written.Add(slot);
                        }
                        else plan.Kept.Add(new KeptSlot { Slot = slot, Value = stored });
                    }
                }

                // VERSION 1'S SEMANTIC HALF, and it exists because the live run on Storm10
                // (2026-09-18) showed the plain backfill preserving BEHAVIOUR but not MEANING.
                //
                // Before this version `StormForcedEnvironment` was the only sky a storm could
                // wear. After it, that key means specifically THE WET ONE, and `StormDryEnvironment`
                // holds the dry one. An owner who had set it to Eikthyr — the dry sky, chosen
                // deliberately because it is the one that lets lightning through — ends up with
                // their value sitting in the slot labelled wet. Behaviour stayed right, since
                // StormDryChance 0 means the wet slot is always used and it still held Eikthyr.
                // But the boot line then read "wet 'Eikthyr' or dry 'Eikthyr'", which is
                // self-contradictory, and the moment they raised StormDryChance hoping for variety
                // they would get Eikthyr either way and conclude the feature was broken.
                //
                // So when the stored sky IS the dry one, move it to the key that now means that,
                // put the shipped wet default back in the key that now means WET, and set
                // StormDryChance to 1. Every storm still rolls dry, still wears Eikthyr, still
                // permits lightning — identical behaviour — and the two dials finally say what
                // they do. Lowering StormDryChance then produces real variety instead of nothing.
                //
                // This is the ONE place this migration writes over a key the owner set, so it is
                // gated hard: only that exact key, only when it holds exactly the dry default, and
                // the value is moved rather than discarded.
                if (version == 1)
                {
                    string forcedSlot = Slot(WeatherSectionV1, "StormForcedEnvironment");
                    string chanceSlot = Slot(WeatherSectionV1, "StormDryChance");
                    string drySlot = Slot(WeatherSectionV1, "StormDryEnvironment");

                    string storedSky = Read(forcedSlot);
                    bool storedIsDrySky =
                        storedSky != null &&
                        string.Equals(storedSky.Trim(), DryEnvironmentDefault, StringComparison.OrdinalIgnoreCase);

                    // Never fight a file that already carries the new keys: that is either an
                    // admin's own choice or an earlier run's, and neither is ours to overwrite.
                    if (storedIsDrySky && Read(chanceSlot) == null && Read(drySlot) == null)
                    {
                        decided.Add(forcedSlot);
                        decided.Add(chanceSlot);
                        decided.Add(drySlot);
                        written.Add(forcedSlot);
                        written.Add(chanceSlot);
                        written.Add(drySlot);

                        plan.Relocated.Add(new BackfilledSlot
                        {
                            Slot = drySlot,
                            Value = storedSky.Trim(),
                            Because = "the sky you chose was the DRY one, and this is the key that now means that",
                        });
                        plan.Relocated.Add(new BackfilledSlot
                        {
                            Slot = forcedSlot,
                            Value = WetEnvironmentDefault,
                            Because = "this key now means the WET storm only, so it goes back to the shipped default",
                        });
                        plan.Relocated.Add(new BackfilledSlot
                        {
                            Slot = chanceSlot,
                            Value = "1",
                            Because = "every storm still rolls dry, exactly as before; lower it for the new wet/dry mix",
                        });
                    }
                }

                if (backfills != null && backfills.TryGetValue(version, out var backfillSteps) && backfillSteps != null)
                {
                    foreach (Backfill b in backfillSteps)
                    {
                        string slot = Slot(b.Section, b.Key);
                        if (decided.Contains(slot)) continue;

                        // ABSENT ONLY, and this is the whole safety of a backfill. A key already
                        // in the file carries either the admin's choice or a value an earlier
                        // partial run wrote, and overwriting either would make this migration the
                        // thing that loses settings. The snapshot is taken BEFORE any bind for
                        // exactly this reason: once BepInEx has bound the key it is present at its
                        // shipped default and absence can no longer be observed. Tested through the
                        // view, so a key counts as present under whatever name it had in the file.
                        if (view.ContainsKey(slot)) continue;

                        decided.Add(slot);
                        written.Add(slot);
                        plan.Backfilled.Add(new BackfilledSlot
                        {
                            Slot = slot,
                            Value = b.LegacyValue,
                            Because = b.Because,
                        });
                    }
                }
            }

            // Whatever now sits under a name other than the one it was stored under has moved: carry
            // it, unless a step above already decided what belongs there, and drop the old line
            // either way. Sorted so the plan, the log and the tests read the same on every machine.
            var arrivals = new List<string>(view.Keys);
            arrivals.Sort(StringComparer.Ordinal);
            foreach (string current in arrivals)
            {
                string stored = view[current];
                if (string.Equals(current, stored, StringComparison.Ordinal)) continue;

                if (!written.Contains(current))
                {
                    string alreadyThere =
                        displaced.TryGetValue(current, out string there) && snapshot.TryGetValue(there, out string thereValue)
                            ? thereValue
                            : null;
                    plan.Moved.Add(new MovedSlot { From = stored, To = current, Value = snapshot[stored], AlreadyThere = alreadyThere });
                }

                plan.Dropped.Add(new DroppedSlot { Slot = stored, Because = "moved to " + DescribeSlot(current) });
            }

            return plan;
        }

        /// <summary>A retired slot takes every decision about it with it: nothing can be written to a key that is not bound.</summary>
        private static void Forget(MigrationPlan plan, HashSet<string> decided, HashSet<string> written, string slot)
        {
            decided.Remove(slot);
            written.Remove(slot);
            plan.ResetToDefault.RemoveAll(s => s == slot);
            plan.Kept.RemoveAll(k => k.Slot == slot);
            plan.Backfilled.RemoveAll(b => b.Slot == slot);
            plan.Relocated.RemoveAll(r => r.Slot == slot);
        }

        /// <summary>A moved slot takes every decision about it along, so an earlier rung's write lands where this build reads it.</summary>
        private static void Rename(MigrationPlan plan, HashSet<string> decided, HashSet<string> written, string from, string to)
        {
            if (decided.Remove(from)) decided.Add(to);
            if (written.Remove(from)) written.Add(to);

            for (int i = 0; i < plan.ResetToDefault.Count; i++)
                if (plan.ResetToDefault[i] == from) plan.ResetToDefault[i] = to;
            for (int i = 0; i < plan.Kept.Count; i++)
                if (plan.Kept[i].Slot == from) plan.Kept[i] = new KeptSlot { Slot = to, Value = plan.Kept[i].Value };
            for (int i = 0; i < plan.Backfilled.Count; i++)
                if (plan.Backfilled[i].Slot == from)
                    plan.Backfilled[i] = new BackfilledSlot { Slot = to, Value = plan.Backfilled[i].Value, Because = plan.Backfilled[i].Because };
            for (int i = 0; i < plan.Relocated.Count; i++)
                if (plan.Relocated[i].Slot == from)
                    plan.Relocated[i] = new BackfilledSlot { Slot = to, Value = plan.Relocated[i].Value, Because = plan.Relocated[i].Because };
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

            // Relocations first: they are the only entries that write over something the owner
            // set, so they are the ones an owner most needs to see and be able to undo.
            foreach (BackfilledSlot r in plan.Relocated)
                parts.Add(DescribeSlot(r.Slot) + " moved to " + r.Value + " (" + r.Because + ")");

            foreach (BackfilledSlot b in plan.Backfilled)
                parts.Add(DescribeSlot(b.Slot) + " set to " + b.Value + " (" + b.Because + ")");

            if (plan.ResetToDefault.Count > 0)
            {
                var names = new List<string>();
                foreach (string s in plan.ResetToDefault) names.Add(DescribeSlot(s));
                parts.Add(plan.ResetToDefault.Count.ToString(CultureInfo.InvariantCulture) +
                          " value(s) moved to their new defaults: " + string.Join(", ", names.ToArray()));
            }

            if (plan.Kept.Count > 0)
            {
                var names = new List<string>();
                foreach (KeptSlot k in plan.Kept) names.Add(DescribeSlot(k.Slot));
                parts.Add(names.Count.ToString(CultureInfo.InvariantCulture) +
                          " kept as yours: " + string.Join(", ", names.ToArray()));
            }

            if (plan.Moved.Count > 0)
            {
                int toAdvanced = 0;
                foreach (MovedSlot m in plan.Moved)
                    if (m.To.StartsWith(AdvancedPrefix, StringComparison.Ordinal)) toAdvanced++;
                parts.Add(plan.Moved.Count.ToString(CultureInfo.InvariantCulture) +
                          " setting(s) moved to the new layout with their values kept" +
                          (toAdvanced > 0
                              ? ", " + toAdvanced.ToString(CultureInfo.InvariantCulture) + " of them into " + AdvancedFileName
                              : ""));
            }

            var retired = new List<string>();
            var unread = new List<string>();
            foreach (DroppedSlot d in plan.Dropped)
            {
                if (d.Because != null && d.Because.StartsWith("retired", StringComparison.Ordinal))
                    retired.Add(DescribeSlot(d.Slot) + " (" + d.Because.Substring("retired: ".Length) + ")");
                else if (d.Because == "nothing reads it") unread.Add(DescribeSlot(d.Slot));
            }
            if (retired.Count > 0)
                parts.Add(retired.Count.ToString(CultureInfo.InvariantCulture) +
                          " retired setting(s) removed: " + string.Join(", ", retired.ToArray()));
            if (unread.Count > 0)
                parts.Add(unread.Count.ToString(CultureInfo.InvariantCulture) +
                          " line(s) removed that nothing was reading (a mis-typed name, or a very old setting): " +
                          string.Join(", ", unread.ToArray()));

            return head + string.Join("; ", parts.ToArray());
        }
    }
}
