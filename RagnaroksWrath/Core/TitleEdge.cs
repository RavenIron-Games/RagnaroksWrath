using System.Collections.Generic;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Rising-edge detection for titles. Pure so the harness can pin it.
    ///
    /// Every title is earned on the tick its condition BECOMES true for a player, never on every
    /// tick it stays true. Until 2026-09-24 Stormrider, Plaguewalker and Winterborn were awarded
    /// every tick their condition held, so two that held together (a storm over plague, plague in
    /// winter) swapped back and forth every title tick, each swap announced to the whole server
    /// and written to disk.
    /// </summary>
    public static class TitleEdge
    {
        /// <summary>
        /// True exactly once per episode: on the first call where <paramref name="condition"/> is
        /// true for this player. A false condition re-arms it.
        /// </summary>
        public static bool Rises(HashSet<long> held, long playerId, bool condition)
        {
            if (!condition)
            {
                held.Remove(playerId);
                return false;
            }
            return held.Add(playerId);
        }
    }
}
