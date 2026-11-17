using System;
using System.Collections.Generic;

namespace RavenIron.RagnaroksWrath.Net
{
    /// <summary>
    /// Carries titles from the server's TitleStore to every client's nameplate patch — the
    /// first real piece of client plumbing in this mod, kept deliberately tiny.
    ///
    /// WHY AN RPC AND NOT A ZDO KEY: a character ZDO is owned by its own client, and only the
    /// OWNER's writes replicate — a foreign key the server sets stays local and is stomped by
    /// the owner's next sync. The ZDO-position trap in CLAUDE.md, generalised. So the server
    /// broadcasts (playerID, title) pairs and every client keeps a cache the nameplate patch
    /// reads. GUID-prefixed name, because RPC names share one hash namespace with the base game
    /// and every mod.
    ///
    /// Registration is keyed on the ZRoutedRpc INSTANCE, not a "done" flag: the instance is
    /// per-world-session, and a stale flag from the previous world would leave the next one
    /// with no handler at all (docs/reference/VALHEIM-DEDICATED-SERVER-FACTS.md, Routed RPCs).
    /// Call EnsureRegistered from any per-tick path on both sides; it is a reference compare
    /// when nothing changed.
    /// </summary>
    public static class TitleSync
    {
        public const string RpcName = "com.raveniron.ragnarokswrath.title_set";

        /// <summary>Player → server: "wear this title" (or "auto", "none", or "" to just list).</summary>
        public const string ChooseRpc = "com.raveniron.ragnarokswrath.title_choose";

        /// <summary>Server → that one player: the answer, as a line of text for their chat or console.</summary>
        public const string ReplyRpc = "com.raveniron.ragnarokswrath.title_reply";

        private static readonly Dictionary<long, string> _cache = new Dictionary<long, string>(16);
        private static ZRoutedRpc _registeredOn;

        // Where the reply to the last request is printed: the chat box or console it was typed in.
        private static Terminal _replyTo;

        // One request per second per sender. Each accepted choice saves the title file, so an
        // unthrottled client could make the server write it as fast as it can send packets.
        private static readonly Dictionary<long, float> _lastRequest = new Dictionary<long, float>(8);
        private const float RequestCooldownSeconds = 1f;

        /// <summary>The local cache the nameplate patch reads. Never null entries.</summary>
        public static string TitleFor(long playerId)
            => _cache.TryGetValue(playerId, out string t) ? t : null;

        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, _registeredOn)) return;

            try
            {
                rpc.Register<long, string>(RpcName, RPC_TitleSet);
                rpc.Register<string>(ChooseRpc, RPC_Choose);
                rpc.Register<string>(ReplyRpc, RPC_Reply);
                _registeredOn = rpc;
                _cache.Clear();   // new world session, new truth; the server re-broadcasts
                _lastRequest.Clear();
            }
            catch (Exception ex)
            {
                // Same-name double-register throws; if another of our own instances got here
                // first that is fine, but say so once rather than silently owning no handler.
                RagnaroksWrath.Log.LogWarning($"TitleSync: register failed: {ex.Message}");
                _registeredOn = rpc;
            }
        }

        /// <summary>Server-side: tell everybody (and ourselves — Everybody dispatches locally
        /// too, which is what keeps a listen-server host's own cache warm).</summary>
        public static void Broadcast(long playerId, string title)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || playerId == 0) return;

            try
            {
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, playerId, title ?? "");
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"TitleSync: broadcast failed: {ex.Message}");
            }
        }

        /// <summary>
        /// A player's own console or chat: ask the server to change (or just show) their title.
        /// Players are not admins, so this cannot use the console's admin forward; it is its own
        /// RPC, and the server works out WHO from the connection it arrived on, never from
        /// anything in the packet. Returns false when there is no server to ask.
        /// </summary>
        public static bool RequestChoice(string choice, Terminal replyTo)
        {
            EnsureRegistered();
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ZNet.instance == null) return false;

            _replyTo = replyTo;
            try
            {
                // The target-less overload sends to the server (or handles it here on a listen host).
                rpc.InvokeRoutedRPC(ChooseRpc, choice ?? "");
                return true;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"TitleSync: title request failed: {ex.Message}");
                return false;
            }
        }

        private static void RPC_Choose(long sender, string choice)
        {
            ZNet znet = ZNet.instance;
            if (znet == null || !znet.IsServer()) return;   // only the server holds the titles
            if (!Core.TitleStore.IsLoaded) { Reply(sender, "titles are not loaded on the server yet; try again in a moment."); return; }

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_lastRequest.TryGetValue(sender, out float last) && now - last < RequestCooldownSeconds) return;
            _lastRequest[sender] = now;

            long playerId = PlayerIdOf(sender);
            if (playerId == 0)
            {
                Reply(sender, "your character is not known to the server yet; try again in a moment.");
                return;
            }

            string text = (choice ?? "").Trim();
            if (text.Length == 0)
            {
                Reply(sender, Core.TitleStore.Describe(playerId));
                return;
            }

            string before = Core.TitleStore.Shown(playerId);
            bool ok = Core.TitleStore.TryChoose(playerId, text, out string message);
            if (ok)
            {
                string after = Core.TitleStore.Shown(playerId);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    Broadcast(playerId, after ?? "");   // "" clears every nameplate cache
                RagnaroksWrath.Log.LogInfo($"TitleSync: player {playerId} chose '{text}', now wears '{after ?? "nothing"}'.");
            }
            Reply(sender, message);
        }

        /// <summary>
        /// The persistent player id behind a routed-RPC sender, taken from the server's own record
        /// of that connection's character. The sender id itself is bound to the connection by
        /// SenderGuard, so a client cannot speak for someone else. On a listen host the host's own
        /// request arrives with the host's id, which is no peer: that is the local player.
        /// </summary>
        private static long PlayerIdOf(long sender)
        {
            try
            {
                if (sender == ZNet.GetUID())
                {
                    Player local = Player.m_localPlayer;
                    return local != null ? local.GetPlayerID() : 0L;
                }

                ZNetPeer peer = ZNet.instance.GetPeer(sender);
                if (peer == null || peer.m_characterID.IsNone()) return 0L;
                ZDO zdo = ZDOMan.instance?.GetZDO(peer.m_characterID);
                return zdo != null ? zdo.GetLong(ZDOVars.s_playerID, 0L) : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        private static void Reply(long target, string text)
        {
            try { ZRoutedRpc.instance?.InvokeRoutedRPC(target, ReplyRpc, text ?? ""); }
            catch (Exception ex) { RagnaroksWrath.Log.LogWarning($"TitleSync: reply failed: {ex.Message}"); }
        }

        private static void RPC_Reply(long sender, string text)
        {
            if (!SenderGuard.FromServer(sender) || string.IsNullOrEmpty(text)) return;

            string line = "title: " + text;
            Terminal where = _replyTo != null ? _replyTo : (Terminal)Chat.instance;
            if (where != null) where.AddString(line);
            else Feedback.MessageFeed.ToLocalPlayer(line);
        }

        private static void RPC_TitleSet(long sender, long playerId, string title)
        {
            if (playerId == 0) return;
            // Only the server hands out titles. Before this, any client could broadcast one onto
            // everybody's nameplates, which mattered little until players could CHOOSE theirs.
            if (!SenderGuard.FromServer(sender)) return;

            if (string.IsNullOrEmpty(title)) _cache.Remove(playerId);
            else _cache[playerId] = title;
        }
    }
}
