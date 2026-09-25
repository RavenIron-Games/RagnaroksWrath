using BepInEx.Configuration;
using RavenIron.RagnaroksWrath.Core;

namespace RavenIron.RagnaroksWrath.Config
{
    /// <summary>
    /// Config surface, in TWO FILES since 0.28.0 (config layout version 2): the main file holds the
    /// settings an owner actually changes — every system's on/off switch, storms, lightning,
    /// outbreaks, announcements, visuals — and the advanced file beside it holds the tuning. Both use
    /// the same numbered section names, so a system's tuning sits under the same heading in the
    /// advanced file as its switch does in the main one. Owner-facing explanations live in
    /// docs/CONFIG.md; the description on each setting is one or two plain sentences, and the
    /// reasoning behind a value lives in the comments here.
    ///
    /// Moving a setting between sections or files is a MIGRATION, not an edit: BepInEx addresses a
    /// setting by section and key, so a moved key reads as brand new and the owner's value is
    /// stranded. Add a Move rung to ConfigLedger in the same commit, or every existing server loses
    /// that value on upgrade. A harness test binds this class and fails when the ledger and the
    /// binds disagree.
    ///
    /// Conventions worth keeping:
    ///
    /// 1. Every system gets its own on/off toggle from day one. That is what makes incremental
    ///    testing possible (build one system, disable the rest) and lets server owners adopt
    ///    part of the mod without all of it.
    ///
    /// 2. Clamp on READ as well as on write. Config files get hand-edited, and a value validated
    ///    only for a floor and not a ceiling has already taken a production service down for six
    ///    hours in this codebase's history. AcceptableValueRange handles the write side; anything
    ///    consumed in a loop should be re-clamped where it is used.
    /// </summary>
    public static class ModConfig
    {
        // ---- Sections, in the order BepInEx writes them (it sorts by name, so the numbers are
        // two digits). Both files use the same names, so a setting's tuning sits under the same
        // heading in the advanced file as its switch does in the main one.
        public const string GeneralSection = "01 - General";
        public const string SeasonSection = "02 - Season";
        public const string WeatherSection = "03 - Weather";
        public const string BiomeSection = "04 - Biome state";
        public const string FireSection = "05 - Fire";
        public const string PlagueSection = "06 - Plague";
        public const string EcologySection = "07 - Ecology";
        public const string FarmingSection = "08 - Farming";
        public const string HealthSection = "09 - Health";
        public const string ConsequenceSection = "10 - Consequence";
        public const string RivalrySection = "11 - Rivalry";
        public const string RelicSection = "12 - Relic";
        public const string TitlesSection = "13 - Titles";
        public const string WorldSection = "14 - World state";
        public const string VisualsSection = "15 - Visuals";

        // ---- Core ----------------------------------------------------------------------
        public static ConfigEntry<float> TickBudgetMs;
        public static ConfigEntry<float> MaxCreditSeconds;
        public static ConfigEntry<bool>  VerboseLogging;
        public static ConfigEntry<float> AutosaveIntervalSeconds;

        // ---- Feedback -------------------------------------------------------------------
        public static ConfigEntry<float> MessageMinIntervalSeconds;

        // ---- Season ---------------------------------------------------------------------
        public static ConfigEntry<int>   SeasonLengthDays;
        public static ConfigEntry<float> SeasonIntervalSeconds;
        public static ConfigEntry<bool>  AnnounceSeasonChange;

        // ---- Biome state ----------------------------------------------------------------
        public static ConfigEntry<float> BiomeStateIntervalSeconds;
        public static ConfigEntry<int>   BiomeContactRadiusZones;
        public static ConfigEntry<int>   BiomeMaxZonesPerTick;
        public static ConfigEntry<float> BiomeRecoveryPerHour;
        public static ConfigEntry<float> BiomeFrostPressurePerHour;

        // ---- Weather and storms ---------------------------------------------------------
        public static ConfigEntry<float>  WeatherIntervalSeconds;
        public static ConfigEntry<float>  StormMinIntervalSeconds;
        public static ConfigEntry<float>  StormMaxIntervalSeconds;
        public static ConfigEntry<float>  StormDurationSeconds;
        public static ConfigEntry<float>  StormRangeMeters;
        public static ConfigEntry<float>  StormPlagueSpreadMultiplier;
        public static ConfigEntry<bool>   StormsForceWeather;
        public static ConfigEntry<string> StormForcedEnvironment;
        public static ConfigEntry<string> StormDryEnvironment;
        public static ConfigEntry<float>  StormDryChance;
        public static ConfigEntry<float>  StormAvoidBaseMeters;

        // ---- Wind -----------------------------------------------------------------------
        public static ConfigEntry<float> WindIntervalSeconds;

        // ---- Fire (bridge to FireFront) -------------------------------------------------
        public static ConfigEntry<float> FireScorchIntervalSeconds;
        public static ConfigEntry<float> FireScorchPerMinute;
        public static ConfigEntry<bool>  StormLightningEnabled;
        public static ConfigEntry<float> LightningMeanMinutes;
        public static ConfigEntry<float> LightningRingMinMeters;
        public static ConfigEntry<float> LightningRingMaxMeters;
        public static ConfigEntry<float> LightningStandoffMeters;
        public static ConfigEntry<float> LightningIgniteRadiusMeters;

        // ---- Plague ---------------------------------------------------------------------
        public static ConfigEntry<float> PlagueSpreadIntervalSeconds;
        public static ConfigEntry<float> PlagueGrowthPerHour;
        public static ConfigEntry<float> PlagueCorruptionBoost;
        public static ConfigEntry<float> PlagueSpreadThreshold;
        public static ConfigEntry<float> PlagueSeedAmount;
        public static ConfigEntry<float> PlagueSpreadChance;
        public static ConfigEntry<int>   PlagueMaxSpreadsPerTick;

        // ---- World state ----------------------------------------------------------------
        public static ConfigEntry<float> WorldStateIntervalSeconds;
        public static ConfigEntry<float> WorldFlourishingBurden;
        public static ConfigEntry<float> WorldAilingBurden;
        public static ConfigEntry<float> WorldStrickenBurden;
        public static ConfigEntry<float> WorldStormBurden;

        // ---- Ecology --------------------------------------------------------------------
        public static ConfigEntry<float> EcologyIntervalSeconds;
        public static ConfigEntry<float> EcologyCorruptionPerHour;
        public static ConfigEntry<float> EcologyPlagueThreshold;
        public static ConfigEntry<float> EcologyScorchThreshold;

        // ---- Farming --------------------------------------------------------------------
        public static ConfigEntry<float>  FarmingIntervalSeconds;
        public static ConfigEntry<float>  FarmingDepletionPerCropHour;
        public static ConfigEntry<string> FarmingCropPrefabs;
        public static ConfigEntry<float>  FarmingGrowthSlowdownAtFull;
        public static ConfigEntry<bool>   PlagueGenesisEnabled;
        public static ConfigEntry<float>  PlagueGenesisMeanHours;

        // ---- Titles ---------------------------------------------------------------------
        public static ConfigEntry<float> TitleIntervalSeconds;
        public static ConfigEntry<float> WinterbornSeconds;
        public static ConfigEntry<bool>  AnnounceTitles;

        // ---- Zone sync + client visuals ---------------------------------------------------
        public static ConfigEntry<float> ZoneSyncIntervalSeconds;
        public static ConfigEntry<int>   ZoneSyncRadiusZones;
        public static ConfigEntry<bool>  PlagueFogEnabled;
        public static ConfigEntry<float> PlagueFogDensity;
        public static ConfigEntry<bool>  FrostBreathEnabled;
        public static ConfigEntry<float> FrostBreathFloor;
        public static ConfigEntry<bool>  ScorchAshEnabled;
        public static ConfigEntry<float> ScorchAshDensity;

        // ---- Health ---------------------------------------------------------------------
        public static ConfigEntry<float> HealthIntervalSeconds;
        public static ConfigEntry<float> ExposureMinutesToMax;
        public static ConfigEntry<float> ExposureRecoveryMinutes;
        public static ConfigEntry<float> ExposureRestedRecoveryMultiplier;
        public static ConfigEntry<float> ExposurePoisonResistMultiplier;
        public static ConfigEntry<float> ExposureTier1;
        public static ConfigEntry<float> ExposureTier2;
        public static ConfigEntry<float> ExposureTier3;
        public static ConfigEntry<float> SicknessStaminaRegenAtTier1;
        public static ConfigEntry<float> SicknessStaminaRegenAtMax;
        public static ConfigEntry<float> SicknessHealthRegenAtTier2;
        public static ConfigEntry<float> SicknessHealthRegenAtMax;
        public static ConfigEntry<bool>  FrostChillEnabled;
        public static ConfigEntry<float> FrostChillThreshold;
        public static ConfigEntry<float> ChillStaminaRegenMultiplier;
        public static ConfigEntry<float> ChillHealthRegenMultiplier;

        // ---- Consequence ----------------------------------------------------------------
        public static ConfigEntry<float>  ConsequenceIntervalSeconds;
        public static ConfigEntry<bool>   ConsequenceBarren;
        public static ConfigEntry<bool>   ConsequenceEmpower;
        public static ConfigEntry<bool>   ConsequenceSicken;
        public static ConfigEntry<bool>   ConsequenceWither;
        public static ConfigEntry<bool>   AnnounceConsequences;
        public static ConfigEntry<float>  BarrenPlagueThreshold;
        public static ConfigEntry<float>  BarrenScorchThreshold;
        public static ConfigEntry<float>  SickenPlagueThreshold;
        public static ConfigEntry<float>  SickenSpeedPenalty;
        public static ConfigEntry<float>  EmpowerCorruptionThreshold;
        public static ConfigEntry<float>  EmpowerLevelUpMultiplierAtFull;
        public static ConfigEntry<float>  CropWitherBlightThreshold;
        public static ConfigEntry<string> WildlifePrefabs;

        // ---- Rivalry --------------------------------------------------------------------
        public static ConfigEntry<float> RivalryIntervalSeconds;
        public static ConfigEntry<float> RivalryHalfLifeHours;
        public static ConfigEntry<float> CarePerHealedPoint;
        public static ConfigEntry<float> TendingCarePerPlant;
        public static ConfigEntry<float> ArsonHarmPerScorchPoint;
        public static ConfigEntry<float> GrudgeScale;
        public static ConfigEntry<float> GrudgePickRefuse;
        public static ConfigEntry<float> AshbringerGrudge;
        public static ConfigEntry<float> CareDominanceFloor;
        public static ConfigEntry<float> HarmDominanceFloor;
        public static ConfigEntry<float> ContestHysteresis;
        public static ConfigEntry<bool>  AnnounceContests;
        public static ConfigEntry<float> MercyRecoveryBonus;
        public static ConfigEntry<float> MercySicknessBonus;
        public static ConfigEntry<int>   WardenZonesHeld;
        public static ConfigEntry<int>   DespoilerZonesHeld;
        public static ConfigEntry<float> ContestBlightThreshold;
        public static ConfigEntry<float> ContestCareThreshold;
        public static ConfigEntry<float> StormContestMultiplier;
        public static ConfigEntry<float> ContestStarBonus;
        public static ConfigEntry<float> ContestWildSpawnChance;
        public static ConfigEntry<bool>  EnableNemesis;
        public static ConfigEntry<int>   NemesisMaxLevel;

        // ---- Task 14: relics -------------------------------------------------------------

        public static ConfigEntry<float>  RelicIntervalSeconds;
        public static ConfigEntry<float>  FireRelicPeakThreshold;
        public static ConfigEntry<float>  PlagueRelicPeakThreshold;
        public static ConfigEntry<float>  RelicBlessedRecoveryMult;
        public static ConfigEntry<float>  RelicCursedRecoveryMult;
        public static ConfigEntry<float>  RelicBlessedExposureDrainMult;
        public static ConfigEntry<float>  RelicCursedExposureAccrualMult;
        public static ConfigEntry<float>  RelicCursedStarBonus;
        public static ConfigEntry<float>  RelicVandalHarm;
        public static ConfigEntry<string> RelicPrefabCandidates;
        public static ConfigEntry<bool>   RelicRunesEnabled;

        // ---- Per-system master switches -------------------------------------------------

        public static ConfigEntry<bool> EnableSeason;
        public static ConfigEntry<bool> EnableWeather;
        public static ConfigEntry<bool> EnableWind;
        public static ConfigEntry<bool> EnableBiomeState;
        public static ConfigEntry<bool> EnableFire;
        public static ConfigEntry<bool> EnablePlague;
        public static ConfigEntry<bool> EnableEcology;
        public static ConfigEntry<bool> EnableFarming;
        public static ConfigEntry<bool> EnableHealth;
        public static ConfigEntry<bool> EnableConsequence;
        public static ConfigEntry<bool> EnableRivalry;
        public static ConfigEntry<bool> EnableRelic;
        public static ConfigEntry<bool> EnableTitle;
        public static ConfigEntry<bool> EnableWorldState;
        public static ConfigEntry<bool> EnableZoneSync;

        /// <summary>
        /// The stamped config layout version. Owners should not edit it: lowering it re-runs a
        /// migration that has already happened, raising it skips one that has not.
        /// </summary>
        public static ConfigEntry<int> ConfigVersion;

        public static void Bind(ConfigFile cfg, ConfigFile advanced)
        {
            // BEFORE THE FIRST BIND, and that is the mechanism rather than a tidiness preference.
            // A backfill acts on a key being ABSENT from the file, and BepInEx's own Bind makes it
            // present at its shipped default. Snapshot after binding and every backfill quietly
            // becomes a no-op that still logs success and still stamps its version.
            ConfigMigration.Begin(cfg, advanced);
            try
            {
                // ==== 01 - General ====================================================================

                VerboseLogging = cfg.Bind(GeneralSection, "VerboseLogging", false,
                    "Prints a detailed log line for every pass of every system, instead of just summaries. " +
                    "Turn it on to see the mod actually working, or while chasing down a problem.");

                // ---- 01 - General, advanced file ----

                TickBudgetMs = advanced.Bind(GeneralSection, "TickBudgetMs", 2.0f,
                    new ConfigDescription(
                        "Milliseconds of work this mod may do each frame, across every system combined. Raise " +
                        "it only if systems visibly fall behind on a server with room to spare.",
                        new AcceptableValueRange<float>(0.25f, 16.0f)));

                MaxCreditSeconds = advanced.Bind(GeneralSection, "MaxCreditSeconds", 86400f,
                    new ConfigDescription(
                        "Caps how much real time, in seconds, a zone can catch up on drift the moment someone " +
                        "visits it. Stops a zone left alone for months from getting months of built-up change " +
                        "all at once.",
                        new AcceptableValueRange<float>(60f, 2592000f)));

                AutosaveIntervalSeconds = advanced.Bind(GeneralSection, "AutosaveIntervalSeconds", 120f,
                    new ConfigDescription(
                        "Seconds between writes of the world's drift data to disk. A save always happens on " +
                        "shutdown regardless, so this only limits how much a crash could lose. Set to 0 to turn " +
                        "off the periodic writes.",
                        new AcceptableValueRange<float>(0f, 3600f)));

                MessageMinIntervalSeconds = advanced.Bind(GeneralSection, "MessageMinIntervalSeconds", 8.0f,
                    new ConfigDescription(
                        "Minimum seconds between the on-screen messages this mod shows, so a burst of nearby " +
                        "events cannot spam the screen at once. Doesn't apply to server-wide announcements such " +
                        "as a storm arriving or a season changing.",
                        new AcceptableValueRange<float>(0f, 300f)));

                // ==== 02 - Season =====================================================================

                EnableSeason = cfg.Bind(SeasonSection, "EnableSeason", true,
                    "Turns season tracking on or off. The season it tracks feeds fire risk, plague growth, " +
                    "farming yield and frost buildup elsewhere in this mod.");

                SeasonLengthDays = cfg.Bind(SeasonSection, "SeasonLengthDays", 7,
                    new ConfigDescription(
                        "In-game days per season, when this mod is running its own season clock. Ignored " +
                        "completely if Seasonality or Seasons (shudnal) is installed - their season is used " +
                        "instead.",
                        new AcceptableValueRange<int>(1, 120)));

                AnnounceSeasonChange = cfg.Bind(SeasonSection, "AnnounceSeasonChange", true,
                    "Shows an on-screen message when the season changes. Automatically skipped if " +
                    "Seasonality or Seasons (shudnal) is installed, since they already show the player the " +
                    "season.");

                // ---- 02 - Season, advanced file ----

                // Every other IWorldSystem in this mod already takes its cadence from config; this
                // was the one of fifteen still returning a literal, which is the kind of gap nobody
                // notices because the value is fine. 10s is what it has always run at.
                SeasonIntervalSeconds = advanced.Bind(SeasonSection, "SeasonIntervalSeconds", 10f,
                    new ConfigDescription(
                        "Seconds between season checks, and how often the server tells clients the current " +
                        "season. A player who joins mid-session is right within one of these.",
                        new AcceptableValueRange<float>(1f, 300f)));

                // ==== 03 - Weather ====================================================================

                EnableWeather = cfg.Bind(WeatherSection, "EnableWeather", true,
                    "Turns weather tracking and Devastating Storms on or off. A storm brings an on-screen " +
                    "banner, gameplay effects across its area, and a chance of lightning.");

                StormMinIntervalSeconds = cfg.Bind(WeatherSection, "StormMinIntervalSeconds", 3600f,
                    new ConfigDescription(
                        "Shortest real-world gap allowed between storms, in seconds. No new storm can begin " +
                        "until at least this long has passed since the last one ended.",
                        new AcceptableValueRange<float>(60f, 86400f)));

                StormMaxIntervalSeconds = cfg.Bind(WeatherSection, "StormMaxIntervalSeconds", 10800f,
                    new ConfigDescription(
                        "Longest real-world gap between storms, in seconds. The chance of a storm starting " +
                        "climbs from zero at the minimum gap to certain by this point.",
                        new AcceptableValueRange<float>(120f, 172800f)));

                StormDurationSeconds = cfg.Bind(WeatherSection, "StormDurationSeconds", 300f,
                    new ConfigDescription(
                        "How long a Devastating Storm lasts once it starts, in game seconds. It runs its full " +
                        "course and ends on schedule whether or not any player is nearby.",
                        new AcceptableValueRange<float>(30f, 3600f)));

                StormsForceWeather = cfg.Bind(WeatherSection, "StormsForceWeather", false,
                    "Gives a storm its own stormy sky instead of the world's weather. Left off, this mod " +
                    "never touches the sky, avoiding a fight with another weather mod. Every player needs " +
                    "the same value and a full restart to see it.");

                StormForcedEnvironment = cfg.Bind(WeatherSection, "StormForcedEnvironment", "ThunderStorm",
                    "The sky shown during a storm that rolls wet, used only when StormsForceWeather is on. " +
                    "ThunderStorm is rainy, and rain stops storm lightning from striking.");

                StormDryEnvironment = cfg.Bind(WeatherSection, "StormDryEnvironment", "Eikthyr",
                    "The sky shown during a storm that rolls dry, used only when StormsForceWeather is on. " +
                    "Eikthyr is dark and thundery with no rain, so lightning can strike.");

                StormDryChance = cfg.Bind(WeatherSection, "StormDryChance", 0.5f,
                    new ConfigDescription(
                        "Chance that a storm rolls dry rather than wet, decided once when it starts. 0 makes " +
                        "every storm wet and 1 makes every storm dry, with values in between giving each a " +
                        "proportional chance.",
                        new AcceptableValueRange<float>(0f, 1f)));

                // ---- 03 - Weather, advanced file ----

                EnableWind = advanced.Bind(WeatherSection, "EnableWind", true,
                    "Turns wind tracking on or off. It reads the game's own wind for other systems to use " +
                    "later - nothing currently changes if you turn it off.");

                WeatherIntervalSeconds = advanced.Bind(WeatherSection, "WeatherIntervalSeconds", 5f,
                    new ConfigDescription(
                        "Seconds between weather checks - how quickly a storm's start or end is noticed. Kept " +
                        "low on purpose, since this only reads state rather than computing anything heavy.",
                        new AcceptableValueRange<float>(1f, 60f)));

                StormRangeMeters = advanced.Bind(WeatherSection, "StormRangeMeters", 96f,
                    new ConfigDescription(
                        "Radius of a storm's effect, in metres. The on-screen banner and every gameplay effect " +
                        "of the storm use this same distance, so they always agree on where it reaches.",
                        new AcceptableValueRange<float>(32f, 1024f)));

                StormPlagueSpreadMultiplier = advanced.Bind(WeatherSection, "StormPlagueSpreadMultiplier", 1.5f,
                    new ConfigDescription(
                        "How much faster plague spreads inside a Devastating Storm. 1.5 means plague spreads " +
                        "50% faster within the storm's range than it does outside it.",
                        new AcceptableValueRange<float>(0f, 10f)));

                StormAvoidBaseMeters = advanced.Bind(WeatherSection, "StormAvoidBaseMeters", 30f,
                    new ConfigDescription(
                        "Storms will not anchor within this many metres of anything player-built. If every " +
                        "online player is that close to their base, the storm holds off until someone steps " +
                        "into the wild.",
                        new AcceptableValueRange<float>(0f, 64f)));

                WindIntervalSeconds = advanced.Bind(WeatherSection, "WindIntervalSeconds", 5f,
                    new ConfigDescription(
                        "Seconds between wind readings taken from the game. Wind is only ever read here, never " +
                        "changed.",
                        new AcceptableValueRange<float>(1f, 60f)));

                // ==== 04 - Biome state ================================================================

                EnableBiomeState = cfg.Bind(BiomeSection, "EnableBiomeState", true,
                    "Turns biome state on or off — the zone-by-zone fertility, corruption, plague, scorch " +
                    "and frost that slowly change and heal where players go.");

                // ---- 04 - Biome state, advanced file ----

                BiomeStateIntervalSeconds = advanced.Bind(BiomeSection, "BiomeStateIntervalSeconds", 30f,
                    new ConfigDescription(
                        "How often, in seconds, zone fertility, corruption, scorch and frost are recalculated. " +
                        "Lower reacts to players faster; higher costs less.",
                        new AcceptableValueRange<float>(5f, 600f)));

                BiomeContactRadiusZones = advanced.Bind(BiomeSection, "BiomeContactRadiusZones", 1,
                    new ConfigDescription(
                        "How many zones around each player count as their presence, for drift and plague " +
                        "spread. 0 is only their own zone; 1 covers the surrounding 3x3.",
                        new AcceptableValueRange<int>(0, 3)));

                BiomeMaxZonesPerTick = advanced.Bind(BiomeSection, "BiomeMaxZonesPerTick", 64,
                    new ConfigDescription(
                        "Most zones updated in a single drift pass. Anything left over continues on the next " +
                        "pass, so nothing is skipped, only delayed.",
                        new AcceptableValueRange<int>(1, 1024)));

                BiomeRecoveryPerHour = advanced.Bind(BiomeSection, "BiomeRecoveryPerHour", 0.02f,
                    new ConfigDescription(
                        "How fast fertility, corruption, plague, scorch and frost heal per hour, on a 0 to 1 " +
                        "scale. At the default, fully damaged land heals in about 50 hours.",
                        new AcceptableValueRange<float>(0f, 1f)));

                BiomeFrostPressurePerHour = advanced.Bind(BiomeSection, "BiomeFrostPressurePerHour", 0.015f,
                    new ConfigDescription(
                        "How fast frost builds up per hour in cold seasons, before the season's own cold " +
                        "multiplier. Set to 0 to stop frost building at all.",
                        new AcceptableValueRange<float>(0f, 1f)));

                // ==== 05 - Fire =======================================================================

                EnableFire = cfg.Bind(FireSection, "EnableFire", true,
                    "Turns fire memory on or off — burned zones gain scorch that fades over time. Needs " +
                    "FireFront installed; does nothing without it.");

                StormLightningEnabled = cfg.Bind(FireSection, "StormLightningEnabled", true,
                    "Turns storm lightning on or off: a rare bolt during a Devastating Storm that can start " +
                    "a fire nearby, never in rain. Needs FireFront installed with its own fire spread " +
                    "enabled, or nothing happens.");

                LightningMeanMinutes = cfg.Bind(FireSection, "LightningMeanMinutes", 15f,
                    new ConfigDescription(
                        "Average minutes between lightning bolts while a storm holds at least one player under " +
                        "a dry sky. Higher makes strikes rarer.",
                        new AcceptableValueRange<float>(1f, 600f)));

                // ---- 05 - Fire, advanced file ----

                FireScorchIntervalSeconds = advanced.Bind(FireSection, "FireScorchIntervalSeconds", 10f,
                    new ConfigDescription(
                        "How often, in seconds, burning zones gain scorch. Only matters while FireFront is " +
                        "installed.",
                        new AcceptableValueRange<float>(2f, 120f)));

                FireScorchPerMinute = advanced.Bind(FireSection, "FireScorchPerMinute", 0.02f,
                    new ConfigDescription(
                        "Scorch added per minute to a zone with any fire burning in it — the same rate whether " +
                        "one fire burns there or several. At the default, continuous burning fully chars a zone " +
                        "in about 50 minutes.",
                        new AcceptableValueRange<float>(0f, 1f)));

                LightningRingMinMeters = advanced.Bind(FireSection, "LightningRingMinMeters", 15f,
                    new ConfigDescription(
                        "Closest a lightning bolt can land to the player it strikes near. Kept well clear of " +
                        "point-blank, so a strike is a threat, never a targeted hit.",
                        new AcceptableValueRange<float>(0f, 50f)));

                LightningRingMaxMeters = advanced.Bind(FireSection, "LightningRingMaxMeters", 40f,
                    new ConfigDescription(
                        "Farthest a lightning bolt can land from the player it strikes near. Kept close enough " +
                        "that whoever it lands near can still hear it.",
                        new AcceptableValueRange<float>(10f, 60f)));

                LightningStandoffMeters = advanced.Bind(FireSection, "LightningStandoffMeters", 30f,
                    new ConfigDescription(
                        "No lightning bolt lands within this distance of anything player-built — pieces and " +
                        "planted crops alike. A blocked bolt is simply lost, not rerolled.",
                        new AcceptableValueRange<float>(0f, 64f)));

                LightningIgniteRadiusMeters = advanced.Bind(FireSection, "LightningIgniteRadiusMeters", 2.5f,
                    new ConfigDescription(
                        "Ground radius set alight at a lightning strike, in metres. Kept small on purpose — one " +
                        "bolt starts one fire, and the weather and land decide what it becomes.",
                        new AcceptableValueRange<float>(0.5f, 8f)));

                // ==== 06 - Plague =====================================================================

                EnablePlague = cfg.Bind(PlagueSection, "EnablePlague", true,
                    "Turns plague on or off — sickness that spreads between zones, grows or heals with the " +
                    "seasons, and can sicken players who linger in it.");

                PlagueGenesisEnabled = cfg.Bind(PlagueSection, "PlagueGenesisEnabled", true,
                    "Lets plague start on its own — a rare roll seeds sickness on ground players visit, " +
                    "more likely where it is corrupted or burnt, and more during storms. Off means " +
                    "outbreaks only start by admin command.");

                PlagueGenesisMeanHours = cfg.Bind(PlagueSection, "PlagueGenesisMeanHours", 12f,
                    new ConfigDescription(
                        "Average real hours of played time between new outbreaks starting on clean ground. " +
                        "Blighted ground shortens this by up to five times; a storm overhead shortens it " +
                        "further.",
                        new AcceptableValueRange<float>(0.5f, 500f)));

                // ---- 06 - Plague, advanced file ----

                PlagueSpreadIntervalSeconds = advanced.Bind(PlagueSection, "PlagueSpreadIntervalSeconds", 60f,
                    new ConfigDescription(
                        "How often, in seconds, an infected zone can spread plague to a clean neighbour. Growth " +
                        "and healing within an already-sick zone run on their own separate pace.",
                        new AcceptableValueRange<float>(10f, 600f)));

                PlagueGrowthPerHour = advanced.Bind(PlagueSection, "PlagueGrowthPerHour", 0.03f,
                    new ConfigDescription(
                        "How much plague grows per hour in an already-infected zone a player has visited, " +
                        "before the season's own multiplier.",
                        new AcceptableValueRange<float>(0f, 1f)));

                PlagueCorruptionBoost = advanced.Bind(PlagueSection, "PlagueCorruptionBoost", 1.0f,
                    new ConfigDescription(
                        "How strongly corrupted ground speeds up plague growth there. At the default, fully " +
                        "corrupted ground doubles how fast plague grows.",
                        new AcceptableValueRange<float>(0f, 4f)));

                PlagueSpreadThreshold = advanced.Bind(PlagueSection, "PlagueSpreadThreshold", 0.5f,
                    new ConfigDescription(
                        "Plague level a zone must reach before it can infect its neighbouring zones, on a 0 to " +
                        "1 scale.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                PlagueSeedAmount = advanced.Bind(PlagueSection, "PlagueSeedAmount", 0.05f,
                    new ConfigDescription(
                        "Plague level a zone starts at the moment it is newly infected, on a 0 to 1 scale.",
                        new AcceptableValueRange<float>(0.01f, 0.5f)));

                PlagueSpreadChance = advanced.Bind(PlagueSection, "PlagueSpreadChance", 0.25f,
                    new ConfigDescription(
                        "Chance, checked on each spread pass, that a given neighbouring zone catches plague — " +
                        "rolled once per zone even if several infected zones border it.",
                        new AcceptableValueRange<float>(0f, 1f)));

                PlagueMaxSpreadsPerTick = advanced.Bind(PlagueSection, "PlagueMaxSpreadsPerTick", 16,
                    new ConfigDescription(
                        "Most zones plague can spread into in a single pass, as a safety limit against a large " +
                        "outbreak all rolling successfully at once.",
                        new AcceptableValueRange<int>(1, 256)));

                // ==== 07 - Ecology ====================================================================

                EnableEcology = cfg.Bind(EcologySection, "EnableEcology", true,
                    "Turns land corruption on or off. Corruption is the lasting scar heavy plague or fire " +
                    "damage leaves behind, and left unchecked it feeds back into faster plague growth.");

                // ---- 07 - Ecology, advanced file ----

                EcologyIntervalSeconds = advanced.Bind(EcologySection, "EcologyIntervalSeconds", 60f,
                    new ConfigDescription(
                        "How often, in seconds, the game checks blighted zones for new corruption. Lower checks " +
                        "more often for a small extra cost; it does not change how fast corruption itself " +
                        "builds.",
                        new AcceptableValueRange<float>(10f, 600f)));

                EcologyCorruptionPerHour = advanced.Bind(EcologySection, "EcologyCorruptionPerHour", 0.01f,
                    new ConfigDescription(
                        "Base rate for how fast corruption builds in a zone whose plague or scorch has reached " +
                        "its threshold; the actual rate climbs further as plague or scorch gets worse. Set to 0 " +
                        "to stop corruption entirely.",
                        new AcceptableValueRange<float>(0f, 1f)));

                EcologyPlagueThreshold = advanced.Bind(EcologySection, "EcologyPlagueThreshold", 0.3f,
                    new ConfigDescription(
                        "Plague level at which a zone's land begins to corrupt, feeding a slow build-up that " +
                        "outlasts the outbreak itself.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                EcologyScorchThreshold = advanced.Bind(EcologySection, "EcologyScorchThreshold", 0.3f,
                    new ConfigDescription(
                        "Scorch (fire-damage) level at which a zone's land begins to corrupt, feeding the same " +
                        "slow build-up that plague damage does.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                // ==== 08 - Farming ====================================================================

                EnableFarming = cfg.Bind(FarmingSection, "EnableFarming", true,
                    "Turns crop soil fatigue on or off. Heavily planted zones tire out and grow crops more " +
                    "slowly until the land is given a rest. " +
                    "Read on each player's own game, so give every player the same value.");

                // ---- 08 - Farming, advanced file ----

                FarmingIntervalSeconds = advanced.Bind(FarmingSection, "FarmingIntervalSeconds", 45f,
                    new ConfigDescription(
                        "Seconds between sweeps that count how many crops are growing in each zone. Kept off a " +
                        "round number on purpose so it does not land on the same moment as AwayFromHome's own " +
                        "scan.",
                        new AcceptableValueRange<float>(10f, 600f)));

                FarmingDepletionPerCropHour = advanced.Bind(FarmingSection, "FarmingDepletionPerCropHour", 0.002f,
                    new ConfigDescription(
                        "How much each standing crop tires its zone's soil per hour. At the default, a field of " +
                        "25 crops fully tires the land in about 20 hours of real playtime; resting the field " +
                        "lets it recover.",
                        new AcceptableValueRange<float>(0f, 0.5f)));

                FarmingGrowthSlowdownAtFull = advanced.Bind(FarmingSection, "FarmingGrowthSlowdownAtFull", 2f,
                    new ConfigDescription(
                        "How much longer crops take to grow on fully tired soil, as a multiplier on grow time; " +
                        "1 turns the slowdown off. Applied on players' own games, so set the same value on " +
                        "every player's game.",
                        new AcceptableValueRange<float>(1f, 5f)));

                FarmingCropPrefabs = advanced.Bind(FarmingSection, "FarmingCropPrefabs", "sapling_carrot,sapling_turnip,sapling_onion,sapling_barley,sapling_flax,sapling_seedcarrot,sapling_seedturnip,sapling_seedonion,sapling_jotunpuffs,sapling_magecap",
                    "Comma-separated list of crop names counted as farmland. Only these are checked for " +
                    "soil depletion and slower growth on tired soil; anything not listed is unaffected. " +
                    "Read on each player's own game, so give every player the same value.");

                // ==== 09 - Health =====================================================================

                EnableHealth = cfg.Bind(HealthSection, "EnableHealth", true,
                    "Turns plague sickness and frost chill on or off. Standing on tainted or bitterly cold " +
                    "ground weakens stamina and health regen until the player leaves or recovers. " +
                    "Read on each player's own game, so give every player the same value.");

                FrostChillEnabled = cfg.Bind(HealthSection, "FrostChillEnabled", true,
                    "Turns frost chill on or off: high zone frost slows stamina and health regen where " +
                    "vanilla would not call it cold. Fire, shelter and frost resistance cancel it. Read on " +
                    "each player's own game.");

                // ---- 09 - Health, advanced file ----

                HealthIntervalSeconds = advanced.Bind(HealthSection, "HealthIntervalSeconds", 5f,
                    new ConfigDescription(
                        "How often, in seconds, online players are checked for plague exposure. Lower catches " +
                        "someone stepping into an outbreak sooner, at a small extra cost.",
                        new AcceptableValueRange<float>(1f, 60f)));

                ExposureMinutesToMax = advanced.Bind(HealthSection, "ExposureMinutesToMax", 30f,
                    new ConfigDescription(
                        "Minutes of standing on fully plagued ground before a player's sickness reaches its " +
                        "worst. Ground that is only partly tainted builds sickness proportionally slower.",
                        new AcceptableValueRange<float>(5f, 240f)));

                ExposureRecoveryMinutes = advanced.Bind(HealthSection, "ExposureRecoveryMinutes", 20f,
                    new ConfigDescription(
                        "Minutes for a player's sickness to clear fully once they leave plagued ground, from " +
                        "its worst back to none.",
                        new AcceptableValueRange<float>(2f, 240f)));

                ExposureRestedRecoveryMultiplier = advanced.Bind(HealthSection, "ExposureRestedRecoveryMultiplier", 2f,
                    new ConfigDescription(
                        "How much faster sickness clears while the player has the game's own Rested status. At " +
                        "the default, being Rested roughly doubles recovery speed.",
                        new AcceptableValueRange<float>(1f, 10f)));

                ExposurePoisonResistMultiplier = advanced.Bind(HealthSection, "ExposurePoisonResistMultiplier", 0.5f,
                    new ConfigDescription(
                        "How much slower sickness builds up while poison-resistant, from any mead, gear or " +
                        "food. At the default, protection halves how fast exposure builds.",
                        new AcceptableValueRange<float>(0f, 1f)));

                ExposureTier1 = advanced.Bind(HealthSection, "ExposureTier1", 0.25f,
                    new ConfigDescription(
                        "Exposure level at which the sickness first appears: the status icon shows and stamina " +
                        "regen starts to suffer. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.01f, 1f)));

                ExposureTier2 = advanced.Bind(HealthSection, "ExposureTier2", 0.5f,
                    new ConfigDescription(
                        "Exposure level at which health regen also starts to suffer, on top of the stamina " +
                        "penalty from the first tier. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.01f, 1f)));

                ExposureTier3 = advanced.Bind(HealthSection, "ExposureTier3", 0.8f,
                    new ConfigDescription(
                        "Exposure level at which the sickness is announced as being at its worst. Only changes " +
                        "that announcement — the regen penalties already ramp smoothly past this point. " +
                        "Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.01f, 1f)));

                SicknessStaminaRegenAtTier1 = advanced.Bind(HealthSection, "SicknessStaminaRegenAtTier1", 0.85f,
                    new ConfigDescription(
                        "Stamina regen multiplier the instant the first sickness tier is crossed, so the " +
                        "penalty is felt right away rather than easing in unnoticed. Set the same value on " +
                        "every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                SicknessStaminaRegenAtMax = advanced.Bind(HealthSection, "SicknessStaminaRegenAtMax", 0.3f,
                    new ConfigDescription(
                        "Stamina regen multiplier at full exposure, easing down from the first tier's penalty " +
                        "as exposure climbs. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                SicknessHealthRegenAtTier2 = advanced.Bind(HealthSection, "SicknessHealthRegenAtTier2", 0.8f,
                    new ConfigDescription(
                        "Health regen multiplier the instant the second sickness tier is crossed — the wound " +
                        "half of the sickness arriving after the stamina fails. Set the same value on every " +
                        "player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                SicknessHealthRegenAtMax = advanced.Bind(HealthSection, "SicknessHealthRegenAtMax", 0.38f,
                    new ConfigDescription(
                        "Health regen multiplier at full exposure, easing down from the second tier's penalty " +
                        "as exposure climbs. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                FrostChillThreshold = advanced.Bind(HealthSection, "FrostChillThreshold", 0.5f,
                    new ConfigDescription(
                        "Zone frost level at which the chill effect takes hold of a player standing in it. Set " +
                        "the same value on every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                ChillStaminaRegenMultiplier = advanced.Bind(HealthSection, "ChillStaminaRegenMultiplier", 0.8f,
                    new ConfigDescription(
                        "Stamina regen multiplier while chilled. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                ChillHealthRegenMultiplier = advanced.Bind(HealthSection, "ChillHealthRegenMultiplier", 0.7f,
                    new ConfigDescription(
                        "Health regen multiplier while chilled. Set the same value on every player's game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                // ==== 10 - Consequence ================================================================

                EnableConsequence = cfg.Bind(ConsequenceSection, "EnableConsequence", true,
                    "Turns land consequences on or off: barren pickables, tougher spawns, sickened wildlife " +
                    "and dying crops on badly plagued, scorched or corrupted ground. " +
                    "Read on each player's own game, so give every player the same value.");

                ConsequenceBarren = cfg.Bind(ConsequenceSection, "ConsequenceBarren", true,
                    "Stops berries, mushrooms and other pickables from being harvested on badly plagued or " +
                    "scorched ground, with an in-world message explaining why. " +
                    "Read on each player's own game, so give every player the same value.");

                ConsequenceEmpower = cfg.Bind(ConsequenceSection, "ConsequenceEmpower", true,
                    "Gives hostile creatures a better chance of spawning as a stronger, starred variant on " +
                    "badly corrupted ground. Passive wildlife is never affected. " +
                    "Read on each player's own game, so give every player the same value.");

                ConsequenceSicken = cfg.Bind(ConsequenceSection, "ConsequenceSicken", true,
                    "Slows and sickens passive wildlife (deer, boars, hares) standing on plagued ground. " +
                    "The effect wears off once the animal leaves or the plague clears. " +
                    "Read on each player's own game, so give every player the same value.");

                ConsequenceWither = cfg.Bind(ConsequenceSection, "ConsequenceWither", true,
                    "Kills crops planted in badly blighted soil once they would otherwise finish growing. " +
                    "Replanting after the land recovers is the fix. " +
                    "Read on each player's own game, so give every player the same value.");

                AnnounceConsequences = cfg.Bind(ConsequenceSection, "AnnounceConsequences", true,
                    "Shows a one-line message the first time a player enters a zone with barren ground, " +
                    "tougher spawns, sickness or dying crops. Sent once per zone, per session.");

                // ---- 10 - Consequence, advanced file ----

                ConsequenceIntervalSeconds = advanced.Bind(ConsequenceSection, "ConsequenceIntervalSeconds", 10f,
                    new ConfigDescription(
                        "Seconds between checks that announce a zone's consequences to players near it. The " +
                        "effects themselves apply continuously regardless of this setting — it only paces the " +
                        "announcement.",
                        new AcceptableValueRange<float>(2f, 120f)));

                BarrenPlagueThreshold = advanced.Bind(ConsequenceSection, "BarrenPlagueThreshold", 0.4f,
                    new ConfigDescription(
                        "Plague level in a zone at or above which pickables there stop yielding anything. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                BarrenScorchThreshold = advanced.Bind(ConsequenceSection, "BarrenScorchThreshold", 0.5f,
                    new ConfigDescription(
                        "Scorch level in a zone at or above which pickables there stop yielding anything — ash " +
                        "bears nothing. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                SickenPlagueThreshold = advanced.Bind(ConsequenceSection, "SickenPlagueThreshold", 0.4f,
                    new ConfigDescription(
                        "Plague level in a zone at or above which passive wildlife there starts to sicken. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                SickenSpeedPenalty = advanced.Bind(ConsequenceSection, "SickenSpeedPenalty", 0.35f,
                    new ConfigDescription(
                        "How much slower sickened wildlife moves, as a fraction of its normal speed — for " +
                        "example, 0.5 means half speed. This only slows animals; it never kills them. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0f, 0.9f)));

                EmpowerCorruptionThreshold = advanced.Bind(ConsequenceSection, "EmpowerCorruptionThreshold", 0.5f,
                    new ConfigDescription(
                        "Corruption level in a zone above which hostile spawns start getting better odds of " +
                        "coming up stronger. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                EmpowerLevelUpMultiplierAtFull = advanced.Bind(ConsequenceSection, "EmpowerLevelUpMultiplierAtFull", 6f,
                    new ConfigDescription(
                        "How much better the odds of a stronger spawn get on fully corrupted ground, as a " +
                        "multiplier on the game's own level-up chance. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(1f, 10f)));

                CropWitherBlightThreshold = advanced.Bind(ConsequenceSection, "CropWitherBlightThreshold", 0.6f,
                    new ConfigDescription(
                        "Blight level (whichever is worse, plague or corruption) at which planted crops wither " +
                        "and die outright. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                WildlifePrefabs = advanced.Bind(ConsequenceSection, "WildlifePrefabs", "Deer,Boar,Hare",
                    "Comma-separated list of creature names counted as passive wildlife — these can sicken " +
                    "from plague but are never turned into a stronger spawn by corruption. " +
                    "Read on each player's own game, so give every player the same value.");

                // ==== 11 - Rivalry ====================================================================

                EnableRivalry = cfg.Bind(RivalrySection, "EnableRivalry", true,
                    "Turns the rivalry system on or off. It tracks who helps or harms each area, feeding " +
                    "grudges, contested ground, and titles that reward or shame players for how they treat " +
                    "the land. " +
                    "Read on each player's own game, so give every player the same value.");

                AnnounceContests = cfg.Bind(RivalrySection, "AnnounceContests", true,
                    "Announce contest outcomes to nearby players: an area changing hands between two " +
                    "rivals, or a spawn war on contested ground finally resolving.");

                EnableNemesis = cfg.Bind(RivalrySection, "EnableNemesis", true,
                    "Turns nemesis marking on or off: the creature that kills a player is marked, levelled " +
                    "up and named for who it slew. Read from the killed player's own game, so give every " +
                    "player the same value.");

                NemesisMaxLevel = cfg.Bind(RivalrySection, "NemesisMaxLevel", 3,
                    new ConfigDescription(
                        "Highest level a creature can reach by killing players (level 3 is two stars). Bosses " +
                        "are marked but never levelled. Read from the killed player's own game, so give every " +
                        "player the same value.",
                        new AcceptableValueRange<int>(1, 5)));

                // ---- 11 - Rivalry, advanced file ----

                RivalryIntervalSeconds = advanced.Bind(RivalrySection, "RivalryIntervalSeconds", 30f,
                    new ConfigDescription(
                        "How often the rivalry system re-checks grudges, care, and tending progress, in " +
                        "seconds.",
                        new AcceptableValueRange<float>(5f, 300f)));

                RivalryHalfLifeHours = advanced.Bind(RivalrySection, "RivalryHalfLifeHours", 48f,
                    new ConfigDescription(
                        "Real hours for a player's recorded harm and care in an area to fade by half. Lower " +
                        "makes the land forgive faster; higher makes both grudges and goodwill linger longer.",
                        new AcceptableValueRange<float>(1f, 720f)));

                CarePerHealedPoint = advanced.Bind(RivalrySection, "CarePerHealedPoint", 1f,
                    new ConfigDescription(
                        "Credit booked to players nearby when damaged land heals, split among everyone whose " +
                        "presence covers it — it offsets grudges and counts toward the Warden title.",
                        new AcceptableValueRange<float>(0f, 10f)));

                TendingCarePerPlant = advanced.Bind(RivalrySection, "TendingCarePerPlant", 0.05f,
                    new ConfigDescription(
                        "Credit booked to a crop's planter, once per plant ever planted — replanting the same " +
                        "spot again earns nothing extra.",
                        new AcceptableValueRange<float>(0f, 1f)));

                ArsonHarmPerScorchPoint = advanced.Bind(RivalrySection, "ArsonHarmPerScorchPoint", 1f,
                    new ConfigDescription(
                        "Blame booked against whoever started a fire, per point of scorch it burns into an area " +
                        "— fully charring one area blames them one point at the default.",
                        new AcceptableValueRange<float>(0f, 10f)));

                GrudgeScale = advanced.Bind(RivalrySection, "GrudgeScale", 1f,
                    new ConfigDescription(
                        "How strongly a player's harm to an area, minus any care they've since given it, turns " +
                        "into a grudge against them specifically. Higher turns the land against wrongdoers " +
                        "faster.",
                        new AcceptableValueRange<float>(0f, 10f)));

                GrudgePickRefuse = advanced.Bind(RivalrySection, "GrudgePickRefuse", 0.25f,
                    new ConfigDescription(
                        "Grudge level at which an area's berries and mushrooms refuse to be picked by the " +
                        "player who earned it — everyone else can still pick them. Checked in each player's own " +
                        "game.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                AshbringerGrudge = advanced.Bind(RivalrySection, "AshbringerGrudge", 0.5f,
                    new ConfigDescription(
                        "Grudge level, in a player's single worst area, needed to earn the Ashbringer title.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                CareDominanceFloor = advanced.Bind(RivalrySection, "CareDominanceFloor", 0.2f,
                    new ConfigDescription(
                        "Minimum care a player needs in an area before the land can remember them as its carer " +
                        "— below this, nobody holds that ground.",
                        new AcceptableValueRange<float>(0.05f, 5f)));

                HarmDominanceFloor = advanced.Bind(RivalrySection, "HarmDominanceFloor", 0.2f,
                    new ConfigDescription(
                        "Minimum harm a player needs in an area before the land can remember them as its " +
                        "dominant despoiler.",
                        new AcceptableValueRange<float>(0.05f, 5f)));

                ContestHysteresis = advanced.Bind(RivalrySection, "ContestHysteresis", 0.15f,
                    new ConfigDescription(
                        "How far a challenger's care or harm must exceed the current holder's before they take " +
                        "over an area (0.15 = 15% higher). Stops ground flickering between two close rivals.",
                        new AcceptableValueRange<float>(0f, 1f)));

                MercyRecoveryBonus = advanced.Bind(RivalrySection, "MercyRecoveryBonus", 0.25f,
                    new ConfigDescription(
                        "Extra healing speed for an area while its remembered carer is nearby (0.25 = 25% " +
                        "faster recovery).",
                        new AcceptableValueRange<float>(0f, 2f)));

                MercySicknessBonus = advanced.Bind(RivalrySection, "MercySicknessBonus", 0.5f,
                    new ConfigDescription(
                        "Faster recovery from plague sickness while standing on ground you're remembered as the " +
                        "carer of (0.5 = 50% faster).",
                        new AcceptableValueRange<float>(0f, 3f)));

                WardenZonesHeld = advanced.Bind(RivalrySection, "WardenZonesHeld", 3,
                    new ConfigDescription(
                        "Number of areas a player must be remembered as the carer of, at once, to earn the " +
                        "Warden title.",
                        new AcceptableValueRange<int>(1, 64)));

                DespoilerZonesHeld = advanced.Bind(RivalrySection, "DespoilerZonesHeld", 3,
                    new ConfigDescription(
                        "Number of areas a player must be remembered as the dominant harmer of, at once, to " +
                        "earn the Despoiler title.",
                        new AcceptableValueRange<int>(1, 64)));

                ContestBlightThreshold = advanced.Bind(RivalrySection, "ContestBlightThreshold", 0.5f,
                    new ConfigDescription(
                        "How plagued or corrupted an area must be, whichever is worse, before it can become " +
                        "contested war ground.",
                        new AcceptableValueRange<float>(0.1f, 1f)));

                ContestCareThreshold = advanced.Bind(RivalrySection, "ContestCareThreshold", 0.3f,
                    new ConfigDescription(
                        "Total care, summed across everyone, a blighted area needs before it counts as actively " +
                        "contested rather than simply lost.",
                        new AcceptableValueRange<float>(0.05f, 5f)));

                StormContestMultiplier = advanced.Bind(RivalrySection, "StormContestMultiplier", 2f,
                    new ConfigDescription(
                        "How much fiercer a contested area's war gets while a Devastating Storm passes over it. " +
                        "1 turns this off.",
                        new AcceptableValueRange<float>(1f, 5f)));

                ContestStarBonus = advanced.Bind(RivalrySection, "ContestStarBonus", 1f,
                    new ConfigDescription(
                        "Extra chance for hostile spawns to come up starred, per point of war intensity, on " +
                        "contested ground. Read from the nearby player's own game, not the server's.",
                        new AcceptableValueRange<float>(0f, 5f)));

                ContestWildSpawnChance = advanced.Bind(RivalrySection, "ContestWildSpawnChance", 100f,
                    new ConfigDescription(
                        "Spawn chance, as a percentage, for the wildlife list in a contested zone: the wild " +
                        "answering the war. Never lowers the game's own chance, and never raises the game's " +
                        "own cap on how many can stand there. 0 turns the answer off. Applied from the game of " +
                        "the player standing there, not the server's.",
                        new AcceptableValueRange<float>(0f, 100f)));

                // ==== 12 - Relic ======================================================================

                EnableRelic = cfg.Bind(RelicSection, "EnableRelic", true,
                    "Turns relic stones on or off. Zones where a fire fully heals, a plague is cured, or a " +
                    "spawn war resolves can raise a lasting blessed or cursed landmark that changes how " +
                    "fast the land heals and how tough creatures nearby become. " +
                    "Read on each player's own game, so give every player the same value.");

                // ---- 12 - Relic, advanced file ----

                RelicIntervalSeconds = advanced.Bind(RelicSection, "RelicIntervalSeconds", 30f,
                    new ConfigDescription(
                        "Seconds between checks for a zone whose story has just completed and is ready to raise " +
                        "a stone.",
                        new AcceptableValueRange<float>(5f, 600f)));

                FireRelicPeakThreshold = advanced.Bind(RelicSection, "FireRelicPeakThreshold", 0.5f,
                    new ConfigDescription(
                        "How badly a zone must have scorched before healing it fully afterwards raises a " +
                        "blessed stone there.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                PlagueRelicPeakThreshold = advanced.Bind(RelicSection, "PlagueRelicPeakThreshold", 0.5f,
                    new ConfigDescription(
                        "How bad a zone's plague must have gotten before curing it fully afterwards raises a " +
                        "blessed stone there.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                RelicBlessedRecoveryMult = advanced.Bind(RelicSection, "RelicBlessedRecoveryMult", 1.25f,
                    new ConfigDescription(
                        "Multiplies how fast a damaged zone heals while blessed ground stands there. Above 1 " +
                        "speeds up recovery.",
                        new AcceptableValueRange<float>(1f, 3f)));

                RelicCursedRecoveryMult = advanced.Bind(RelicSection, "RelicCursedRecoveryMult", 0.8f,
                    new ConfigDescription(
                        "Multiplies how fast a damaged zone heals while cursed ground stands there. Below 1 " +
                        "slows recovery down.",
                        new AcceptableValueRange<float>(0.25f, 1f)));

                RelicBlessedExposureDrainMult = advanced.Bind(RelicSection, "RelicBlessedExposureDrainMult", 1.5f,
                    new ConfigDescription(
                        "How much faster plague sickness fades from a player standing on blessed ground. It " +
                        "only speeds up healing off the sickness, never shields against catching it in the " +
                        "first place.",
                        new AcceptableValueRange<float>(1f, 3f)));

                RelicCursedExposureAccrualMult = advanced.Bind(RelicSection, "RelicCursedExposureAccrualMult", 1.25f,
                    new ConfigDescription(
                        "How much faster plague sickness builds up on a player standing on cursed ground.",
                        new AcceptableValueRange<float>(1f, 3f)));

                RelicCursedStarBonus = advanced.Bind(RelicSection, "RelicCursedStarBonus", 0.25f,
                    new ConfigDescription(
                        "Extra chance for hostile spawns to come up starred while standing on cursed ground, " +
                        "stacking with the corruption bonus and any active spawn war. " +
                        "Read on each player's own game, so give every player the same value.",
                        new AcceptableValueRange<float>(0f, 2f)));

                RelicVandalHarm = advanced.Bind(RelicSection, "RelicVandalHarm", 0.5f,
                    new ConfigDescription(
                        "How much blame is booked against a player who destroys a relic stone. Has no effect " +
                        "unless rivalry is also turned on.",
                        new AcceptableValueRange<float>(0f, 5f)));

                RelicPrefabCandidates = advanced.Bind(RelicSection, "RelicPrefabCandidates", "highstone,widestone",
                    "Comma-separated names of existing game objects to try, in order, for the relic stone's " +
                    "shape — the first one the game recognises is used. Keep this the same on every " +
                    "player's copy of the config for a consistent result.");

                // ==== 13 - Titles =====================================================================

                EnableTitle = cfg.Bind(TitlesSection, "EnableTitle", true,
                    "Turns earned titles on or off — names shown under a player's nameplate for what they " +
                    "have done in the world, such as getting caught in a storm or walking into a badly " +
                    "plagued zone.");

                AnnounceTitles = cfg.Bind(TitlesSection, "AnnounceTitles", true,
                    "Announces a newly earned title to everyone on the server. Titles are rare by design, " +
                    "so this should not spam chat.");

                // ---- 13 - Titles, advanced file ----

                TitleIntervalSeconds = advanced.Bind(TitlesSection, "TitleIntervalSeconds", 10f,
                    new ConfigDescription(
                        "Seconds between checks of online players for newly earned titles.",
                        new AcceptableValueRange<float>(2f, 120f)));

                WinterbornSeconds = advanced.Bind(TitlesSection, "WinterbornSeconds", 1800f,
                    new ConfigDescription(
                        "Seconds a player must be online during Winter to earn the Winterborn title. Restarting " +
                        "the server resets everyone's progress toward it.",
                        new AcceptableValueRange<float>(60f, 86400f)));

                // ==== 14 - World state ================================================================

                EnableWorldState = cfg.Bind(WorldSection, "EnableWorldState", true,
                    "Turns the world's overall condition on or off — whether the land as a whole is judged " +
                    "Flourishing, Ailing or Stricken, announced when it changes.");

                // ---- 14 - World state, advanced file ----

                WorldStateIntervalSeconds = advanced.Bind(WorldSection, "WorldStateIntervalSeconds", 30f,
                    new ConfigDescription(
                        "Seconds between recalculations of the world's overall condition (Flourishing, Ailing " +
                        "or Stricken).",
                        new AcceptableValueRange<float>(10f, 600f)));

                WorldFlourishingBurden = advanced.Bind(WorldSection, "WorldFlourishingBurden", 0.25f,
                    new ConfigDescription(
                        "Total burden — a combined score of plague, corruption, scorch, soil tiredness and " +
                        "frost across the world — at or below which the land counts as Flourishing.",
                        new AcceptableValueRange<float>(0f, 10f)));

                WorldAilingBurden = advanced.Bind(WorldSection, "WorldAilingBurden", 4f,
                    new ConfigDescription(
                        "Total burden at which the land turns Ailing. Keep this above the Flourishing " +
                        "threshold, since burden is the same combined score described there.",
                        new AcceptableValueRange<float>(0.5f, 100f)));

                WorldStrickenBurden = advanced.Bind(WorldSection, "WorldStrickenBurden", 12f,
                    new ConfigDescription(
                        "Total burden at which the land is judged Stricken, the worst condition. Keep it " +
                        "comfortably above the Ailing threshold.",
                        new AcceptableValueRange<float>(1f, 500f)));

                WorldStormBurden = advanced.Bind(WorldSection, "WorldStormBurden", 1f,
                    new ConfigDescription(
                        "Extra burden added to the total while a Devastating Storm is active, so a stormy " +
                        "moment can nudge the land's judged condition worse.",
                        new AcceptableValueRange<float>(0f, 20f)));

                // ==== 15 - Visuals ====================================================================

                EnableZoneSync = cfg.Bind(VisualsSection, "EnableZoneSync", true,
                    "Turns zone-state syncing on or off. It feeds the plague fog, frost breath and " +
                    "scorch-ash visuals; a connecting player loses all three when it's off, though a player " +
                    "hosting their own game keeps seeing them regardless.");

                PlagueFogEnabled = cfg.Bind(VisualsSection, "PlagueFogEnabled", true,
                    "Client-side: shows a low, grey-green mist over plagued ground on your own screen. " +
                    "Every player chooses this for themselves; purely visual.");

                FrostBreathEnabled = cfg.Bind(VisualsSection, "FrostBreathEnabled", true,
                    "Client-side: fogs your character's breath on land whose cold has built up, as an early " +
                    "warning before the chill effect itself sets in.");

                ScorchAshEnabled = cfg.Bind(VisualsSection, "ScorchAshEnabled", true,
                    "Client-side: drifts grey ash over badly burned ground on your own screen, thinning as " +
                    "the land heals. Separate from FireFront's own flames and scorched-ground marks, which " +
                    "are unaffected by this setting.");

                RelicRunesEnabled = cfg.Bind(VisualsSection, "RelicRunesEnabled", true,
                    "Client-side: shows rune glyphs rising around standing relic stones on your own screen " +
                    "— gold on blessed ground, red on cursed.");

                // ---- 15 - Visuals, advanced file ----

                ZoneSyncIntervalSeconds = advanced.Bind(VisualsSection, "ZoneSyncIntervalSeconds", 10f,
                    new ConfigDescription(
                        "Seconds between zone-state updates sent to each connected player. Lowering it makes " +
                        "the plague fog, frost breath and ash visuals catch up to real changes sooner, at the " +
                        "cost of more frequent small network pushes.",
                        new AcceptableValueRange<float>(2f, 120f)));

                ZoneSyncRadiusZones = advanced.Bind(VisualsSection, "ZoneSyncRadiusZones", 2,
                    new ConfigDescription(
                        "How many zones out from each player's position are sent to them each push. A larger " +
                        "radius covers a bigger area around the player but sends more data per push.",
                        new AcceptableValueRange<int>(1, 4)));

                PlagueFogDensity = advanced.Bind(VisualsSection, "PlagueFogDensity", 1f,
                    new ConfigDescription(
                        "Client-side: how thick the plague mist looks on your screen. Lightly plagued ground " +
                        "never shows fog regardless of this setting, so a fresh outbreak stays hidden until it " +
                        "has properly taken hold.",
                        new AcceptableValueRange<float>(0f, 4f)));

                FrostBreathFloor = advanced.Bind(VisualsSection, "FrostBreathFloor", 0.3f,
                    new ConfigDescription(
                        "Client-side: how much zone frost is needed before your breath starts to fog. Kept " +
                        "below the chill effect's own threshold so you see the warning before the cold actually " +
                        "bites.",
                        new AcceptableValueRange<float>(0.05f, 1f)));

                ScorchAshDensity = advanced.Bind(VisualsSection, "ScorchAshDensity", 1f,
                    new ConfigDescription(
                        "Client-side: how much ash drifts over burned ground on your screen. Lightly scorched " +
                        "ground never shows ash regardless of this setting.",
                        new AcceptableValueRange<float>(0f, 4f)));

                // Bound LAST, with every other key already in place, so the migration below can reach
                // any of them.
                //
                // This comment used to claim the section sorts to the TOP of the written file because
                // "a digit is not a letter". That is backwards, and was corrected on 2026-09-18 by
                // reading ConfigFile.Save: it groups by section and orders by the section NAME, so
                // "Meta" sorts BELOW "1 - Core" and lands at the bottom. Purely cosmetic, and left
                // alone on purpose - renaming the section now would orphan the stamp in every file
                // already written, so every one of them would re-migrate and leave a dead [Meta] line
                // behind. (Undertow, which had not shipped one yet, uses "0 - Meta" instead.)
                //
                // NO AcceptableValueRange, removed 2026-09-18. BepInEx CLAMPS an out-of-range value
                // silently, so a ceiling here would one day quietly refuse the stamp and turn this into
                // a migration that re-applies on every single boot. A stamp is not a dial.
                ConfigVersion = cfg.Bind(ConfigLedger.MetaSection, ConfigLedger.VersionKey, 0,
                    "Tracks which layout version this mod's config files have already been updated to. Set " +
                    "automatically by the mod — do not edit it by hand, or a settings update may be " +
                    "reapplied or skipped incorrectly.");
            }
            catch
            {
                // A bind that throws stops the mod loading, as it always did. Nothing has been written:
                // both files keep exactly what they held before this boot.
                ConfigMigration.Abandon(cfg, advanced);
                throw;
            }

            // AFTER every bind: apply what Begin planned against the pre-bind snapshot, save the advanced
            // file, drop the old lines, stamp the version, save the main file. A fresh install has
            // nothing to apply or drop, but both files are still written and stamped here.
            ConfigMigration.Finish(cfg, advanced, ConfigVersion);
        }
    }
}
