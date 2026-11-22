using System;
using System.Collections.Generic;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Net;

namespace RavenIron.RagnaroksWrath.Visuals
{
    /// <summary>
    /// The nordic design on the stones: rune glyphs — fehu, algiz, gebo, thurisaz —
    /// rising slowly around every standing relic, gold where the ground is blessed, a
    /// dull red where it is cursed. Fourth emitter on the ParticleKit template, and the
    /// first anchored to PLACES rather than the walking player: one column per standing
    /// relic in the player's 3x3 neighbourhood, pruned when the stone falls or the
    /// player leaves. Procedural, no assets (the design is CODE — a carved texture would
    /// break both the no-assets rule and rule 4's material ban), rule 3 throughout, one
    /// throw latches it off.
    ///
    /// THE BEACON (added 2026-11-22, the owner: "they are almost impossible to find"): the
    /// runes alone were sized to be "invisible from the next zone", which made a stone
    /// something you could only find by already standing on it. Each standing relic in the
    /// 5x5 around the player now also raises a tall, slowly pulsing pillar of light in the
    /// stone's colour, rising well above the treeline, plus a soft point light at its foot
    /// that makes the stone glow at night. The pillar is a LineRenderer on ParticleKit's
    /// bolt material (bright core, soft edges), drawn only where this client has REAL
    /// terrain under the zone centre — GetGroundData leaves the height untouched on a
    /// raycast miss, so an unloaded zone is retried next second rather than drawn at sea
    /// level. Still no assets, no vanilla material touched.
    ///
    /// Visibility budget: 5 glyphs/s x 5s alive in a 2.4m-wide column = ~25 runes at
    /// ~0.4m each at the stone; the pillar is 1.8 -> 1.3m wide and 45m tall, bright to 80% of its
    /// height. Measured by the robot player (tools\autotest, phase R) on 2026-11-22: the first, thinner
    /// cut showed at night from 120 m but vanished against the daytime sky.
    /// </summary>
    public class RelicRunes : MonoBehaviour
    {
        private const float UpdateInterval = 1f;
        private const float EmissionRate = 5f;

        private const int RuneRadiusZones = 1;     // 3x3: the runes are for standing AT the stone
        private const int BeaconRadiusZones = 2;   // 5x5: the beacon is for FINDING it
        private const float BeaconHeight = 45f;
        // Wide and bright all the way up, fading only over the last fifth: from afar the TOP is the part that
        // clears the treeline, so a pillar that thins and fades upward hides exactly the part a searcher sees.
        // The first cut (1.1 -> 0.35 m, 0.55 alpha fading to 0) was invisible against a daytime sky at 120 m.
        private const float BeaconBaseWidth = 1.8f;
        private const float BeaconTopWidth = 1.3f;
        private const float BeaconAlpha = 0.85f;
        private const float BeaconFadeFrom = 0.8f;   // fraction of the height where the fade-out starts
        private const float GlowRange = 12f;
        private const float GlowIntensity = 1.6f;
        private const float PulseSeconds = 3.5f;

        private static readonly Color Blessed = new Color(0.79f, 0.71f, 0.35f, 0.85f); // the title gold
        private static readonly Color Cursed  = new Color(0.71f, 0.31f, 0.31f, 0.85f); // the nemesis red

        private readonly Dictionary<ZoneKey, ParticleSystem> _columns =
            new Dictionary<ZoneKey, ParticleSystem>(4);
        private readonly Dictionary<ZoneKey, Beacon> _beacons =
            new Dictionary<ZoneKey, Beacon>(4);
        private readonly List<ZoneKey> _dead = new List<ZoneKey>(4);

        private sealed class Beacon
        {
            public GameObject Root;
            public LineRenderer Pillar;
            public Light Glow;
            public Color Tint;
            public float Phase;
            public Gradient Gradient;
            public GradientColorKey[] ColorKeys;
            public GradientAlphaKey[] AlphaKeys;
        }

        private Material _material;
        private Material _beaconMaterial;
        private bool _buildFailed;
        private float _nextUpdate;

        private void Update()
        {
            Pulse();

            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + UpdateInterval;

            try
            {
                RelicSync.EnsureRegistered();

                Player player = Player.m_localPlayer;
                bool show = ModConfig.RelicRunesEnabled.Value && player != null && !_buildFailed;
                ZoneKey centre = show
                    ? ZoneKey.FromWorldPos(player.transform.position) : default;

                // Prune markers for fallen stones and left-behind ground.
                PruneColumns(show, centre);
                PruneBeacons(show, centre);

                if (!show) return;

                // Raise markers for standing relics around the player.
                for (int dx = -BeaconRadiusZones; dx <= BeaconRadiusZones; dx++)
                for (int dy = -BeaconRadiusZones; dy <= BeaconRadiusZones; dy++)
                {
                    var zone = new ZoneKey(centre.X + dx, centre.Y + dy);
                    bool wantRunes = Math.Abs(dx) <= RuneRadiusZones && Math.Abs(dy) <= RuneRadiusZones;
                    if (_beacons.ContainsKey(zone) && (!wantRunes || _columns.ContainsKey(zone))) continue;

                    RelicLedger.Relic relic = RelicSync.RelicAt(zone);
                    if (!relic.Standing) continue;
                    if (!TryGround(zone, out Vector3 ground)) continue;   // terrain not loaded here yet

                    if (!_beacons.ContainsKey(zone))
                    {
                        Beacon beacon = BuildBeacon(zone, ground, relic.Cursed);
                        if (beacon != null) _beacons[zone] = beacon;
                    }
                    if (wantRunes && !_columns.ContainsKey(zone))
                    {
                        ParticleSystem column = BuildColumn(zone, ground, relic.Cursed);
                        if (column != null) _columns[zone] = column;
                    }
                }
            }
            catch (Exception ex)
            {
                _buildFailed = true;   // a visual that throws once does not get to throw per second
                RagnaroksWrath.Log.LogWarning($"RelicRunes: disabled after error: {ex.Message}");
            }
        }

        private bool Near(ZoneKey zone, ZoneKey centre, int radius)
            => Math.Abs(zone.X - centre.X) <= radius && Math.Abs(zone.Y - centre.Y) <= radius;

        private void PruneColumns(bool show, ZoneKey centre)
        {
            _dead.Clear();
            foreach (KeyValuePair<ZoneKey, ParticleSystem> kv in _columns)
                if (!show || !Near(kv.Key, centre, RuneRadiusZones) || !RelicSync.RelicAt(kv.Key).Standing)
                    _dead.Add(kv.Key);
            for (int i = 0; i < _dead.Count; i++)
            {
                ParticleSystem gone = _columns[_dead[i]];
                _columns.Remove(_dead[i]);
                if (gone != null) Destroy(gone.gameObject);
            }
        }

        private void PruneBeacons(bool show, ZoneKey centre)
        {
            _dead.Clear();
            foreach (KeyValuePair<ZoneKey, Beacon> kv in _beacons)
                if (!show || !Near(kv.Key, centre, BeaconRadiusZones) || !RelicSync.RelicAt(kv.Key).Standing)
                    _dead.Add(kv.Key);
            for (int i = 0; i < _dead.Count; i++)
            {
                Beacon gone = _beacons[_dead[i]];
                _beacons.Remove(_dead[i]);
                if (gone != null && gone.Root != null) Destroy(gone.Root);
            }
        }

        /// <summary>The same deterministic spot the stone was raised on — zone centre snapped
        /// to this client's real loaded terrain. False on a raycast miss (terrain not loaded),
        /// where GetGroundData would otherwise hand back the zone centre at height zero.</summary>
        private static bool TryGround(ZoneKey zone, out Vector3 pos)
        {
            pos = zone.ToWorldPos();
            ZoneSystem zs = ZoneSystem.instance;   // Unity null: never ?. on a UnityEngine.Object
            if (zs == null) return false;
            zs.GetGroundData(ref pos, out _, out _, out _, out Heightmap hmap);
            return hmap != null;
        }

        /// <summary>Per-frame breathing of every beacon: a slow sine on the pillar's alpha
        /// and the glow's intensity. Cosmetic, own try/catch (rule 3); a throw latches the
        /// whole visual off like any other build failure.</summary>
        private void Pulse()
        {
            if (_beacons.Count == 0 || _buildFailed) return;
            try
            {
                float t = Time.time;
                foreach (Beacon b in _beacons.Values)
                {
                    if (b == null || b.Pillar == null) continue;
                    float wave = 0.5f + 0.5f * Mathf.Sin((t / PulseSeconds + b.Phase) * Mathf.PI * 2f);
                    float k = 0.65f + 0.35f * wave;

                    float alpha = BeaconAlpha * k;
                    b.AlphaKeys[0].alpha = alpha;
                    b.AlphaKeys[1].alpha = alpha;
                    b.AlphaKeys[2].alpha = 0f;
                    b.Gradient.SetKeys(b.ColorKeys, b.AlphaKeys);
                    b.Pillar.colorGradient = b.Gradient;   // the setter copies; reusing one Gradient avoids per-frame garbage
                    if (b.Glow != null) b.Glow.intensity = GlowIntensity * k;
                }
            }
            catch (Exception ex)
            {
                _buildFailed = true;
                RagnaroksWrath.Log.LogWarning($"RelicRunes: beacon pulse failed, markers disabled: {ex.Message}");
            }
        }

        private Beacon BuildBeacon(ZoneKey zone, Vector3 ground, bool cursed)
        {
            GameObject go = null;
            try
            {
                if (_beaconMaterial == null)
                    _beaconMaterial = ParticleKit.BuildBoltMaterial("RelicRunes beacon");

                Color tint = cursed ? Cursed : Blessed;
                tint.a = 1f;

                go = new GameObject($"ragnarokswrath_relic_beacon_{zone.X}_{zone.Y}");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.position = ground;

                LineRenderer pillar = go.AddComponent<LineRenderer>();
                pillar.sharedMaterial = _beaconMaterial;
                pillar.useWorldSpace = true;
                pillar.positionCount = 2;
                pillar.SetPosition(0, ground + Vector3.up * 0.2f);
                pillar.SetPosition(1, ground + Vector3.up * BeaconHeight);
                pillar.startWidth = BeaconBaseWidth;
                pillar.endWidth = BeaconTopWidth;
                pillar.textureMode = LineTextureMode.Stretch;
                pillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pillar.receiveShadows = false;

                var lightGo = new GameObject("ragnarokswrath_relic_glow");
                lightGo.transform.SetParent(go.transform, worldPositionStays: false);
                lightGo.transform.position = ground + Vector3.up * 2.5f;
                Light glow = lightGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = tint;
                glow.range = GlowRange;
                glow.intensity = GlowIntensity;
                glow.shadows = LightShadows.None;   // a constant shadowed light costs every frame

                var colorKeys = new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) };
                var alphaKeys = new[]
                {
                    new GradientAlphaKey(BeaconAlpha, 0f),
                    new GradientAlphaKey(BeaconAlpha, BeaconFadeFrom),
                    new GradientAlphaKey(0f, 1f),
                };
                var gradient = new Gradient();
                gradient.SetKeys(colorKeys, alphaKeys);
                pillar.colorGradient = gradient;

                var beacon = new Beacon
                {
                    Root = go,
                    Pillar = pillar,
                    Glow = glow,
                    Tint = tint,
                    Gradient = gradient,
                    ColorKeys = colorKeys,
                    AlphaKeys = alphaKeys,
                    // Stable per zone, so two stones side by side do not breathe in lockstep.
                    Phase = ((zone.X * 73856093) ^ (zone.Y * 19349663)) % 1000 / 1000f,
                };

                RagnaroksWrath.Log.LogInfo(
                    $"RelicRunes: beacon raised at {zone} ({(cursed ? "cursed" : "blessed")}).");
                return beacon;
            }
            catch (Exception ex)
            {
                if (go != null) Destroy(go);
                _buildFailed = true;
                RagnaroksWrath.Log.LogWarning($"RelicRunes: beacon build failed, markers disabled: {ex.Message}");
                return null;
            }
        }

        /// <summary>One rune column at a stone.</summary>
        private ParticleSystem BuildColumn(ZoneKey zone, Vector3 ground, bool cursed)
        {
            try
            {
                if (_material == null)
                    _material = ParticleKit.BuildRuneMaterial("RelicRunes");

                var go = new GameObject($"ragnarokswrath_relic_runes_{zone.X}_{zone.Y}");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.position = ground + Vector3.up * 0.4f;
                var system = go.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);   // a slow rise
                main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.5f);
                main.startColor = cursed ? Cursed : Blessed;
                main.gravityModifier = 0f;
                main.maxParticles = 96;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.2f;

                // One random rune per glyph, held for its whole life — carved, not animated.
                ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = EmissionRate;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.material = _material;
                renderer.sortMode = ParticleSystemSortMode.Distance;

                RagnaroksWrath.Log.LogInfo(
                    $"RelicRunes: column raised at {zone} ({(cursed ? "cursed" : "blessed")}).");
                return system;
            }
            catch (Exception ex)
            {
                _buildFailed = true;
                RagnaroksWrath.Log.LogWarning($"RelicRunes: build failed, runes disabled: {ex.Message}");
                return null;
            }
        }
    }
}
