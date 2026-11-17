using System;
using System.Collections.Generic;
using UnityEngine;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Where storm fires BEGIN — the arithmetic half of lightning. Devastating Storms have
    /// multiplied fire RISK since 0.2.2 without ever producing a flame; lightning closes
    /// that gap: a rare bolt during a storm asks FireFront to ignite ground near a player
    /// standing under it. This class is only the dice and the geometry — per-tick chance
    /// from a configured mean, and the ring around the player where the bolt lands. The
    /// gates (a real player present, rain, the homestead standoff) live in FireSystem, and
    /// the fire itself is entirely FireFront's: a strike is one ignite call, never a
    /// second simulation.
    /// </summary>
    public static class LightningStrike
    {
        /// <summary>
        /// Whether the SKY permits a bolt where it would land. Two cases, and in neither may the
        /// engine's own weather be asked on the authority.
        ///
        /// A FORCED sky is the storm's rolled look, one verdict for the whole storm. That look
        /// is a vanilla event's `m_forceEnvironment`, and vanilla applies the override PER
        /// MACHINE, through a path a dedicated server never walks: with no local player there
        /// is nothing to put inside the event area. Asking `EnvMan.IsWet()` there let a bolt
        /// land under a forced `ThunderStorm` one log line after the mod announced that rain
        /// suppresses lightning — observed live on a dedicated server 2026-09-18, in both of
        /// that session's wet storms, while the connected client showed the vanilla `Wet`
        /// status. The rolled look is right on the authority by construction, because the
        /// authority is what rolled it.
        ///
        /// With NO sky forced the storm imposes nothing and the world's own weather decides, at
        /// the spot the bolt would land. Through 0.27.5 that came from `EnvMan.IsWet()` too, and
        /// a dedicated server never updates it: `EnvMan.UpdateEnvironment` returns before its
        /// weather roll when there is no main camera, so it reads `Awake`'s default (Clear,
        /// dry) for the life of the process, and a bolt could land in rain on every dedicated
        /// server under the default config. FireSystem now asks FireFront, which replays
        /// vanilla's own roll for a position and puts its own fires out by the same answer.
        /// An answer that cannot be had — a FireFront too old to give it, or a call that
        /// failed — is not a dry sky: the bolt is withheld, the fail-closed rule the homestead
        /// standoff already follows. That covers only what THIS side can see. FireFront's read
        /// answers dry, not unknown, whenever it cannot see the weather itself (EnvMan not up
        /// yet, or its own reflection broken by a game update), so this gate is exactly as right
        /// about rain as FireFront's own suppression is, and no more. tools\apiprobe checks the
        /// game members that read reaches for.
        /// </summary>
        /// <param name="forcedSky">StormsForceWeather: the storm imposes an environment on clients.</param>
        /// <param name="stormIsDry">The look this storm rolled, read from the live event's name.</param>
        /// <param name="rainingWhereItLands">Whether rain falls where the bolt would land, or null
        /// when that cannot be known. Consulted only when no sky is forced.</param>
        public static bool SkyAllows(bool forcedSky, bool stormIsDry, bool? rainingWhereItLands)
            => forcedSky ? stormIsDry : rainingWhereItLands == false;

        /// <summary>Per-tick chance of one strike, from the configured mean minutes
        /// between strikes while a storm holds at least one player. Garbage disables
        /// rather than floods — the PlagueGenesis contract, same shape on purpose.</summary>
        public static float ChancePerTick(float intervalSeconds, float meanMinutes)
        {
            if (float.IsNaN(intervalSeconds) || intervalSeconds <= 0f) return 0f;
            if (float.IsNaN(meanMinutes) || meanMinutes <= 0f) return 0f;

            float chance = intervalSeconds / (meanMinutes * 60f);
            return chance > 1f ? 1f : chance;
        }

        // ---- realism (2026-11-17): what the bolt hits, what it does, what it looks like ----------

        /// <summary>One place a bolt could land: the ground under it, and how high its top stands.</summary>
        public struct Target
        {
            public Vector3 Ground;   // where the fire starts and the damage is centred
            public float Top;        // world height of the highest point there (a treetop, a hill)
            public bool IsTree;
        }

        /// <summary>
        /// Which target the bolt takes. Real lightning goes to the highest point within striking
        /// distance, but not always: the leader wanders, so a slightly lower point sometimes wins.
        /// Each candidate is weighted by exp((top - highest) / preference): with the 3 m FireSystem
        /// uses, a point 3 m lower is about a third as likely and a tree 13 m above seven ground
        /// samples takes the bolt about 9 times in 10. A preference of 0 or less always takes the
        /// highest. Returns -1 when there is no candidate with a real height.
        /// </summary>
        /// <param name="u">A uniform [0,1) draw, a parameter so the choice is testable.</param>
        public static int ChooseTarget(IList<Target> candidates, double u, float preferenceMeters)
        {
            if (candidates == null || candidates.Count == 0) return -1;

            // A NaN height is no height: it must never be "the highest" (every comparison with NaN
            // is false, so a NaN first entry used to win by default).
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (float.IsNaN(candidates[i].Top)) continue;
                if (best < 0 || candidates[i].Top > candidates[best].Top) best = i;
            }
            if (best < 0) return -1;
            if (float.IsNaN(preferenceMeters) || preferenceMeters <= 0f) return best;

            float highest = candidates[best].Top;
            double total = 0;
            var weights = new double[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                float top = candidates[i].Top;
                double w = float.IsNaN(top) ? 0 : Math.Exp((top - highest) / preferenceMeters);
                weights[i] = w;
                total += w;
            }
            if (!(total > 0)) return best;

            double pick = (u < 0 ? 0 : u >= 1 ? 0.999999 : u) * total;
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (pick < 0) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>
        /// Lightning damage to someone <paramref name="distance"/> metres (XZ) from the strike: the
        /// full amount at the point, falling off with the square of the distance to nothing at
        /// <paramref name="radius"/>. Squared because a ground strike's current and the side flash
        /// both fade fast: at half the radius it is a quarter. Garbage gives no damage, never more.
        /// </summary>
        public static float DamageAt(float distance, float radius, float maxDamage)
        {
            if (float.IsNaN(distance) || float.IsNaN(radius) || float.IsNaN(maxDamage)) return 0f;
            if (radius <= 0f || maxDamage <= 0f || distance < 0f || distance >= radius) return 0f;
            float k = 1f - distance / radius;
            return maxDamage * k * k;
        }

        /// <summary>
        /// The bolt's main channel, from the cloud to the ground: midpoint displacement, the
        /// standard way to draw lightning. Each pass splits every segment and pushes its midpoint
        /// sideways by up to <paramref name="jag"/> times the segment's length, so the bolt is
        /// jagged at every scale, coarse kinks and fine crackle alike, the way a real channel is.
        /// Deterministic for a seed, so every player sees the same bolt. Both ends are exact.
        /// </summary>
        public static List<Vector3> BoltPath(Vector3 cloud, Vector3 ground, int seed, int passes, float jag)
        {
            var rng = new System.Random(seed);
            var pts = new List<Vector3> { cloud, ground };
            if (passes < 0) passes = 0;
            if (passes > 8) passes = 8;   // 257 points; more is invisible and costs a frame
            if (float.IsNaN(jag) || jag < 0f) jag = 0f;

            for (int p = 0; p < passes; p++)
            {
                var next = new List<Vector3>(pts.Count * 2);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1];
                    float dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    float off = len * jag;
                    next.Add(a);
                    next.Add(new Vector3(
                        (a.x + b.x) * 0.5f + (float)(rng.NextDouble() * 2 - 1) * off,
                        (a.y + b.y) * 0.5f + (float)(rng.NextDouble() * 2 - 1) * off * 0.3f,
                        (a.z + b.z) * 0.5f + (float)(rng.NextDouble() * 2 - 1) * off));
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }
            return pts;
        }

        /// <summary>
        /// Forks off the main channel: real bolts branch downward from the upper part of the
        /// channel and die out in the air. Each starts at a point of <paramref name="main"/> (never
        /// the last third, where the channel has already found the ground) and runs down and out
        /// for a fraction of the bolt's height. Same seed, same forks, on every machine.
        /// </summary>
        public static List<List<Vector3>> BoltBranches(List<Vector3> main, int seed, int count, int passes, float jag)
        {
            var result = new List<List<Vector3>>();
            if (main == null || main.Count < 4 || count <= 0) return result;

            var rng = new System.Random(seed ^ 0x5bd1e995);
            Vector3 top = main[0], end = main[main.Count - 1];
            float height = Math.Abs(top.y - end.y);
            int limit = Math.Max(1, (main.Count * 2) / 3);

            for (int b = 0; b < count; b++)
            {
                Vector3 from = main[rng.Next(1, limit)];
                float reach = height * (0.15f + (float)rng.NextDouble() * 0.25f);
                double angle = rng.NextDouble() * 2 * Math.PI;
                var to = new Vector3(
                    from.x + (float)Math.Cos(angle) * reach * 0.6f,
                    from.y - reach,
                    from.z + (float)Math.Sin(angle) * reach * 0.6f);
                result.Add(BoltPath(from, to, rng.Next(), passes, jag));
            }
            return result;
        }

        /// <summary>
        /// When each stroke of a flash fires, in seconds from the first. A real flash is several
        /// return strokes down the same channel, tens of milliseconds apart, which is what makes
        /// lightning flicker rather than blink. 2 to 4 strokes, 40 to 120 ms apart.
        /// </summary>
        public static float[] StrokeTimes(int seed)
        {
            var rng = new System.Random(seed ^ 0x27d4eb2d);
            int n = 2 + rng.Next(3);
            var t = new float[n];
            for (int i = 1; i < n; i++) t[i] = t[i - 1] + 0.04f + (float)rng.NextDouble() * 0.08f;
            return t;
        }

        /// <summary>Seconds before the thunder is heard: distance over the speed of sound in air.</summary>
        public const float SpeedOfSound = 343f;

        public static float ThunderDelay(float distanceMeters)
            => (float.IsNaN(distanceMeters) || distanceMeters <= 0f) ? 0f : distanceMeters / SpeedOfSound;

        /// <summary>
        /// The bolt's landing point: area-uniform over the ring between minRadius and
        /// maxRadius around the anchor (sqrt keeps strikes from bunching at the inner
        /// edge), at the anchor's own height — a strike lands near a LOADED player, so
        /// the anchor's Y is honest, and the server must never ask for ground height
        /// (it returns its input on a miss). u1/u2 are uniform [0,1) rolls, taken as
        /// parameters so the geometry is deterministic under test. NaN or negative radii
        /// clamp to zero; an inverted pair collapses to the surviving honest value.
        /// </summary>
        public static Vector3 StrikePoint(Vector3 anchor, double u1, double u2,
                                          float minRadius, float maxRadius)
        {
            float lo = (float.IsNaN(minRadius) || minRadius < 0f) ? 0f : minRadius;
            float hi = (float.IsNaN(maxRadius) || maxRadius < lo) ? lo : maxRadius;

            double r = Math.Sqrt(lo * lo + (hi * hi - lo * lo) * u1);
            double angle = u2 * 2.0 * Math.PI;

            return new Vector3(
                anchor.x + (float)(r * Math.Cos(angle)),
                anchor.y,
                anchor.z + (float)(r * Math.Sin(angle)));
        }
    }
}
