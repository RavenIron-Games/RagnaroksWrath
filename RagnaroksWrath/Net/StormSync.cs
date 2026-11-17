using System;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Systems.World;

namespace RavenIron.RagnaroksWrath.Net
{
    /// <summary>
    /// Server → client storm settings: the forced sky, the duration and the range.
    ///
    /// WHY IT EXISTS. Vanilla replicates an active event by NAME alone, and each client resolves
    /// the sky, the duration and the range from ITS OWN copy of the event, which this mod baked
    /// from ITS OWN config when the world loaded. So the owner changing `StormsForceWeather` on the
    /// server changed nothing for a player until that player edited their file and relaunched.
    /// This sends the server's values so a client's events follow the server, live.
    ///
    /// SeasonSync's shape on purpose: absolute, unconditional, on a slow cadence plus once at the
    /// moment of a change. No "have I told this peer" bookkeeping, so a joining client is right
    /// within one cadence, a dropped packet heals itself, and the payload is a few dozen bytes.
    ///
    /// A client that never hears (an older server, or this class missing there) keeps using its own
    /// config exactly as before. The receiver refuses to act on the authority, which absorbs a
    /// listen host's copy of its own broadcast.
    /// </summary>
    public static class StormSync
    {
        /// <summary>GUID-prefixed, and renamed on any wire change so a skewed pair no-ops cleanly.</summary>
        public const string RpcName = "com.raveniron.ragnarokswrath.stormsettings";

        /// <summary>Seconds between unconditional broadcasts from the authority.</summary>
        public const float IntervalSeconds = 10f;

        private static ZRoutedRpc _registeredOn;

        /// <summary>
        /// Keyed on the ZRoutedRpc INSTANCE, not a flag: it is per world session. Safe to call from
        /// anywhere, any number of times. A packet for a hash nobody registered is dropped silently
        /// by vanilla, which is why `wrath storm` says whether the values came from the server.
        /// </summary>
        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, _registeredOn)) return;

            try
            {
                rpc.Register<bool, float, float, string, string>(RpcName, RPC_Settings);
                _registeredOn = rpc;

                // New world session, new server: the last one's word is not this one's.
                StormLive.ForgetServer();
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"StormSync: register failed: {ex.Message}");
                _registeredOn = rpc;
            }
        }

        /// <summary>Authority-side: tell everyone the storm settings in effect.</summary>
        public static void Broadcast(StormLive.Settings s)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null) return;

            try
            {
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, s.Force, s.Duration, s.Range, s.WetSky ?? "", s.DrySky ?? "");
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"StormSync: broadcast failed: {ex.Message}");
            }
        }

        private static void RPC_Settings(long sender, bool force, float duration, float range, string wetSky, string drySky)
        {
            // The authority's settings are its own config and are never overridden by a packet.
            if (RagnaroksWrath.IsSimulationAuthority()) return;

            // Only the server may set the storm. Without this, any other player could push a sky
            // onto everyone else's client.
            if (!SenderGuard.FromServer(sender)) return;

            var s = new StormLive.Settings { Force = force, Duration = duration, Range = range, WetSky = wetSky, DrySky = drySky };
            if (!StormLive.AcceptFromServer(s))
            {
                RagnaroksWrath.Log.LogWarning("StormSync: the server sent storm settings that make no sense; keeping the last good ones.");
                return;
            }

            WeatherSystem.RefreshStormEvents();
        }
    }
}
