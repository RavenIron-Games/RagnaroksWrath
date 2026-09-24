using System;
using System.Collections.Generic;
using HarmonyLib;

namespace RavenIron.RagnaroksWrath.Net
{
    /// <summary>
    /// Who really sent a routed RPC (review 2026-09-24, nice-to-have 7).
    ///
    /// A routed RPC's sender id is written by the SENDING client, and the server forwards it
    /// unchanged, so a handler cannot trust it on its own. Two halves:
    ///
    ///  - On the server, <see cref="Patch_RoutedRpcSender"/> drops any packet for the relic and
    ///    zone RPCs whose claimed sender is not the peer it actually arrived from. After that,
    ///    a sender id that reaches a handler is real.
    ///  - On a client, <see cref="FromServer"/> accepts the server-to-client RPCs only from the
    ///    server, so another player cannot push fake zone state or make this client raise stones.
    ///
    /// Only this mod's own RPC names are checked; every other routed RPC passes untouched.
    /// </summary>
    public static class SenderGuard
    {
        private static readonly HashSet<int> Guarded = new HashSet<int>
        {
            RelicSync.SetRpc.GetStableHashCode(),
            RelicSync.PlaceRpc.GetStableHashCode(),
            RelicSync.PlacedRpc.GetStableHashCode(),
            RelicSync.BrokenRpc.GetStableHashCode(),
            ZoneSync.RpcName.GetStableHashCode(),
        };

        // One warning per peer per session is enough to name a misbehaving client.
        private static readonly HashSet<long> _warned = new HashSet<long>();

        /// <summary>
        /// True when <paramref name="sender"/> is the server. On the authority (a listen host, or
        /// single-player) that is this machine's own id: its broadcasts reach it locally.
        /// GetServerPeerID is private in the real assembly (rule 5), so the peer is read instead.
        /// </summary>
        public static bool FromServer(long sender)
        {
            ZNet znet = ZNet.instance;
            if (znet == null) return false;
            if (znet.IsServer()) return sender == ZNet.GetUID();

            ZNetPeer server = znet.GetServerPeer();
            return server != null && server.m_uid == sender;
        }

        internal static bool IsGuarded(int methodHash) => Guarded.Contains(methodHash);

        internal static void WarnOnce(long realPeer, long claimed, int methodHash)
        {
            if (!_warned.Add(realPeer)) return;
            RagnaroksWrath.Log.LogWarning(
                $"SenderGuard: dropped an RPC (hash {methodHash}) from peer {realPeer} claiming to be " +
                $"{claimed}. A modified client, or a bug; further drops from this peer are silent.");
        }
    }

    /// <summary>
    /// Server-side sender binding for this mod's routed RPCs. ZRoutedRpc.RPC_RoutedRPC is private
    /// in 1.0.15, hence the name string. Reads the packet header (msg id, sender, target, target
    /// ZDO, method hash; RoutedRPCData.Deserialize's order, decompiled) and puts the read position
    /// back, so vanilla deserialises the same bytes. Honours __runOriginal and has no opinion about
    /// any packet it does not own; any failure lets the packet through as before.
    /// </summary>
    [HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
    public static class Patch_RoutedRpcSender
    {
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(ZRpc rpc, ZPackage pkg, bool __runOriginal)
        {
            if (!__runOriginal) return false;

            try
            {
                ZNet znet = ZNet.instance;
                if (znet == null || !znet.IsServer() || rpc == null || pkg == null) return true;

                long claimed;
                int hash;
                int pos = pkg.GetPos();
                try
                {
                    pkg.ReadLong();              // msg id
                    claimed = pkg.ReadLong();    // sender
                    pkg.ReadLong();              // target peer
                    pkg.ReadZDOID();             // target ZDO
                    hash = pkg.ReadInt();        // method
                }
                finally
                {
                    pkg.SetPos(pos);
                }

                if (!SenderGuard.IsGuarded(hash)) return true;

                List<ZNetPeer> peers = znet.GetPeers();
                for (int i = 0; i < peers.Count; i++)
                {
                    ZNetPeer peer = peers[i];
                    if (peer == null || !ReferenceEquals(peer.m_rpc, rpc)) continue;
                    if (peer.m_uid == claimed) return true;
                    SenderGuard.WarnOnce(peer.m_uid, claimed, hash);
                    return false;
                }

                return true;   // not a known peer's connection: vanilla's own checks apply
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"SenderGuard: header check failed, packet passed: {ex.Message}");
                return true;
            }
        }
    }
}
