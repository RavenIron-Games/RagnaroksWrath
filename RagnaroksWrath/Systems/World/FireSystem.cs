using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;

namespace RavenIron.RagnaroksWrath.Systems.World
{
    /// <summary>
    /// The world's memory of fire — a BRIDGE, not a fire simulation.
    ///
    /// FireFront (com.raveniron.firefront) already owns fire: ignition from vanilla fire
    /// damage, spread, ground cells, wind bias, extinguishing, VFX, client sync — shipped and
    /// verified on a dedicated server. Building a second spread simulation here would put two
    /// Raven Iron mods igniting and destroying the same pieces: the same
    /// two-mods-forcing-one-thing conflict rule 4 exists to prevent, in-house. Decided
    /// 2026-08-25 (see the locked-decisions table).
    ///
    /// So this system asks FireFront where fires burn and raises `ZoneState.Scorch` there.
    /// Scorch then suppresses fertility through BiomeDrift and will feed future fire risk.
    /// Fire acts; the land remembers. Without FireFront installed the system is DORMANT by
    /// design — Scorch stays a substrate other events can raise.
    ///
    /// Since 0.23.0 the bridge also carries ONE write: storm lightning. A Devastating Storm
    /// over a player under a dry sky may land a bolt nearby, and the bolt is a single call
    /// into FireFront's own ignition (`IgniteGroundNear`) — RW decides when and where,
    /// FireFront owns every consequence, so the "never a second fire sim" decision holds.
    /// Lightning never fires in rain (FireFront's own suppression rule, honoured before a bolt is
    /// announced, and since 0.28.0 judged where the bolt would land by FireFront's own reading of
    /// the weather),
    /// never lands within the configured standoff of anything player-built, and only ever
    /// strikes near an ONLINE player — which is also what keeps the AwayFromHome promise:
    /// an unattended base cannot be reached by a bolt that only exists where players are.
    ///
    /// NO ZONE CLOCK. Per docs/zone-clock-ownership.md, this system uses live tick time only:
    /// scorch accrues while a fire actually burns, which is also what the unattended-bases rule
    /// requires — FireFront's fires are the only input, and they exist only where its own
    /// simulation is running.
    ///
    /// FireFront is reached by REFLECTION, resolved once and cached, so it stays a soft
    /// dependency AT LOAD TIME: neither mod fails to load without the other, and this csproj
    /// gains no reference that fetch-libs cannot supply. The contract is
    /// `FireManager.CollectActiveFirePositions(List&lt;Vector3&gt;)`, public in FireFront since
    /// 0.17.2 and documented there as load-bearing for this mod.
    ///
    /// Since 0.23.0 FireFront is ALSO a listed manifest dependency (owner's call
    /// 2026-08-27, reversing the earlier soft-by-design packaging): mod managers install the
    /// pair together, so a missing FireFront now most likely means a hand install skipped it —
    /// which is why absence warns instead of whispering. Initialise tattles the exact version
    /// against the API floors so a stale pairing is diagnosed at boot, not discovered as
    /// silence.
    /// </summary>
    public class FireSystem : IWorldSystem
    {
        public const string FireFrontGuid = "com.raveniron.firefront";

        // The API floors the bridge cares about: positions (scorch) landed in 0.17.2,
        // the igniter surface (arson attribution) in 0.17.3, and per-fire igniters in 1.0.2.
        // Compared against BepInEx's parsed plugin metadata at boot so a stale pairing names
        // itself before the per-tick resolver's warnings become the only clue. Fully qualified
        // because Valheim ships its own global-namespace `Version` class, which shadows
        // System.Version in every file that references game types.
        private static readonly System.Version MinimumFireFrontVersion = new System.Version(0, 17, 2);
        private static readonly System.Version IgniterFireFrontVersion = new System.Version(0, 17, 3);
        private static readonly System.Version PerFireIgniterFireFrontVersion = new System.Version(1, 0, 2);

        // Valheim 1.0.7 broke FireFront below 0.20.0 outright: it calls the deleted
        // World.GetWorldSavePath, so its fire store cannot resolve a path. Worth its own line
        // because the symptom lands on OUR side of the bridge — fires that never persist look
        // like a scorch bug here, not a stale dependency there.
        private static readonly System.Version GameOneZeroFireFrontVersion = new System.Version(0, 20, 0);

        // FireFront's rain read (`ValheimBridge.IsRainingAt`) landed in 0.20.3. Lightning under
        // the world's own sky asks it before every bolt and withholds the bolt without it.
        private static readonly System.Version RainFireFrontVersion = new System.Version(0, 20, 3);

        public string Name => "FireSystem";
        public bool Enabled => ModConfig.EnableFire.Value;
        public float IntervalSeconds => ModConfig.FireScorchIntervalSeconds.Value;

        private bool _fireFrontPresent;

        // Rule 5 shape: resolved once, retried until they succeed, never latched as failed.
        private PropertyInfo _instanceProperty;
        private MethodInfo _collectMethod;

        // The OPTIONAL igniter surface (FireFront 0.17.3+): unlike the load-bearing
        // position method, an older FireFront is a legitimate configuration, so absence
        // logs once and goes quiet instead of warning per tick. Arson attribution simply
        // stays dormant until the surface exists.
        private PropertyInfo _igniterProperty;
        private bool _igniterAbsenceLogged;

        // Reused per tick so a steady state allocates nothing.
        private readonly List<Vector3> _firePositions = new List<Vector3>(64);
        private readonly List<ZoneKey> _burningZones = new List<ZoneKey>(16);

        // Arson attribution (review 2026-09-24). FireFront 1.0.2+ says who lit EACH fire
        // (`CollectActiveFiresWithIgniters`, same order as CollectActiveFirePositions, 0 = natural
        // or unknown); when it does, blame is per fire (FireBlame). Older FireFront has only one
        // global igniter, and then ArsonFootprint keeps the blame to the zones that fire reached.
        private MethodInfo _collectWithIgnitersMethod;
        private bool _collectWithIgnitersResolved;
        private System.Version _fireFrontVersion;    // from Initialise; null until FireFront is found
        private bool _perFire;                     // this tick's collect carried per-fire igniters
        private readonly List<long> _fireIgniters = new List<long>(64);
        private readonly object[] _collectWithIgnitersArgs = new object[2];
        private readonly List<KeyValuePair<ZoneKey, long>> _blame = new List<KeyValuePair<ZoneKey, long>>(16);
        private bool _blameMismatchLogged;
        private readonly ArsonFootprint _arson = new ArsonFootprint();
        private readonly List<ZoneKey> _arsonZones = new List<ZoneKey>(16);
        private readonly object[] _collectArgs = new object[1];

        // Storm lightning (0.23.0). The ignite surface follows the igniter's
        // optional-surface rules: absence logs once, lightning stays dormant, scorch
        // is untouched. Scratch lists reused because the roll runs every tick even
        // though a strike is rare.
        private MethodInfo _igniteMethod;
        private bool _igniteGroundAbsenceLogged;
        private readonly System.Random _rng = new System.Random();
        private readonly List<ZDO> _stormPlayers = new List<ZDO>(8);
        private readonly List<ZDO> _sectorScratch = new List<ZDO>(256);

        // The rain read (0.28.0): FireFront's static `ValheimBridge.IsRainingAt(Vector3)`,
        // asked only when no storm sky is forced. Optional-surface rules again, with one
        // difference: without it an unforced storm cannot check a bolt for rain, so it drops
        // every bolt, and the absence is a warning rather than a whisper.
        private MethodInfo _rainMethod;
        private bool _rainAbsenceLogged;
        private readonly object[] _rainArgs = new object[1];

        public void Initialise()
        {
            _fireFrontPresent = Chainloader.PluginInfos.ContainsKey(FireFrontGuid);

            if (!_fireFrontPresent)
            {
                // A warning, not info, since 0.23.0: FireFront is a listed manifest
                // dependency, so absence usually means a hand install missed half the pair.
                // Running without it stays safe and supported — the world just never scars.
                RagnaroksWrath.Log.LogWarning(
                    $"[{Name}] FireFront not present — dormant. It ships as a dependency of this " +
                    "mod; a manual install likely skipped it. Scorch will not accrue from fire; " +
                    "install FireFront (com.raveniron.firefront) 0.17.2+ to light the world's memory.");
                return;
            }

            System.Version version = Chainloader.PluginInfos[FireFrontGuid].Metadata.Version;
            _fireFrontVersion = version;
            RagnaroksWrath.Log.LogInfo(
                $"[{Name}] FireFront {version} detected — bridging. Burning zones gain " +
                $"{ModConfig.FireScorchPerMinute.Value:F3} scorch/min.");

            if (version < MinimumFireFrontVersion)
                RagnaroksWrath.Log.LogWarning(
                    $"[{Name}] FireFront {version} predates {MinimumFireFrontVersion} — its read API " +
                    "is missing, so scorch CANNOT accrue. Update FireFront; until then every bridge " +
                    "tick will name the unresolved surface.");
            else if (version < IgniterFireFrontVersion)
                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] FireFront {version} predates {IgniterFireFrontVersion} — arson " +
                    "attribution stays dormant; scorch is unaffected.");
            else if (version < PerFireIgniterFireFrontVersion)
                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] FireFront {version} predates {PerFireIgniterFireFrontVersion} — it names " +
                    "one igniter for the whole map, so arson is blamed on that player only where their " +
                    "fire has spread. Update FireFront to blame each fire on whoever lit it.");

            if (version < GameOneZeroFireFrontVersion)
                RagnaroksWrath.Log.LogWarning(
                    $"[{Name}] FireFront {version} predates {GameOneZeroFireFrontVersion}, which is the " +
                    "first build ported to Valheim 1.0.7. On 1.0.7 it cannot resolve its own save path, " +
                    "so fires do not survive a restart and anything downstream of them here will look " +
                    "wrong for the wrong reason. Update FireFront.");

            if (ModConfig.StormLightningEnabled.Value)
            {
                bool forcedSky = ModConfig.StormsForceWeather.Value;
                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] storm lightning armed — ~1 bolt per " +
                    $"{ModConfig.LightningMeanMinutes.Value:0.#} storm-minutes, landing " +
                    $"{ModConfig.LightningRingMinMeters.Value:0}-{ModConfig.LightningRingMaxMeters.Value:0}m " +
                    $"from a present player, {ModConfig.LightningStandoffMeters.Value:0}m homestead standoff; " +
                    (forcedSky
                        ? "rain judged by each storm's rolled look, since the sky is forced."
                        : "rain judged where each bolt would land, by FireFront's reading of the world's weather."));

                if (!forcedSky && version < RainFireFrontVersion)
                    RagnaroksWrath.Log.LogWarning(
                        $"[{Name}] FireFront {version} predates {RainFireFrontVersion}, which added the " +
                        "rain read lightning needs when no storm sky is forced. Without it no bolt can be " +
                        "checked for rain, so none will fall. Update FireFront.");
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!_fireFrontPresent) return;
            if (!Persistence.IsLoaded) return;   // not the authority, or world not up yet

            if (!TryCollectFirePositions()) return;

            // Lightning rides the RESOLVED bridge and must run before the no-fires
            // early-out: starting a fire from nothing is its entire purpose.
            TryLightning();

            if (_firePositions.Count == 0)
            {
                _arson.Clear();   // every fire is out, so FireFront has let its igniter go too
                return;
            }

            _burningZones.Clear();
            FireScorch.CollectBurningZones(_firePositions, _burningZones);

            float delta = FireScorch.ScorchDelta(ModConfig.FireScorchPerMinute.Value, deltaSeconds);
            if (delta <= 0f) return;

            // Task 13's arson writer: each fire's culprit booked the same scorch this tick burns
            // into their zones. 0 means natural fire, attributed to nobody.
            //  - FireFront 1.0.2+: per fire. Each burning zone books every distinct igniter
            //    whose fire is in it (FireBlame).
            //  - Older FireFront: one global igniter for the whole map, so only the zones that
            //    igniter's own fire has reached by contact are billed (ArsonFootprint).
            float harmPerPoint = ModConfig.ArsonHarmPerScorchPoint.Value;
            bool harmWanted = harmPerPoint > 0f && ModConfig.EnableRivalry.Value && RivalryLedger.IsLoaded;
            long igniter = 0;
            bool bookHarm = false;
            if (_perFire)
            {
                _arson.Clear();
                if (!FireBlame.Collect(_firePositions, _fireIgniters, _blame) && !_blameMismatchLogged)
                {
                    _blameMismatchLogged = true;
                    RagnaroksWrath.Log.LogWarning(
                        $"[{Name}] FireFront returned {_firePositions.Count} fire(s) but " +
                        $"{_fireIgniters.Count} igniter(s) - no arson booked while they disagree.");
                }
                if (harmWanted)
                    for (int i = 0; i < _blame.Count; i++)
                        RivalryLedger.AddHarm(_blame[i].Key, _blame[i].Value, delta * harmPerPoint);
            }
            else
            {
                igniter = TryReadIgniter();
                _arson.Observe(igniter, _burningZones, _arsonZones);
                bookHarm = igniter != 0 && _arsonZones.Count > 0 && harmWanted;
            }

            for (int i = 0; i < _burningZones.Count; i++)
            {
                ZoneKey zone = _burningZones[i];
                ZoneState state = Persistence.Get(zone);
                state.Scorch += delta;

                // Set clamps to 0..1 and enforces sparseness; scorch recovery is BiomeDrift's
                // job, on the zone clock. This system only ever adds.
                Persistence.Set(zone, state);

                if (bookHarm && _arsonZones.Contains(zone))
                    RivalryLedger.AddHarm(zone, igniter, delta * harmPerPoint);
            }

            if (ModConfig.VerboseLogging.Value)
                RagnaroksWrath.Log.LogInfo(
                    $"[{Name}] {_firePositions.Count} fire(s) scorching {_burningZones.Count} zone(s).");
        }

        /// <summary>
        /// One lightning roll per pass while a Devastating Storm runs. Every gate is a real
        /// rule: config, storm, the dice, a player actually under the storm, a dry sky where
        /// the bolt would land (FireFront's own rain suppression honoured before the
        /// bolt is announced — announcing a fire that fizzles in seconds reads as a bug),
        /// the homestead standoff. A blocked or lost bolt is never rerolled — the configured rate stays
        /// honest. The announcement says lightning STRUCK,
        /// not that fire caught: a bolt into rock or sand igniting nothing is honest
        /// weather, and FireFront's cell checks own that verdict.
        /// </summary>
        private void TryLightning()
        {
            if (!ModConfig.StormLightningEnabled.Value) return;
            if (!WeatherSystem.StormActive) return;

            float chance = LightningStrike.ChancePerTick(
                IntervalSeconds, ModConfig.LightningMeanMinutes.Value);
            if (_rng.NextDouble() > chance) return;

            // Older FireFront only: its igniter is one global captured ONCE, so a bolt fire
            // would be billed to whoever lit the fire already burning. While an attributed fire
            // burns, the sky holds its peace. With per-fire igniters (1.0.2+) a bolt's fire is
            // igniter 0 by construction and blames nobody, so no hold is needed.
            if (!_perFire && TryReadIgniter() != 0) return;

            ZNet znet = ZNet.instance;
            if (znet == null) return;

            List<ZDO> characters;
            try { characters = znet.GetAllCharacterZDOS(); }
            catch { return; }
            if (characters == null) return;

            _stormPlayers.Clear();
            for (int i = 0; i < characters.Count; i++)
            {
                ZDO c = characters[i];
                if (c == null || !c.IsValid()) continue;
                if (c.GetLong(ZDOVars.s_playerID, 0L) == 0) continue;   // real players, never an AFH keeper
                if (!WeatherSystem.IsStormAt(c.GetPosition())) continue;
                _stormPlayers.Add(c);
            }
            if (_stormPlayers.Count == 0) return;   // a storm with nobody under it strikes nobody

            Vector3 anchor = _stormPlayers[_rng.Next(_stormPlayers.Count)].GetPosition();
            Vector3 strike = LightningStrike.StrikePoint(
                anchor, _rng.NextDouble(), _rng.NextDouble(),
                ModConfig.LightningRingMinMeters.Value, ModConfig.LightningRingMaxMeters.Value);

            // The sky, judged where the bolt would land. Forced: the storm's rolled look. Not
            // forced: the world's own weather at this spot as FireFront reads it — never
            // EnvMan.IsWet(), which a dedicated server never updates (see
            // LightningStrike.SkyAllows). Asked here rather than before the dice because
            // unforced weather differs by place and only now is there a place to ask about.
            // That costs the rate nothing: a bolt refused here is lost, as one refused earlier
            // was.
            bool forcedSky = ModConfig.StormsForceWeather.Value;
            bool? raining = forcedSky ? (bool?)null : TryReadRainAt(strike);
            if (!LightningStrike.SkyAllows(forcedSky, WeatherSystem.StormIsDry, raining))
            {
                if (ModConfig.VerboseLogging.Value)
                    RagnaroksWrath.Log.LogInfo(
                        $"[{Name}] bolt withheld at ({strike.x:F0}, {strike.z:F0}) — " +
                        (forcedSky ? "the storm's forced sky is wet."
                         : raining == true ? "FireFront reads rain there."
                         : "FireFront cannot say whether it rains there."));
                return;
            }

            if (IsNearPlayerBuilt(strike, ModConfig.LightningStandoffMeters.Value))
            {
                if (ModConfig.VerboseLogging.Value)
                    RagnaroksWrath.Log.LogInfo(
                        $"[{Name}] bolt grounded by the homestead in {ZoneKey.FromWorldPos(strike)} — lost.");
                return;
            }

            if (!TryIgniteGround(strike, ModConfig.LightningIgniteRadiusMeters.Value)) return;

            Feedback.MessageFeed.ToPlayersNear(strike, 64f, "Lightning splits the sky!",
                Feedback.MessageFeed.Placement.Centre);
            RagnaroksWrath.Log.LogInfo(
                $"[{Name}] lightning strike at ({strike.x:F0}, {strike.z:F0}) in " +
                $"{ZoneKey.FromWorldPos(strike)} — the fire, if any, is FireFront's now.");
        }

        /// <summary>
        /// The bolt standoff, now shared machinery: Homestead owns the player-built scan
        /// (it grew a second caller in 0.26.0 — the storm scheduler anchors storms on
        /// players in the wild with the same question). Lightning fails CLOSED: when the
        /// world cannot be checked, the bolt is lost, never risked.
        /// </summary>
        private bool IsNearPlayerBuilt(Vector3 strike, float standoff)
            => Homestead.IsNearPlayerBuilt(strike, standoff, _sectorScratch, resultWhenUncheckable: true);

        /// <summary>
        /// FireFront's `IgniteGroundNear(Vector3, float)` — the bridge's one WRITE,
        /// promoted to a documented cross-mod contract beside CollectActiveFirePositions.
        /// Optional-surface rules like the igniter: absence logs once and lightning stays
        /// dormant; scorch and everything else is unaffected. Only called on ticks that
        /// already resolved the manager type.
        /// </summary>
        private bool TryIgniteGround(Vector3 origin, float radius)
        {
            try
            {
                if (_igniteMethod == null)
                {
                    Type manager = _collectMethod?.DeclaringType;
                    if (manager == null) return false;

                    _igniteMethod = manager.GetMethod("IgniteGroundNear",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (_igniteMethod == null)
                    {
                        if (!_igniteGroundAbsenceLogged)
                        {
                            _igniteGroundAbsenceLogged = true;
                            RagnaroksWrath.Log.LogInfo(
                                $"[{Name}] FireFront has no IgniteGroundNear surface — " +
                                "storm lightning dormant; scorch unaffected.");
                        }
                        return false;
                    }
                }

                object instance = _instanceProperty?.GetValue(null);
                if (instance == null) return false;

                _igniteMethod.Invoke(instance, new object[] { origin, radius });
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[{Name}] lightning ignite failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Whether rain falls at this position, from FireFront's static
        /// `ValheimBridge.IsRainingAt(Vector3)` (0.20.3+), or null when that cannot be known,
        /// which withholds the bolt. FireFront replays vanilla's per-period, per-biome weather
        /// roll for the position, after the overrides vanilla applies first, because headless
        /// `EnvMan` never rolls at all; it puts its own fires out by the same answer, so a bolt
        /// this lets through is not one FireFront's rain would drown. It answers false, not
        /// unknown, when it cannot see the weather itself, so null here means only that the
        /// surface is missing or the call threw — see LightningStrike.SkyAllows. Absence logs
        /// once, like the other optional surfaces. Only called on ticks that already resolved the manager
        /// type, whose assembly is FireFront's.
        /// </summary>
        private bool? TryReadRainAt(Vector3 position)
        {
            try
            {
                if (_rainMethod == null)
                {
                    Type bridge = _collectMethod?.DeclaringType?.Assembly
                        .GetType("FireFront.Utils.ValheimBridge");
                    MethodInfo method = bridge?.GetMethod("IsRainingAt",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3) }, null);

                    if (method == null || method.ReturnType != typeof(bool))
                    {
                        if (!_rainAbsenceLogged)
                        {
                            _rainAbsenceLogged = true;
                            RagnaroksWrath.Log.LogWarning(
                                $"[{Name}] FireFront has no ValheimBridge.IsRainingAt surface (0.20.3+) — " +
                                "with no storm sky forced, no bolt can be checked for rain, so none will " +
                                "fall. Scorch is unaffected. Update FireFront.");
                        }
                        return null;
                    }
                    _rainMethod = method;
                }

                _rainArgs[0] = position;
                return (bool)_rainMethod.Invoke(null, _rainArgs);
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning(
                    $"[{Name}] could not read FireFront's weather, so the bolt is withheld: " +
                    (ex.InnerException ?? ex).Message);
                return null;
            }
        }

        /// <summary>
        /// The current fire event's igniter player id via FireFront's OPTIONAL
        /// `CurrentFireIgniterPlayerId` property (0.17.3+), or 0 when absent, unreadable,
        /// or genuinely nobody. Only called on ticks that already resolved the instance.
        /// </summary>
        private long TryReadIgniter()
        {
            try
            {
                if (_igniterProperty == null)
                {
                    System.Type manager = _collectMethod?.DeclaringType;
                    if (manager == null) return 0L;

                    _igniterProperty = manager.GetProperty("CurrentFireIgniterPlayerId",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (_igniterProperty == null)
                    {
                        if (!_igniterAbsenceLogged)
                        {
                            _igniterAbsenceLogged = true;
                            RagnaroksWrath.Log.LogInfo(
                                $"[{Name}] FireFront predates the igniter surface (0.17.3) — " +
                                "arson attribution dormant; scorch still accrues.");
                        }
                        return 0L;
                    }
                }

                object instance = _instanceProperty?.GetValue(null);
                if (instance == null) return 0L;

                return (long)_igniterProperty.GetValue(instance);
            }
            catch (Exception)
            {
                return 0L;   // attribution is optional; scorch must never depend on it
            }
        }

        /// <summary>
        /// Fill _firePositions from FireFront, resolving the reflection handles on first use.
        ///
        /// Failures are warnings, not latches: if FireFront's surface moved, every tick names
        /// what could not be found, because a bridge that goes silently dormant after an update
        /// is this codebase's least favourite failure mode.
        /// </summary>
        private bool TryCollectFirePositions()
        {
            try
            {
                if (_collectMethod == null)
                {
                    Type manager = Chainloader.PluginInfos[FireFrontGuid].Instance.GetType()
                        .Assembly.GetType("FireFront.Fire.FireManager");
                    if (manager == null)
                    {
                        RagnaroksWrath.Log.LogWarning(
                            $"[{Name}] FireFront.Fire.FireManager not found — FireFront's API moved.");
                        return false;
                    }

                    _instanceProperty = manager.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _collectMethod = manager.GetMethod("CollectActiveFirePositions",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (_instanceProperty == null || _collectMethod == null)
                    {
                        _collectMethod = null;   // keep retrying; do not half-resolve
                        RagnaroksWrath.Log.LogWarning(
                            $"[{Name}] FireManager.Instance or CollectActiveFirePositions not found — " +
                            "FireFront is present but older than 0.17.2. Scorch will not accrue.");
                        return false;
                    }
                }

                if (!_collectWithIgnitersResolved)
                {
                    // Optional (FireFront 1.0.2+). The type is fixed for the process, so one look
                    // settles it either way.
                    _collectWithIgnitersResolved = true;
                    _collectWithIgnitersMethod = _collectMethod.DeclaringType.GetMethod(
                        "CollectActiveFiresWithIgniters", BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(List<Vector3>), typeof(List<long>) }, null);
                    // Below 1.0.2 the boot line already named the fallback, so only the two cases
                    // it could not know are logged here: the per-fire API found, or a FireFront new
                    // enough to have it whose method has moved.
                    if (_collectWithIgnitersMethod != null)
                        RagnaroksWrath.Log.LogInfo(
                            $"[{Name}] FireFront names each fire's igniter - arson is blamed per fire.");
                    else if (_fireFrontVersion != null && _fireFrontVersion >= PerFireIgniterFireFrontVersion)
                        RagnaroksWrath.Log.LogWarning(
                            $"[{Name}] FireFront {_fireFrontVersion} has no CollectActiveFiresWithIgniters" +
                            "(List<Vector3>, List<long>) - FireFront's API moved. Arson falls back to its " +
                            "single igniter, blamed only where that fire has spread.");
                }

                object instance = _instanceProperty.GetValue(null);
                if (instance == null) return false;   // FireManager not awake yet; normal early on

                _firePositions.Clear();
                _fireIgniters.Clear();
                if (_collectWithIgnitersMethod != null)
                {
                    _collectWithIgnitersArgs[0] = _firePositions;
                    _collectWithIgnitersArgs[1] = _fireIgniters;
                    _collectWithIgnitersMethod.Invoke(instance, _collectWithIgnitersArgs);
                    _perFire = true;
                }
                else
                {
                    _collectArgs[0] = _firePositions;
                    _collectMethod.Invoke(instance, _collectArgs);
                    _perFire = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[{Name}] could not read FireFront fires: {ex.Message}");
                return false;
            }
        }
    }
}
