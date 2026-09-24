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
