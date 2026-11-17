namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// Which settings ServerSync carries from the server to every player, as pure logic the
    /// harness can pin. Everything is synced EXCEPT what is genuinely the player's own business:
    ///
    ///  - the config layout stamp, which each file earns by migrating itself (a synced stamp would
    ///    tell a client's file it had reached a version its own lines never did);
    ///  - VerboseLogging, which only changes what THIS machine writes to its own log;
    ///  - the purely visual dials, a player's preference about what their own screen draws.
    ///
    /// The zone-sync settings sit in the visuals section but are the SERVER's (it decides what it
    /// sends and how often), so they are synced like any other.
    ///
    /// Why sync at all: 44 of the 145 settings are read on each player's game, not the server's
    /// (CLAUDE.md, the client-read trap), so before ServerSync a change on the server never reached
    /// the place it took effect. Syncing makes the server's file the one that counts, everywhere.
    /// </summary>
    public static class SyncRules
    {
        public const string MetaSection = ConfigLedger.MetaSection;
        public const string GeneralSection = "01 - General";
        public const string VisualsSection = "15 - Visuals";

        public static bool IsLocal(string section, string key)
        {
            if (section == MetaSection) return true;
            if (section == GeneralSection && key == "VerboseLogging") return true;
            if (section == VisualsSection)
                return !(key == "EnableZoneSync" || key == "ZoneSyncIntervalSeconds" || key == "ZoneSyncRadiusZones");
            return false;
        }
    }
}
