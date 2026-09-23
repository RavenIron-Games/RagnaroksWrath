using UnityEngine;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Whether a world position lies inside a storm's area of effect.
    ///
    /// The maths here is copied EXACTLY from `RandEventSystem.IsInsideRandomEventArea`, and that
    /// is the whole point of the file. Vanilla uses that test to decide whether a player sees the
    /// storm's banner and its spawns; we use ours to decide whether the storm's gameplay
    /// multipliers apply. Those are two enforcers of one boundary, and if they disagree the
    /// symptom is a player standing under a storm banner taking no extra fire risk — which is
    /// nearly unreadable from a log, because both halves are behaving exactly as written.
    ///
    /// Three details are load-bearing, and all three are vanilla's:
    ///
    /// 1. Distance is XZ ONLY. A storm reaches up the mountain above it, not merely across flat
    ///    ground. Using Vector3.Distance would shrink the area for anyone climbing.
    /// 2. The comparison is strictly less-than, so the perimeter itself is outside.
    /// 3. Anything above y = 3000 is outside regardless — vanilla's guard for players who are
    ///    mid-teleport or otherwise off the map.
    /// </summary>
    public static class StormArea
    {
        /// <summary>Above this height a position is outside every storm. Vanilla's constant.</summary>
        public const float SkyCeiling = 3000f;

        /// <summary>
        /// Whether a storm's clock stops while nobody stands inside its area. FALSE, and it must stay
        /// false: this is the value `WeatherSystem` registers as the event's `m_pauseIfNoPlayerInArea`.
        ///
        /// Through 0.27.4 it was true, described as vanilla's "stop running where nobody is" behaviour
        /// inherited for free. It is not a behaviour, it is a freeze. `RandomEvent.Update` returns
        /// before `m_time += dt` whenever the flag is on and no player is within the event's range
        /// (decompiled from 1.0.15), so a storm everyone had walked away from, or logged off under,
        /// never reached its duration and never ended. Because this mod reads "a storm event exists"
        /// as "a storm is on", the frozen storm also blocked every later storm and kept adding its
        /// burden to the whole world's condition, indefinitely. A storm is weather: it blows over
        /// whether or not anyone watched. ValkyriesCargo hit the same flag on its merchant visit and
        /// made the same fix (its PR #97).
        /// </summary>
        public const bool ClockPausesWithNobodyInside = false;

        public static bool Contains(Vector3 centre, float range, Vector3 position)
        {
            if (position.y > SkyCeiling) return false;

            return DistanceXZ(position, centre) < range;
        }

        /// <summary>
        /// Mirrors `Utils.DistanceXZ`. Reimplemented rather than called so this file stays free of
        /// game types and the harness can compile and test it — the formula is two subtractions
        /// and a square root, and it is pinned by a test against a known distance.
        /// </summary>
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;

            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
