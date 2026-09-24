using System.Collections.Generic;
using UnityEngine;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Arson blame from FireFront's per-fire igniters (FireFront 1.0.2+,
    /// `CollectActiveFiresWithIgniters`). Pure so the harness can pin it.
    ///
    /// Each burning fire carries the player who lit the fire it spread from, 0 for natural or
    /// unknown. Blame is the distinct (zone, igniter) pairs: a zone with ten of A's fires books
    /// A once per tick, exactly as one zone booked its one igniter before; a zone burning with
    /// both A's and B's fires books both. Nobody is billed for a fire they did not light.
    /// </summary>
    public static class FireBlame
    {
        /// <summary>
        /// Fill <paramref name="into"/> (cleared first) with the distinct zone and igniter pairs.
        /// False, with nothing written, when the two lists do not line up; the caller then books
        /// no harm rather than guessing.
        /// </summary>
        public static bool Collect(List<Vector3> positions, List<long> igniters,
                                   List<KeyValuePair<ZoneKey, long>> into)
        {
            into.Clear();
            if (positions == null || igniters == null || positions.Count != igniters.Count) return false;

            for (int i = 0; i < positions.Count; i++)
            {
                long igniter = igniters[i];
                if (igniter == 0) continue;

                var pair = new KeyValuePair<ZoneKey, long>(ZoneKey.FromWorldPos(positions[i]), igniter);
                if (!into.Contains(pair)) into.Add(pair);
            }
            return true;
        }
    }
}
