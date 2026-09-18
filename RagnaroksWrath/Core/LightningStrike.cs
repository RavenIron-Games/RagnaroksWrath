using System;
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
        /// Whether the SKY permits a bolt — the one gate that cannot be answered by asking
        /// the engine what the weather is.
        ///
        /// A storm's look is a vanilla event's `m_forceEnvironment`, and vanilla applies that
        /// override PER MACHINE, through a path a dedicated server never walks: with no local
        /// player there is nothing to put inside the event area, so the server keeps reporting
        /// its OWN unforced weather. `EnvMan.IsWet()` on a headless authority therefore
        /// describes a sky nobody is looking at, uncorrelated with the storm every player can
        /// see. Asking it there let a bolt land under a forced `ThunderStorm` one log line
        /// after the mod announced that rain suppresses lightning — observed live on a
        /// dedicated server 2026-09-18, in both of that session's wet storms, while the
        /// connected client showed the vanilla `Wet` status and visible rainfall.
        ///
        /// So when the sky is forced, the ROLLED LOOK is the only honest answer — and it is
        /// right on the authority by construction, because the authority is what rolled it.
        /// When no sky is forced the storm imposes nothing, the world's real weather is the
        /// truth, and this behaves exactly as it always did.
        /// </summary>
        /// <param name="forcedSky">StormsForceWeather: the storm imposes an environment on clients.</param>
        /// <param name="stormIsDry">The look this storm rolled, read from the live event's name.</param>
        /// <param name="envIsWet">EnvMan.IsWet() as THIS machine sees it. Consulted only when
        /// no sky is forced, because only then does it describe the storm anyone is standing in.</param>
        public static bool SkyAllows(bool forcedSky, bool stormIsDry, bool envIsWet)
            => forcedSky ? stormIsDry : !envIsWet;

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
