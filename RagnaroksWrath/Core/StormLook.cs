namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Which of the two faces a Devastating Storm wears, as pure logic.
    ///
    /// WHY THIS IS ITS OWN TYPE, and not three lines inside WeatherSystem: the look is carried by
    /// the EVENT NAME and by nothing else. Vanilla replicates only the active event's name
    /// (`SendCurrentRandomEvent` → the "SetEvent" RPC → `SetRandomEventByName` → `GetEvent`, all
    /// decompile-verified 2026-09-18), so every machine resolves its own sky from that string.
    /// That makes the string load-bearing in a way a string rarely is: if liveness stops
    /// recognising one of the two names, that storm reads as NO STORM AT ALL — every multiplier
    /// off, no lightning, no announcement, nothing in the log — on a mod whose stated enemy is the
    /// silent no-op. Sitting here, it compiles into the test harness with the rest of Core and is
    /// pinned by tests; sitting in WeatherSystem it could not be, because that file needs
    /// RandEventSystem to exist.
    ///
    /// This is <see cref="StormArea"/>'s pattern: the decision lives in Core where it can be
    /// tested, the system that owns the storm merely drives it.
    /// </summary>
    public static class StormLook
    {
        /// <summary>
        /// The WET storm. Named exactly as it always was, and that is deliberate — vanilla
        /// serialises the active event into the world file, so a rename would strand every
        /// in-flight storm on the first load after an update.
        /// </summary>
        public const string WetEventName = "ragnarokswrath_devastating_storm";

        /// <summary>The DRY storm, added 2026-09-18 alongside the wet one rather than replacing it.</summary>
        public const string DryEventName = "ragnarokswrath_devastating_storm_dry";

        /// <summary>True for either of our storms. The check liveness must use.</summary>
        public static bool IsStorm(string eventName)
            => eventName == WetEventName || eventName == DryEventName;

        /// <summary>
        /// True only for the dry storm. Read off the ACTIVE event rather than remembered from the
        /// roll, so it is right on a client that never rolled, and right after a restart that
        /// resumed a storm from vanilla's saved state.
        /// </summary>
        public static bool IsDry(string eventName) => eventName == DryEventName;

        /// <summary>
        /// Pick a storm's look. Takes the random draw rather than making one, so the boundaries
        /// are testable and a chance of 0 or 1 can be proven absolute rather than assumed.
        /// </summary>
        /// <param name="unitRandom">A draw in [0,1), as System.Random.NextDouble gives.</param>
        /// <param name="dryChance">
        /// Probability of the dry look. Clamped, because a config file can hold anything and a
        /// storm that throws is worse than a storm that is always wet.
        /// </param>
        /// <returns>The event name to start.</returns>
        public static string Roll(double unitRandom, float dryChance)
        {
            if (dryChance <= 0f) return WetEventName;
            if (dryChance >= 1f) return DryEventName;
            return unitRandom < dryChance ? DryEventName : WetEventName;
        }
    }
}
