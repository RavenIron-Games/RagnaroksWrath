using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Feedback;

namespace RavenIron.RagnaroksWrath.Systems.World
{
    /// <summary>
    /// Gameplay-only weather, and Devastating Storms.
    ///
    /// RULE 4 IN FULL. This system READS weather and never selects it. Seasonality (RustyMods,
    /// 558K downloads) owns environment selection, and two mods forcing it on one client is a
    /// straight conflict where whoever patches last silently wins. The only line in this mod that
    /// can ever set `m_forceEnvironment` sits behind `StormsForceWeather`, default false, and
    /// exists for owners who run no weather mod at all.
    ///
    /// STORMS ARE REAL VANILLA EVENTS. A `RandomEvent` is appended to `RandEventSystem.m_events`
    /// and triggered by name, so it inherits vanilla's banner, timer, music, pause-when-nobody-is-
    /// near, and its network replication — none of which we would get right by reimplementing.
    /// What we add is when it fires and what it means; what it looks like stays vanilla's.
    ///
    /// STORMS ARE POSITIONAL, AND SO ARE THEIR EFFECTS. A vanilla event has a position and a
    /// range, so a storm 5 km away must not change fire risk here. Every multiplier this system
    /// exposes takes a position and answers for that spot, using <see cref="StormArea"/> — the
    /// same containment test vanilla uses for the banner, so the two cannot drift apart.
    /// </summary>
    public class WeatherSystem : IWorldSystem
    {
        /// <summary>
        /// Prefixed because event names share one namespace with the base game and every other
        /// mod, exactly like routed-RPC names. A collision here would have us triggering someone
        /// else's event, or them triggering ours.
        /// </summary>
        /// <remarks>
        /// The names and the roll live in <see cref="StormLook"/>, in Core, so the harness can pin
        /// them — a storm whose name stops being recognised reads as no storm at all. These two
        /// stay here because callers outside this file have always used them.
        /// </remarks>
        public const string StormEventName = StormLook.WetEventName;

        /// <summary>
        /// The dry storm's event, added 2026-09-18. A storm now rolls one of two looks per
        /// episode and the look has teeth: the wet one soaks and only the dry one can burn.
        /// FireSystem gates on the ROLLED look — <see cref="StormIsDry"/> — and NOT on
        /// `EnvMan.IsWet()`, because a forced sky never reaches a dedicated server and the
        /// engine's weather there is not the storm's. See `LightningStrike.SkyAllows`.
        /// </summary>
        public const string StormDryEventName = StormLook.DryEventName;

        public string Name => "WeatherSystem";
        public bool Enabled => ModConfig.EnableWeather.Value;
        public float IntervalSeconds => ModConfig.WeatherIntervalSeconds.Value;

        // ---- read-only weather state -----------------------------------------------------

        /// <summary>Vanilla's current environment name, or "" before EnvMan exists. Read, never set.</summary>
        public static string CurrentEnvironment { get; private set; } = "";

        // ---- storm state -----------------------------------------------------------------

        public static bool StormActive { get; private set; }
        public static Vector3 StormCentre { get; private set; }
        public static float StormRange { get; private set; }

        /// <summary>
        /// Which look the live storm rolled. Derived from the ACTIVE EVENT'S NAME, never from the
        /// roll that started it, so it is correct on a pure client (which never rolls), correct
        /// after a server restart mid-storm (which re-reads vanilla's saved event), and correct
        /// for anyone who joins halfway through. Meaningless-but-harmless when
        /// StormsForceWeather is off: both events force nothing, so neither look is visible.
        /// </summary>
        public static bool StormIsDry { get; private set; }

        /// <summary>True for either storm event. Liveness must accept both names, or a dry storm reads as no storm.</summary>
        public static bool IsStormEvent(string eventName) => StormLook.IsStorm(eventName);

        private float _sinceStormEnded;
        private readonly System.Random _rng = new System.Random();

        // Wild-anchor machinery (0.26.0): scratch for Homestead's sector scan, the
        // filtered candidate list, and a latch so a held storm logs its hold once per
        // episode instead of every tick it stays overdue.
        private readonly List<ZDO> _sectorScratch = new List<ZDO>(256);
        private readonly List<ZDO> _wildCandidates = new List<ZDO>(8);
        private bool _holdLogged;

        /// <summary>
        /// RandEventSystem.m_randomEvent is PRIVATE, so liveness comes through a cached field
        /// accessor per rule 5 — publicized assemblies are compile-time only and Mono refuses the
        /// direct access at runtime. Everything else we need off the event (m_name, m_pos,
        /// m_eventRange) is genuinely public, so this is the only private member touched.
        /// </summary>
        private static AccessTools.FieldRef<RandEventSystem, RandomEvent> _activeEventRef;
        private static bool _activeEventResolved;

        public void Initialise()
        {
            RagnaroksWrath.Log.LogInfo(
                $"[{Name}] storms every {ModConfig.StormMinIntervalSeconds.Value:F0}-" +
                $"{ModConfig.StormMaxIntervalSeconds.Value:F0}s, duration " +
                $"{ModConfig.StormDurationSeconds.Value:F0}s, range {ModConfig.StormRangeMeters.Value:F0}m, " +
                $"forceWeather={ModConfig.StormsForceWeather.Value}.");

            // Info, not a warning, since 2026-09-03: the sky is set through vanilla's own event
            // override, which EnvMan consults BEFORE the biome weather list. Seasonality only
            // rewrites that list (decompiled, then seen by eye), so the two coexist. A weather mod
            // that patches the override path itself is a different story, hence the last clause.
            if (ModConfig.StormsForceWeather.Value)
                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] StormsForceWeather is ON: each storm rolls one of two skies through " +
                    $"vanilla's event override, which outranks the biome weather list - wet " +
                    $"'{ModConfig.StormForcedEnvironment.Value}' or dry " +
                    $"'{ModConfig.StormDryEnvironment.Value}', dry {ModConfig.StormDryChance.Value:P0} " +
                    "of the time. Rain suppresses lightning, so only the dry one can start fires. " +
                    "Coexists with Seasonality (verified 2026-09-03); a mod that patches the " +
                    "override path itself may still win.");
        }

        public void Tick(float deltaSeconds)
        {
            bool wasActive = StormActive;

            ReadWeather();
            RefreshStormState();

            // The sky is the evidence. Naming the environment at both ends of a storm is what
            // turns "we did not force weather" from a claim into something a log can settle:
            // with StormsForceWeather off these follow vanilla's own cycle, and a value that
            // snapped to ThunderStorm on start and back afterwards would be visible here.
            if (StormActive != wasActive)
            {
                // Multipliers reported at the storm's own centre, at both ends of its life. This
                // line USED to print fire risk and wind alongside plague spread, worded as though
                // all three drove gameplay, and it was cited as the in-game verification that
                // they did. They did not: as of 2026-09-18 only plague spread has a consumer, and
                // printing the other two next to it is how that went unnoticed for three weeks.
                // An instrument that reports a number nothing acts on is not a weak instrument,
                // it is a misleading one - so this now says which is which, and the day fire risk
                // or wind gains a real consumer, move it up into the live half of the line.
                Vector3 probe = StormCentre;
                float wind = WindMultiplierAt(probe);

                // Whose sky is this? On a dedicated server CurrentEnvironment is the SERVER'S
                // own weather, which a forced storm never overrides — reading it as "the
                // storm's sky" is what made 'Clear' look normal under a ThunderStorm all
                // through the 2026-09-18 session. Name the owner of the value, always.
                string skyText = ModConfig.StormsForceWeather.Value
                    ? $"clients see '{(StormIsDry ? ModConfig.StormDryEnvironment.Value : ModConfig.StormForcedEnvironment.Value)}'" +
                      $", this machine's own sky is '{CurrentEnvironment}' and is NOT the storm's"
                    : $"sky is '{CurrentEnvironment}' (no sky forced, so this IS the storm's)";

                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] storm {(StormActive ? "began" : "ended")} - {(StormIsDry ? "DRY" : "wet")} " +
                    $"look, {skyText} " +
                    $"(forceWeather={ModConfig.StormsForceWeather.Value}); at the centre: " +
                    $"plagueSpread x{PlagueSpreadMultiplierAt(probe):F2} (live). " +
                    $"Reserved, consumed by nothing yet: fireRisk x{FireRiskMultiplierAt(probe):F2}, " +
                    $"wind x{wind:F2} (vanilla {WindSystem.BaseIntensity:F2} -> gameplay " +
                    $"{WindState.Combine(WindSystem.BaseIntensity, wind):F2}).");

                if (!StormActive) MessageFeed.ToEveryone("The storm passes.");
            }

            // Only the authority schedules. A client running this would fire storms into its own
            // copy of the world and nowhere else - the same failure shape as a console command
            // executing where it was typed.
            if (!RagnaroksWrath.IsSimulationAuthority()) return;

            if (StormActive)
            {
                _sinceStormEnded = 0f;
                return;
            }

            // The clock only runs while somebody is online. Accruing on an empty server meant
            // the interval was long past its maximum by the time anyone connected, so the storm
            // fired the instant their character ZDO appeared — while they were still on the
            // loading screen, with no HUD to show the announcement to. A storm nobody can see is
            // worse than a storm slightly delayed.
            if (!TryPickStormCentre(out Vector3 centre)) return;

            _sinceStormEnded += deltaSeconds;
            if (_sinceStormEnded < ModConfig.StormMinIntervalSeconds.Value) return;

            // Between the min and max interval the chance ramps from 0 to 1, so a storm is
            // guaranteed by the max rather than merely likely. A flat per-tick roll has a long
            // tail: with bad luck a server could go a real day without one and it would look
            // broken rather than unlucky.
            float span = Math.Max(1f, ModConfig.StormMaxIntervalSeconds.Value - ModConfig.StormMinIntervalSeconds.Value);
            float progress = (_sinceStormEnded - ModConfig.StormMinIntervalSeconds.Value) / span;
            if (_rng.NextDouble() > progress) return;

            // The roll succeeded — the storm WANTS to fire. Only now pay for the wild
            // filter (0.26.0): a storm anchored on a player at their homestead announces
            // drama it structurally cannot deliver — lightning grounds out on the standoff
            // and cleared base ground gives fire nothing to eat — so the anchor must be a
            // player out in the wild. If everyone online is behind their own walls, the
            // overdue storm HOLDS with its accrual intact: the sky breaks the moment
            // somebody steps back out. Deliberately checked after the roll so the scan
            // costs nothing on the quiet ticks.
            if (ModConfig.StormAvoidBaseMeters.Value > 0f)
            {
                if (!TryPickWildCentre(out Vector3 wild))
                {
                    if (ModConfig.VerboseLogging.Value && !_holdLogged)
                    {
                        _holdLogged = true;
                        RagnaroksWrath.Log.LogInfo(
                            $"[{Name}] storm holds — every player is at a homestead; " +
                            "it breaks when someone steps into the wild.");
                    }
                    return;
                }
                centre = wild;
            }

            _holdLogged = false;
            StartStorm(centre);
        }

        // ---- weather, read-only ----------------------------------------------------------

        private static void ReadWeather()
        {
            EnvMan env = EnvMan.instance;
            if (env == null) return;

            try
            {
                EnvSetup current = env.GetCurrentEnvironment();
                CurrentEnvironment = current != null ? current.m_name : "";
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[WeatherSystem] could not read environment: {ex.Message}");
            }
        }

        // ---- storms ----------------------------------------------------------------------

        /// <summary>
        /// Append our storm to the event list. Called from a prefix on RandEventSystem.Awake,
        /// which only assigns m_instance and never touches m_events, so the serialized list is
        /// already populated by the time we see it.
        ///
        /// Idempotent: Awake runs once per world load, and appending a second copy would leave
        /// vanilla able to pick either.
        /// </summary>
        public static void RegisterStormEvent(RandEventSystem system)
        {
            if (system == null) return;

            // TWO events, one per storm look, and the reason is worth reading before touching
            // this. Vanilla syncs the active event BY NAME and nothing else: SendCurrentRandomEvent
            // invokes the "SetEvent" RPC with m_randomEvent.m_name, and the receiving client calls
            // SetRandomEventByName -> GetEvent(name), which walks THAT CLIENT'S OWN m_events list
            // and reads THAT CLIENT'S OWN m_forceEnvironment (all four bodies decompile-verified
            // 2026-09-18). So a server that rolls the dry storm cannot hand a client an
            // environment - but it can hand it a NAME, and the client resolves its own sky from
            // it. That is why a per-storm random look needs no new networking at all, and why
            // trying to sync the environment string instead would fight the engine.
            //
            // CONSEQUENCE: both events must be registered on EVERY machine, unconditionally, even
            // when StormsForceWeather is off and the two are identical in effect. GetEvent returns
            // null for a name it does not have, and SetRandomEvent(null) means that player simply
            // gets NO STORM while everyone else has one. A client on an older version that knows
            // only the original name sees exactly that if the server rolls dry - VersionSync
            // already warns on mismatch, which is the machinery for it.
            RegisterOne(system, StormEventName,    Wet: true);
            RegisterOne(system, StormDryEventName, Wet: false);
        }

        /// <param name="Wet">
        /// Which of the two looks this event carries. It selects a config key, nothing more: with
        /// StormsForceWeather off BOTH events force nothing and the distinction is invisible,
        /// which is the locked decision's default and stays that way.
        /// </param>
        private static void RegisterOne(RandEventSystem system, string name, bool Wet)
        {
            try
            {
                if (system.HaveEvent(name)) return;

                var storm = new RandomEvent
                {
                    m_name = name,
                    m_enabled = true,

                    // We schedule storms ourselves, so vanilla's roll must not also pick this.
                    // Two schedulers for one event would double the rate and neither would know.
                    m_random = false,

                    m_duration = ModConfig.StormDurationSeconds.Value,
                    m_eventRange = ModConfig.StormRangeMeters.Value,

                    // A storm is weather, not a raid: it should reach wilderness, not only bases.
                    m_nearBaseOnly = false,

                    // Vanilla's own "stop running where nobody is" behaviour, inherited rather
                    // than reimplemented.
                    m_pauseIfNoPlayerInArea = true,

                    m_biome = (Heightmap.Biome)(-1),   // every biome

                    // Plain text, not "$tokens": we register no localisation, and vanilla
                    // renders an unknown token as visible garbage in the event banner.
                    m_startMessage = "A devastating storm rages",
                    m_endMessage = "The storm passes",

                    // THE LOCKED DECISION, in one line. Empty means the event runs with the
                    // player's real sky: full vanilla event, no environment override, no fight
                    // with Seasonality. The config below is the only thing that may fill it.
                    m_forceEnvironment = ModConfig.StormsForceWeather.Value
                        ? (Wet ? ModConfig.StormForcedEnvironment.Value
                               : ModConfig.StormDryEnvironment.Value)
                        : "",
                };

                system.m_events.Add(storm);

                RagnaroksWrath.Log.LogInfo(
                    $"[WeatherSystem] registered event '{name}' ({(Wet ? "wet" : "dry")} look, " +
                    $"duration {storm.m_duration:F0}s, range {storm.m_eventRange:F0}m, " +
                    $"forceEnvironment='{storm.m_forceEnvironment}').");
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError($"[WeatherSystem] could not register storm event '{name}': {ex}");
            }
        }

        private void StartStorm(Vector3 centre)
        {
            RandEventSystem system = RandEventSystem.instance;
            if (system == null) return;

            // THE ROLL, and it happens exactly here: once per storm, on the authority, before the
            // event is named. Everything downstream - the clients' skies, our own liveness read,
            // whether lightning can strike - follows from the NAME this picks, because that name
            // is the only thing vanilla replicates. Rolling anywhere else (per tick, per client,
            // per query) would give two machines different storms.
            string eventName = StormLook.Roll(_rng.NextDouble(), ModConfig.StormDryChance.Value);
            bool dry = StormLook.IsDry(eventName);

            try
            {
                system.SetRandomEventByName(eventName, centre);
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogError($"[WeatherSystem] could not start storm: {ex.Message}");
                return;
            }

            _sinceStormEnded = 0f;

            // Vanilla's own banner reaches players inside the event area; this reaches everyone,
            // including those far from it, which is the point of a world event being felt
            // world-wide. Deliberately two messages to two audiences, not one duplicated.
            MessageFeed.ToEveryone("A devastating storm gathers.");

            // The look is named here because it is the difference between a storm that can start
            // fires and one that cannot, and because "no bolts fell" has two causes - a wet sky,
            // or lightning genuinely not rolling - that are indistinguishable without this line.
            // That exact ambiguity cost a round-trip on 2026-09-03.
            RagnaroksWrath.Log.LogInfo(
                ModConfig.StormsForceWeather.Value
                    ? $"[WeatherSystem] storm started at ({centre.x:F0}, {centre.z:F0}) - rolled " +
                      $"{(dry ? $"DRY ('{ModConfig.StormDryEnvironment.Value}'), so lightning may strike" : $"WET ('{ModConfig.StormForcedEnvironment.Value}'), so rain suppresses lightning")} " +
                      $"(StormDryChance {ModConfig.StormDryChance.Value:F2})."
                    : $"[WeatherSystem] storm started at ({centre.x:F0}, {centre.z:F0}) - rolled " +
                      $"{(dry ? "dry" : "wet")}, but StormsForceWeather is off so the sky is the " +
                      "world's own and the roll changes nothing visible. Lightning follows the " +
                      "real weather.");
        }

        /// <summary>
        /// Somewhere a player actually is. Character ZDOs rather than Player instances, for the
        /// same reason BiomeStateSystem uses them: a dedicated server instantiates nothing where
        /// players are. Vanilla's own IsAnyPlayerInEventArea reads the same list.
        /// </summary>
        private bool TryPickStormCentre(out Vector3 centre)
        {
            centre = Vector3.zero;

            ZNet znet = ZNet.instance;
            if (znet == null) return false;

            try
            {
                List<ZDO> characters = znet.GetAllCharacterZDOS();
                if (characters == null || characters.Count == 0) return false;

                var valid = new List<ZDO>(characters.Count);
                for (int i = 0; i < characters.Count; i++)
                {
                    ZDO zdo = characters[i];
                    if (zdo != null && zdo.IsValid()) valid.Add(zdo);
                }

                if (valid.Count == 0) return false;

                centre = valid[_rng.Next(valid.Count)].GetPosition();
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[WeatherSystem] could not pick a storm centre: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// A storm anchor out in the WILD: a random online player standing farther than
        /// `StormAvoidBaseMeters` from anything player-built. False when everyone online
        /// is at a homestead — the caller holds the storm rather than announcing one that
        /// cannot deliver. The scan fails OPEN inside Homestead (an uncheckable world
        /// counts as wild): a storm without lightning still carries wind, plague and the
        /// war, so a broken scan must dull the filter, never kill the weather.
        /// </summary>
        private bool TryPickWildCentre(out Vector3 centre)
        {
            centre = Vector3.zero;

            ZNet znet = ZNet.instance;
            if (znet == null) return false;

            try
            {
                List<ZDO> characters = znet.GetAllCharacterZDOS();
                if (characters == null || characters.Count == 0) return false;

                _wildCandidates.Clear();
                float radius = ModConfig.StormAvoidBaseMeters.Value;
                for (int i = 0; i < characters.Count; i++)
                {
                    ZDO zdo = characters[i];
                    if (zdo == null || !zdo.IsValid()) continue;
                    if (Homestead.IsNearPlayerBuilt(zdo.GetPosition(), radius, _sectorScratch,
                            resultWhenUncheckable: false)) continue;
                    _wildCandidates.Add(zdo);
                }

                if (_wildCandidates.Count == 0) return false;

                centre = _wildCandidates[_rng.Next(_wildCandidates.Count)].GetPosition();
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[WeatherSystem] wild-centre pick failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refresh our view of whether a storm is running, from vanilla's own active event rather
        /// than from a timer of our own. Vanilla may pause a storm when nobody is in its area or
        /// clear it outright, and a second timer would keep insisting the storm was live while
        /// the banner had gone.
        /// </summary>
        private static void RefreshStormState()
        {
            RandEventSystem system = RandEventSystem.instance;
            if (system == null)
            {
                StormActive = false;
                return;
            }

            if (!_activeEventResolved)
            {
                try
                {
                    _activeEventRef = AccessTools.FieldRefAccess<RandEventSystem, RandomEvent>("m_randomEvent");
                    _activeEventResolved = _activeEventRef != null;
                }
                catch (Exception ex)
                {
                    // Do not latch the failure: keep retrying, per rule 5. If it never resolves,
                    // this line names the member so a moved API is obvious rather than silent.
                    RagnaroksWrath.Log.LogWarning(
                        $"[WeatherSystem] could not resolve RandEventSystem.m_randomEvent: {ex.Message}");
                    StormActive = false;
                    return;
                }
            }

            try
            {
                RandomEvent active = _activeEventRef(system);

                if (active == null || !IsStormEvent(active.m_name))
                {
                    StormActive = false;
                    return;
                }

                StormActive = true;
                StormIsDry = StormLook.IsDry(active.m_name);
                StormCentre = active.m_pos;
                StormRange = active.m_eventRange;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[WeatherSystem] could not read the active event: {ex.Message}");
                StormActive = false;
            }
        }

        // ---- what a storm means, at a position -------------------------------------------

        /// <summary>True when <paramref name="position"/> is inside a running storm.</summary>
        public static bool IsStormAt(Vector3 position)
            => StormActive && StormArea.Contains(StormCentre, StormRange, position);

        /// <summary>Fire risk multiplier at a position. 1.0 outside a storm.</summary>
        public static float FireRiskMultiplierAt(Vector3 position)
            => IsStormAt(position) ? ModConfig.StormFireRiskMultiplier.Value : 1f;

        /// <summary>Wind multiplier at a position. 1.0 outside a storm.</summary>
        public static float WindMultiplierAt(Vector3 position)
            => IsStormAt(position) ? ModConfig.StormWindMultiplier.Value : 1f;

        /// <summary>Plague spread multiplier at a position. 1.0 outside a storm.</summary>
        public static float PlagueSpreadMultiplierAt(Vector3 position)
            => IsStormAt(position) ? ModConfig.StormPlagueSpreadMultiplier.Value : 1f;
    }
}
