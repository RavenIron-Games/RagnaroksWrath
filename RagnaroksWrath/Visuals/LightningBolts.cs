using System;
using System.Collections.Generic;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Net;
using UnityEngine;

namespace RavenIron.RagnaroksWrath.Visuals
{
    /// <summary>
    /// What a lightning strike looks and sounds like, on each player's game. Added 2026-11-17: until
    /// then a bolt was a text line and a fire, nothing to see and nothing to hear.
    ///
    /// THE BOLT is drawn, not shipped (the locked visuals rule): a jagged main channel from the
    /// cloud base to whatever stands tallest at the strike (a treetop, or the ground), with a few
    /// forks dying out in the air, all from LightningStrike's pure geometry and the strike's seed, so
    /// every player sees the same bolt. It flickers through 2-4 return strokes tens of milliseconds
    /// apart, which is what real lightning does, then leaves a short afterglow.
    ///
    /// THE FLASH is a bright point light at the strike, pulsing with the strokes, so the trees and
    /// the ground around it light up. THE THUNDER is vanilla's own: the flash and thunder effects
    /// its ThunderStorm weather plays (the `Thunder` component on that environment's object), reused
    /// with the owner's OK on 2026-11-17 because nothing else sounds like Valheim's sky; nothing is
    /// added to the package. The thunder is delayed by the distance over the speed of sound, so a
    /// close strike cracks at once and a far one rumbles seconds after the flash.
    ///
    /// No coroutines (rule 2): one Update walks a short list of live strikes. Everything here is
    /// cosmetic and runs inside its own try/catch (rule 3); the damage is LightningSync's, done
    /// before this is called.
    /// </summary>
    public class LightningBolts : MonoBehaviour
    {
        private static LightningBolts _instance;

        // Strikes farther than this from the camera are not drawn at all: a bolt 5 km away is a
        // pixel, and its thunder would arrive fifteen seconds late from nowhere.
        private const float DrawDistance = 3000f;
        // The cloud base above the strike. Real cloud bases sit a few hundred metres up.
        private const float CloudHeightMin = 220f, CloudHeightMax = 380f;
        private const float StrokeOnSeconds = 0.06f, AfterglowSeconds = 0.25f;

        private Material _material;
        private bool _buildFailed;
        private Thunder _vanillaThunder;
        private bool _thunderSearched;
        private readonly List<Strike> _live = new List<Strike>(4);

        private sealed class Strike
        {
            public float Age;
            public float[] Strokes;
            public float ThunderAt;
            public bool Thundered;
            public Vector3 Cloud, End;
            public LineRenderer Main;
            public readonly List<LineRenderer> Forks = new List<LineRenderer>(4);
            public Light Flash;
            public GameObject Root;
        }

        private void Awake() => _instance = this;

        /// <summary>Called by LightningSync for every strike the server sends.</summary>
        public static void Show(Vector3 ground, float top, int seed)
        {
            LightningBolts self = _instance;
            if (self == null || !ModConfig.LightningBoltsEnabled.Value) return;
            self.Begin(ground, top, seed);
        }

        private void Update()
        {
            LightningSync.EnsureRegistered();   // the render loop is where a pure client arms
            if (_live.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Strike s = _live[i];
                bool done = true;
                try { done = Step(s, dt); }
                catch (Exception ex) { RagnaroksWrath.Log.LogWarning($"LightningBolts: a strike failed mid-flash: {ex.Message}"); }
                finally
                {
                    // Whatever happened, a finished or broken strike leaves nothing behind.
                    if (done)
                    {
                        if (s.Root != null) Destroy(s.Root);
                        _live.RemoveAt(i);
                    }
                }
            }
        }

        private void Begin(Vector3 ground, float top, int seed)
        {
            try
            {
                Camera cam = Utils.GetMainCamera();
                if (cam == null) return;
                Vector3 eye = cam.transform.position;
                float distance = Vector3.Distance(eye, ground);
                if (distance > DrawDistance) return;

                if (!EnsureMaterial()) return;

                var rng = new System.Random(seed);
                Vector3 end = FindEnd(ground, top);
                float cloudUp = CloudHeightMin + (float)rng.NextDouble() * (CloudHeightMax - CloudHeightMin);
                // Bolts lean: the channel starts off to one side and comes down at an angle.
                float lean = cloudUp * 0.25f;
                double lat = rng.NextDouble() * Math.PI * 2;
                var cloud = new Vector3(end.x + (float)Math.Cos(lat) * lean, end.y + cloudUp, end.z + (float)Math.Sin(lat) * lean);

                var s = new Strike
                {
                    Strokes = LightningStrike.StrokeTimes(seed),
                    ThunderAt = LightningStrike.ThunderDelay(Vector3.Distance(eye, end)),
                    Cloud = cloud,
                    End = end,
                    Root = new GameObject("RW_LightningStrike"),
                };

                List<Vector3> path = LightningStrike.BoltPath(cloud, end, seed, 7, 0.22f);
                s.Main = Line(s.Root, path, 1.1f, 0.5f);
                foreach (List<Vector3> fork in LightningStrike.BoltBranches(path, seed, 2 + rng.Next(3), 5, 0.3f))
                    s.Forks.Add(Line(s.Root, fork, 0.4f, 0.05f));

                var lightGo = new GameObject("RW_LightningFlash");
                lightGo.transform.SetParent(s.Root.transform, false);
                lightGo.transform.position = end + Vector3.up * 15f;
                s.Flash = lightGo.AddComponent<Light>();
                s.Flash.type = LightType.Point;
                s.Flash.color = new Color(0.82f, 0.88f, 1f);
                s.Flash.range = 160f;
                s.Flash.intensity = 0f;
                s.Flash.shadows = LightShadows.None;   // a hard flash with shadows costs a frame on every client

                // Vanilla's sky flash, high over the strike, where the thunderstorm weather plays it.
                Thunder thunder = VanillaThunder();
                if (thunder != null && thunder.m_flashEffect != null)
                    thunder.m_flashEffect.Create(cloud, Quaternion.identity);

                SetVisible(s, false);
                _live.Add(s);
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"LightningBolts: could not draw a strike: {ex.Message}");
            }
        }

        /// <returns>True when the strike is over.</returns>
        private bool Step(Strike s, float dt)
        {
            s.Age += dt;

            // Which stroke is lit now, if any. Forks show on the first stroke only, as in a real
            // flash, where the later strokes follow the main channel the first one opened.
            bool lit = false;
            bool first = false;
            for (int k = 0; k < s.Strokes.Length; k++)
            {
                float t = s.Age - s.Strokes[k];
                if (t >= 0f && t < StrokeOnSeconds) { lit = true; first = k == 0; break; }
            }
            float lastStroke = s.Strokes[s.Strokes.Length - 1] + StrokeOnSeconds;
            float glow = s.Age > lastStroke ? 1f - (s.Age - lastStroke) / AfterglowSeconds : 0f;

            if (lit)
            {
                SetMain(s, 1f);
                foreach (LineRenderer f in s.Forks) f.enabled = first;
                s.Flash.intensity = first ? 9f : 6f;
            }
            else if (glow > 0f)
            {
                SetMain(s, glow * 0.45f);
                foreach (LineRenderer f in s.Forks) f.enabled = false;
                s.Flash.intensity = glow * 1.5f;
            }
            else
            {
                s.Main.enabled = false;
                foreach (LineRenderer f in s.Forks) f.enabled = false;
                s.Flash.intensity = 0f;
            }

            if (!s.Thundered && s.Age >= s.ThunderAt)
            {
                s.Thundered = true;
                Thunder thunder = VanillaThunder();
                if (thunder != null && thunder.m_thunderEffect != null)
                    thunder.m_thunderEffect.Create(s.End, Quaternion.identity);
            }

            bool visualDone = s.Age > lastStroke + AfterglowSeconds;
            return visualDone && s.Thundered;
        }

        private static void SetMain(Strike s, float alpha)
        {
            s.Main.enabled = true;
            var c = new Color(0.86f, 0.92f, 1f, alpha);
            s.Main.startColor = c;
            s.Main.endColor = c;
        }

        private static void SetVisible(Strike s, bool on)
        {
            s.Main.enabled = on;
            foreach (LineRenderer f in s.Forks) f.enabled = on;
        }

        /// <summary>
        /// Where the bolt ends: the first solid thing straight down over the strike, which is the
        /// crown of the tree it took, or the ground. Searched only from a little above what the
        /// server called the top, so a bolt never ends on a roof or a bird overhead.
        /// </summary>
        private static Vector3 FindEnd(Vector3 ground, float top)
        {
            float from = Mathf.Max(top, ground.y) + 6f;
            var origin = new Vector3(ground.x, from, ground.z);
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "terrain");
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, from - ground.y + 30f, mask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(ground.x, Mathf.Max(ground.y, top), ground.z);
        }

        private LineRenderer Line(GameObject root, List<Vector3> points, float widthEnd, float widthStart)
        {
            var go = new GameObject("RW_LightningChannel");
            go.transform.SetParent(root.transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.startWidth = widthStart;   // thin at the cloud
            line.endWidth = widthEnd;       // thick where it lands
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            if (_buildFailed) return false;
            try { _material = ParticleKit.BuildBoltMaterial("LightningBolts"); return true; }
            catch (Exception ex)
            {
                _buildFailed = true;
                RagnaroksWrath.Log.LogWarning($"LightningBolts: no material, bolts will not be drawn (thunder still plays): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Vanilla's Thunder component, from whichever environment carries one (ThunderStorm does).
        /// Read-only: its effect lists are used to play a flash and a sound, nothing is changed on
        /// it, and no environment is ever selected (rule 4). Searched until EnvMan is up, then once.
        /// </summary>
        private Thunder VanillaThunder()
        {
            if (_vanillaThunder != null || _thunderSearched) return _vanillaThunder;
            EnvMan env = EnvMan.instance;
            if (env == null || env.m_environments == null) return null;

            _thunderSearched = true;
            foreach (EnvSetup setup in env.m_environments)
            {
                if (setup == null || setup.m_envObject == null) continue;
                Thunder t = setup.m_envObject.GetComponentInChildren<Thunder>(includeInactive: true);
                if (t != null)
                {
                    _vanillaThunder = t;
                    RagnaroksWrath.Log.LogInfo($"LightningBolts: using vanilla thunder and flash from the '{setup.m_name}' weather.");
                    return t;
                }
            }
            RagnaroksWrath.Log.LogWarning("LightningBolts: no vanilla weather carries a Thunder component; bolts will be silent.");
            return null;
        }
    }
}
