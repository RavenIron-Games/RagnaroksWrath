using System;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using UnityEngine;

namespace RavenIron.RagnaroksWrath.Net
{
    /// <summary>
    /// Server → every player: "lightning struck HERE". Added 2026-11-17 with realistic lightning.
    ///
    /// The server decides a strike (when, where, which tree it takes, whether fire may catch); it
    /// cannot draw one or hurt anyone, because a dedicated server renders nothing and a player's
    /// character is owned by that player's game. So it sends the strike point, the height of
    /// what it hit and a seed, and each player's game does the rest:
    ///
    ///  - the damage, first and on its own (gameplay): the local player, if within
    ///    LightningHurtRadiusMeters, takes lightning damage on their own character, which is the
    ///    one place vanilla applies it. Read from the synced config, so the server's numbers count.
    ///  - the picture and the sound (cosmetic, rule 3): handed to <see cref="Visuals.LightningBolts"/>
    ///    inside its own try/catch, so a visual that throws can never cost anyone the damage.
    ///
    /// The seed makes every player see the same bolt. The packet is ~30 bytes; storms throw a
    /// few a minute at most.
    /// </summary>
    public static class LightningSync
    {
        public const string RpcName = "com.raveniron.ragnarokswrath.lightning";

        private static ZRoutedRpc _registeredOn;
        private static bool _damageWarned;

        public static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, _registeredOn)) return;
            try
            {
                rpc.Register<Vector3, float, int>(RpcName, RPC_Strike);
                _registeredOn = rpc;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"LightningSync: register failed: {ex.Message}");
                _registeredOn = rpc;
            }
        }

        /// <summary>Authority: tell everyone a bolt landed. Everybody includes a listen host itself.</summary>
        public static void Broadcast(Vector3 ground, float top, int seed)
        {
            EnsureRegistered();
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null) return;
            try { rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, ground, top, seed); }
            catch (Exception ex) { RagnaroksWrath.Log.LogWarning($"LightningSync: broadcast failed: {ex.Message}"); }
        }

        private static void RPC_Strike(long sender, Vector3 ground, float top, int seed)
        {
            // Only the server throws lightning; another player cannot strike someone down.
            if (!SenderGuard.FromServer(sender)) return;
            if (float.IsNaN(ground.x) || float.IsNaN(ground.y) || float.IsNaN(ground.z) || float.IsNaN(top)) return;

            HurtLocalPlayer(ground);

            try { Visuals.LightningBolts.Show(ground, top, seed); }
            catch (Exception ex) { RagnaroksWrath.Log.LogWarning($"LightningSync: could not draw a strike: {ex.Message}"); }
        }

        /// <summary>
        /// Lightning damage to THIS game's player, by distance on the ground from the strike. Vanilla
        /// applies resistances, armour, god mode and the lightning damage type itself; a death by
        /// lightning is a real death. A player swimming or far away is simply out of range.
        /// </summary>
        private static void HurtLocalPlayer(Vector3 ground)
        {
            try
            {
                if (!ModConfig.LightningHurtsPlayers.Value) return;
                Player player = Player.m_localPlayer;
                if (player == null || player.IsDead()) return;

                Vector3 at = player.transform.position;
                float dx = at.x - ground.x, dz = at.z - ground.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float damage = LightningStrike.DamageAt(distance, ModConfig.LightningHurtRadiusMeters.Value, ModConfig.LightningDamage.Value);
                if (damage <= 0f) return;

                Vector3 away = new Vector3(dx, 0.5f, dz);
                if (away.sqrMagnitude < 0.01f) away = Vector3.up;

                var hit = new HitData();
                hit.m_damage.m_lightning = damage;
                hit.m_point = at + Vector3.up;
                hit.m_dir = away.normalized;
                // The blast throws you off your feet close in, a jolt farther out.
                hit.m_pushForce = 30f * (damage / Mathf.Max(1f, ModConfig.LightningDamage.Value));
                hit.m_staggerMultiplier = 3f;
                player.Damage(hit);

                RagnaroksWrath.Log.LogInfo($"Lightning: struck {distance:F1} m from {player.GetPlayerName()}, {damage:F0} lightning damage before armour.");
            }
            catch (Exception ex)
            {
                if (_damageWarned) return;
                _damageWarned = true;
                RagnaroksWrath.Log.LogWarning($"LightningSync: could not apply lightning damage: {ex.Message}");
            }
        }
    }
}
