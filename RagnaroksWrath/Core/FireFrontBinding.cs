namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Which FireFront build the bridge's reflection handles belong to. Fed, each tick, the
    /// assembly that FireFront's plugin entry currently points at (or null when there is none).
    ///
    /// A process used to load FireFront exactly once, so the handles were resolved once and kept.
    /// A dev reload (Hotload, 2026-11-24) breaks that: the new build arrives under a new
    /// assembly, and the old build's FireManager is destroyed but still answers through a cached
    /// handle, returning the fires it held at the moment it died. Seen on Storm10 on 2026-11-24:
    /// after FireFront 1.0.5 was swapped for 1.0.6 mid-session, about 6,000 ground cells burned
    /// and no zone's scorch moved between two saves (whether that fire sat in a tracked zone was
    /// not pinned down). So: a different assembly means every handle is dropped and re-resolved.
    ///
    /// Takes object, not Assembly, so the off-game harness can drive it with plain stand-ins.
    /// </summary>
    public sealed class FireFrontBinding
    {
        public enum Step
        {
            /// <summary>No FireFront right now (between an unload and the next load): skip the tick.</summary>
            Wait,
            /// <summary>Same build as last tick: keep every handle.</summary>
            Keep,
            /// <summary>First build seen: resolve handles as usual.</summary>
            Bind,
            /// <summary>A different build than the handles belong to: drop them all and resolve again.</summary>
            Rebind,
        }

        private object _bound;

        public object Bound => _bound;

        public Step Observe(object current)
        {
            if (current == null) return Step.Wait;
            if (_bound == null)
            {
                _bound = current;
                return Step.Bind;
            }
            if (ReferenceEquals(_bound, current)) return Step.Keep;
            _bound = current;
            return Step.Rebind;
        }
    }
}
