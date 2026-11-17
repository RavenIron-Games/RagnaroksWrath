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
    /// A bolt never starts a fire in rain (FireFront's own suppression rule, judged since 0.28.0
    /// where the bolt lands by FireFront's own reading of the weather; since 2026-11-17 the bolt
    /// itself still strikes, flashes and hurts in rain, as real lightning does, and only the fire
    /// is withheld),
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

        // FireFront's rain read (`ValheimBridge.IsRainingAt`) landed in 0.21.0. Lightning under
        // the world's own sky asks it before every bolt and withholds the bolt without it.
        private static readonly System.Version RainFireFrontVersion = new System.Version(0, 21, 0);

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
            _live = this;
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
                    $"each reaches {ModConfig.LightningAttractMeters.Value:0}m for the tallest tree or ridge, " +
                    (ModConfig.LightningHurtsPlayers.Value
                        ? $"hurts within {ModConfig.LightningHurtRadiusMeters.Value:0}m (up to {ModConfig.LightningDamage.Value:0} lightning); "
                        : "hurts nobody; ") +
                    (forcedSky
                        ? "fire only under the dry look, since the sky is forced."
                        : "fire only where FireFront reads no rain."));

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
        // The live instance, for `wrath storm strike`. Systems are rebuilt per world.
        private static FireSystem _live;
        private bool _forcing;

        /// <summary>
        /// `wrath storm strike`: one bolt now, skipping only the dice. Every other rule a natural bolt
        /// follows still applies (a storm, a player under it, the homestead standoff, rain stopping
        /// the fire), so what it shows is exactly what a storm does, just sooner.
        /// </summary>
        public static bool TryForceStrike(out string message)
        {
            FireSystem live = _live;
            if (live == null || !live._fireFrontPresent) { message = "lightning needs FireFront installed on the server."; return false; }
            if (!ModConfig.StormLightningEnabled.Value) { message = "storm lightning is off (StormLightningEnabled = false)."; return false; }
            if (!WeatherSystem.StormActive) { message = "no storm is raging; `wrath storm start` first."; return false; }
            live._forcing = true;
            try { live.TryLightning(); }
            finally { live._forcing = false; }
            message = live._lastStrikeNote ?? "the bolt found nobody under the storm to strike near.";
            return live._lastStrikeNote != null;
        }

        private string _lastStrikeNote;

        private void TryLightning()
        {
            _lastStrikeNote = null;
            if (!ModConfig.StormLightningEnabled.Value) return;
            if (!WeatherSystem.StormActive) return;

            float chance = LightningStrike.ChancePerTick(
                IntervalSeconds, ModConfig.LightningMeanMinutes.Value);
            if (!_forcing && _rng.NextDouble() > chance) return;

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
            Vector3 aim = LightningStrike.StrikePoint(
                anchor, _rng.NextDouble(), _rng.NextDouble(),
                ModConfig.LightningRingMinMeters.Value, ModConfig.LightningRingMaxMeters.Value);

            // REAL LIGHTNING TAKES THE TALLEST THING NEAR WHERE IT WAS GOING (2026-11-17): a tree, a
            // ridge. The server instantiates nothing, so it judges from what it does have: tree ZDOs
            // around the aim point, and the world generator's terrain height. See FindTarget.
            LightningStrike.Target target = FindTarget(aim, anchor.y, out bool tookTree);
            Vector3 strike = target.Ground;

            // The standoff is checked on the point the bolt TOOK, not the aim, because a bolt that
            // reached for a tree beside a house must still never land at the house.
            if (IsNearPlayerBuilt(strike, ModConfig.LightningStandoffMeters.Value))
            {
                _lastStrikeNote = $"the bolt was grounded by a homestead near ({strike.x:F0}, {strike.z:F0}) and lost; step farther from your builds.";
                if (ModConfig.VerboseLogging.Value || _forcing)
                    RagnaroksWrath.Log.LogInfo(
                        $"[{Name}] bolt grounded by the homestead in {ZoneKey.FromWorldPos(strike)} — lost.");
                return;
            }

            // The sky, judged where the bolt lands. Forced: the storm's rolled look. Not forced: the
            // world's own weather at this spot as FireFront reads it, never EnvMan.IsWet(), which a
            // dedicated server never updates (see LightningStrike.SkyAllows).
            //
            // Since 2026-11-17 RAIN STOPS THE FIRE, NOT THE BOLT. A thunderstorm that never threw
            // lightning in the rain was the least realistic thing about it; now a wet storm's bolts
            // strike, flash, thunder and hurt like a dry one's, and only the fire is withheld.
            // FireFront would put a fire out in rain within seconds anyway; asking first keeps the
            // log honest about why no fire caught.
            bool forcedSky = ModConfig.StormsForceWeather.Value;
            bool? raining = forcedSky ? (bool?)null : TryReadRainAt(strike);
            bool fireMayCatch = LightningStrike.SkyAllows(forcedSky, WeatherSystem.StormIsDry, raining);

            bool ignited = fireMayCatch && TryIgniteGround(strike, ModConfig.LightningIgniteRadiusMeters.Value);

            // The strike, on every player's game: the bolt, the flash, the thunder, and the damage to
            // whoever stood too close. The seed draws the same bolt for everyone.
            Net.LightningSync.Broadcast(strike, target.Top, _rng.Next());

            _lastStrikeNote = $"lightning struck {(tookTree ? "a tree" : "open ground")} at ({strike.x:F0}, {strike.z:F0})" +
                              (ignited ? ", and it may burn." : ", and started no fire.");
            RagnaroksWrath.Log.LogInfo(
                $"[{Name}] lightning struck {(tookTree ? "a tree" : "open ground")} at ({strike.x:F0}, {strike.z:F0}) in " +
                // Along the ground: aim carries the player's height and strike the tree's base, so a
                // 3D distance once read "42 m" for a bolt that moved 20 (Storm10, 2026-11-17).
                $"{ZoneKey.FromWorldPos(strike)}, {StormArea.DistanceXZ(aim, strike):F0} m from where it was aimed — " +
                (ignited ? "the fire, if any, is FireFront's now."
                 : !fireMayCatch ? (forcedSky ? "no fire: the storm's sky is wet." : raining == true ? "no fire: it rains there." : "no fire: FireFront cannot say whether it rains there.")
                 : "no fire: FireFront's ignite call did not take."));
        }

        // Trees are judged by their prefab: whether it carries vanilla's TreeBase, cached per hash.
        private readonly Dictionary<int, bool> _isTree = new Dictionary<int, bool>(32);
        private readonly List<ZDO> _treeScratch = new List<ZDO>(256);
        private readonly List<LightningStrike.Target> _targets = new List<LightningStrike.Target>(32);

        // A standing tree's crown above its base. Valheim's beeches, oaks, pines and firs run from
        // about 10 to 20 m at scale 1; 14 m is a fair middle, scaled by the tree's own size.
        private const float TreeHeightMeters = 14f;
        // How much lower a point may stand and still sometimes take the bolt (see ChooseTarget).
        // 3 m, so a tree standing 13 m over the land around it takes ~92% of the bolts aimed near it.
        private const float HeightPreferenceMeters = 3f;
        private const int TerrainSamples = 6;

        /// <summary>
        /// The point a bolt aimed at <paramref name="aim"/> actually takes: every standing tree within
        /// LightningAttractMeters, plus the terrain at the aim and at six points around it, each scored
        /// by how high its top stands, the highest strongly favoured (LightningStrike.ChooseTarget).
        ///
        /// Terrain heights come from WorldGenerator, which is the generated land and knows nothing of a
        /// player's digging or raising; on a server that is the only height there is (GetGroundHeight
        /// returns its own input headless, the CLAUDE.md trap). Water is a flat surface at sea level,
        /// so a bolt over water ends on the water. Any failure falls back to the aim, on the ground
        /// height the player's own position gives, which is what every bolt used before this.
        /// </summary>
        private LightningStrike.Target FindTarget(Vector3 aim, float fallbackY, out bool tookTree)
        {
            tookTree = false;
            var fallback = new LightningStrike.Target { Ground = new Vector3(aim.x, fallbackY, aim.z), Top = fallbackY };
            float reach = ModConfig.LightningAttractMeters.Value;
            if (reach <= 0f) return fallback;

            try
            {
                _targets.Clear();
                WorldGenerator world = WorldGenerator.instance;
                ZoneSystem zones = ZoneSystem.instance;
                float water = zones != null ? zones.m_waterLevel : 30f;

                if (world != null)
                {
                    for (int i = 0; i <= TerrainSamples; i++)
                    {
                        float x = aim.x, z = aim.z;
                        if (i > 0)
                        {
                            double a = (i - 1) * Math.PI * 2 / TerrainSamples + _rng.NextDouble() * 0.5;
                            float r = reach * (0.4f + (float)_rng.NextDouble() * 0.6f);
                            x += (float)Math.Cos(a) * r;
                            z += (float)Math.Sin(a) * r;
                        }
                        float y = Mathf.Max(world.GetHeight(x, z), water);
                        _targets.Add(new LightningStrike.Target { Ground = new Vector3(x, y, z), Top = y });
                    }
                }
                else _targets.Add(fallback);

                ZDOMan man = ZDOMan.instance;
                ZNetScene scene = ZNetScene.instance;
                if (man != null && scene != null)
                {
                    _treeScratch.Clear();
                    man.FindSectorObjects(ZoneKey.FromWorldPos(aim).ToVector2s(),
                                          new SimulationDistance(1, 0, classic: true), _treeScratch);
                    float sqr = reach * reach;
                    for (int i = 0; i < _treeScratch.Count; i++)
                    {
                        ZDO zdo = _treeScratch[i];
                        if (zdo == null || !zdo.IsValid()) continue;
                        Vector3 p = zdo.GetPosition();
                        float dx = p.x - aim.x, dz = p.z - aim.z;
                        if (dx * dx + dz * dz > sqr) continue;
                        if (!IsTreePrefab(scene, zdo.GetPrefab())) continue;

                        float scale = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.one).y;
                        float scalar = zdo.GetFloat(ZDOVars.s_scaleScalarHash, 1f);
                        float height = TreeHeightMeters * Mathf.Clamp(scale * scalar, 0.3f, 3f);
                        _targets.Add(new LightningStrike.Target { Ground = p, Top = p.y + height, IsTree = true });
                    }
                }

                int pick = LightningStrike.ChooseTarget(_targets, _rng.NextDouble(), HeightPreferenceMeters);
                if (pick < 0) return fallback;
                tookTree = _targets[pick].IsTree;
                return _targets[pick];
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[{Name}] could not look for tall targets, the bolt lands where it was aimed: {ex.Message}");
                return fallback;
            }
        }

        private bool IsTreePrefab(ZNetScene scene, int hash)
        {
            if (_isTree.TryGetValue(hash, out bool known)) return known;
            bool tree = false;
            try
            {
                GameObject prefab = scene.GetPrefab(hash);
                tree = prefab != null && prefab.GetComponent<TreeBase>() != null;
            }
            catch { tree = false; }
            _isTree[hash] = tree;
            return tree;
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
        /// `ValheimBridge.IsRainingAt(Vector3)` (0.21.0+), or null when that cannot be known,
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
                                $"[{Name}] FireFront has no ValheimBridge.IsRainingAt surface (0.21.0+) — " +
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
