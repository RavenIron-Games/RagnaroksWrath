using System.Collections.Generic;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Which burning zones belong to the attributed fire. Pure so the harness can pin it.
    ///
    /// FireFront exposes ONE igniter, `CurrentFireIgniterPlayerId`: captured when a fire starts
    /// while nothing else burns, cleared only when every fire in the world is out. FireFront runs
    /// several independent fire events at once, so billing every burning zone to that one id
    /// charged the first arsonist for every later, unrelated fire on the map (review 2026-09-24).
    ///
    /// Until FireFront offers a per-event igniter, attribution follows the fire itself: the zones
    /// burning when an igniter is first seen are that igniter's, and a zone joins them only by
    /// burning next to one that already is (8-neighbour contact, grown to a fixpoint each tick).
    /// A fire started elsewhere is not adjacent and is billed to nobody. The footprint remembers
    /// every zone it has claimed until the igniter changes or clears, so a fire that burns out
    /// behind its own front stays connected.
    ///
    /// Known limits: a fire that is already burning elsewhere when an igniter is first seen joins
    /// the seed (FireFront only captures an igniter when nothing burns, so that needs a second fire
    /// inside one scorch interval, or a restart mid-fire); and if every fire goes out and the same
    /// player lights a new one elsewhere between two ticks, the new fire is billed to nobody.
    /// </summary>
    public sealed class ArsonFootprint
    {
        private readonly HashSet<ZoneKey> _zones = new HashSet<ZoneKey>();

        /// <summary>The igniter this footprint belongs to; 0 when none.</summary>
        public long Igniter { get; private set; }

        public int ZoneCount => _zones.Count;

        /// <summary>
        /// Update for this tick and write the burning zones attributable to
        /// <paramref name="igniter"/> into <paramref name="attributed"/> (cleared first).
        /// </summary>
        public void Observe(long igniter, List<ZoneKey> burning, List<ZoneKey> attributed)
        {
            attributed.Clear();

            if (igniter == 0)
            {
                Igniter = 0;
                _zones.Clear();
                return;
            }

            if (igniter != Igniter)
            {
                // A new culprit: FireFront captured it when this fire started with nothing else
                // burning, so what burns now is theirs.
                Igniter = igniter;
                _zones.Clear();
                for (int i = 0; i < burning.Count; i++) _zones.Add(burning[i]);
            }
            else
            {
                // Grow by contact until nothing more joins, so a front that crossed several
                // zones since the last tick is followed along its whole length.
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    for (int i = 0; i < burning.Count; i++)
                    {
                        ZoneKey z = burning[i];
                        if (_zones.Contains(z) || !TouchesFootprint(z)) continue;
                        _zones.Add(z);
                        grew = true;
                    }
                }
            }

            for (int i = 0; i < burning.Count; i++)
                if (_zones.Contains(burning[i])) attributed.Add(burning[i]);
        }

        private bool TouchesFootprint(ZoneKey z)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (_zones.Contains(new ZoneKey(z.X + dx, z.Y + dy))) return true;
            }
            return false;
        }

        public void Clear()
        {
            Igniter = 0;
            _zones.Clear();
        }
    }
}
