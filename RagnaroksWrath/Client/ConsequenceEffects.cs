using System;
using System.Collections.Generic;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Net;

namespace RavenIron.RagnaroksWrath.Client
{
    /// <summary>
    /// Task 12's creature half, wildlife side: in plagued zones, passive animals sicken —
    /// a code-built SE_Stats (task 11's no-asset pattern) that SLOWS them, visibly, the
    /// staggering deer of the design conversation. Silent per-creature by the mixed-voice
    /// rule; the zone announced itself once, server-side.
    ///
    /// Runs on any machine with a local player, acts only on characters whose ZDOs THIS
    /// client owns — the ownership check is what stops two present players double-dosing
    /// the same deer. The effect carries a short TTL and is re-applied while the ground
    /// stays plagued, so a deer that escapes the zone (or a player who leaves) recovers by
    /// simple expiry — no removal bookkeeping to rot.
    ///
    /// The lethal edge from the spec ("may kill a starving deer eventually") is NOT built:
    /// SE_Stats' health-over-time path only heals (decompile-verified — negative values
    /// never arm the ticker), so sickness is slow-only until a deliberate damage mechanism
    /// earns its own pass. Recorded in the backlog rather than faked here.
    /// </summary>
    public class ConsequenceEffects : MonoBehaviour
    {
        private const float UpdateInterval = 2f;
        private const float SickTtlSeconds = 30f;
        private const float ReachMeters = 48f;
        private const float ErrorLogCooldown = 60f;

        private float _nextUpdate;
        private float _nextErrorLog;

        private SE_Stats _sickTemplate;
        private int _sickHash;

        // Phase D, the wild side, lives in Patches/Patch_SpawnWar.cs since 2026-09-25. It used to
        // be invisible pheromone "war horns" on the local player, and Valheim 1.0.7 stopped
        // reading the pheromone spawn fields, so the horns went silent. What stays here is the
        // client-side proof that this machine SAW the war at all.
        private bool _warSeenLogged;
        private float _nextWarWatch;

        private void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + UpdateInterval;

            try
            {
                // Arm the relic wire HERE, not (only) on the nameplate path: a solo
                // client renders no other player's plate, so that postfix never runs
                // alone — which is exactly how the first monument's placement was lost
                // TWICE. This Update runs on every rendering client, solo included.
                Net.RelicSync.EnsureRegistered();
                Net.VersionSync.EnsureRegistered();

                Player player = Player.m_localPlayer;
                if (player == null) return;

                Vector3 origin = player.transform.position;

                // Task 12's sickness sweep, behind its own toggles — the war watch below
                // is rivalry's and deliberately NOT gated on the sickness switch.
                if (ModConfig.EnableConsequence.Value && ModConfig.ConsequenceSicken.Value)
                {
                    string passiveList = ModConfig.WildlifePrefabs.Value;
                    float threshold = ModConfig.SickenPlagueThreshold.Value;

                    List<Character> characters = Character.GetAllCharacters();
                    for (int i = 0; i < characters.Count; i++)
                    {
                        Character c = characters[i];
                        if (c == null || c.IsDead() || c.IsPlayer()) continue;
                        if (Vector3.Distance(c.transform.position, origin) > ReachMeters) continue;
                        if (!ConsequenceMath.IsPassivePrefab(c.name, passiveList)) continue;

                        // Only the owner doses; and only on ground that is actually plagued
                        // UNDER THE ANIMAL, not under the player — a deer at the zone border
                        // sickens by where it stands.
                        ZNetView nview = c.GetComponent<ZNetView>();
                        if (nview == null || !nview.IsValid() || !nview.IsOwner()) continue;

                        float plague = ZoneSync.StateAt(ZoneKey.FromWorldPos(c.transform.position)).Plague;
                        if (!ConsequenceMath.SickensWildlife(plague, threshold)) continue;

                        Dose(c);
                    }
                }

                WatchWar(origin);
            }
            catch (Exception ex)
            {
                if (Time.time >= _nextErrorLog)
                {
                    _nextErrorLog = Time.time + ErrorLogCooldown;
                    RagnaroksWrath.Log.LogWarning($"ConsequenceEffects: pass failed: {ex.Message}");
                }
            }
        }

        private void Dose(Character creature)
        {
            SEMan seman = creature.GetSEMan();
            if (seman == null) return;

            if (_sickTemplate == null)
            {
                _sickTemplate = ScriptableObject.CreateInstance<SE_Stats>();
                _sickTemplate.name = "RW_Blightsick";        // NameHash reads the OBJECT name
                _sickTemplate.m_name = "Blightsick";
                _sickTemplate.m_tooltip = "The blight is in this creature.";
                _sickTemplate.m_ttl = SickTtlSeconds;        // expiry IS the cure
                _sickTemplate.m_speedModifier = -Mathf.Clamp01(ModConfig.SickenSpeedPenalty.Value);
                _sickHash = _sickTemplate.NameHash();
            }

            // One call covers both cases (decompile-verified): already sick -> ResetTime
            // refreshes the TTL; not yet sick -> the instance overload clones the template.
            // A still-exposed animal stays continuously sick; expiry cures the escapee.
            seman.AddStatusEffect(_sickTemplate, resetTime: true);
        }

        /// <summary>
        /// Once per session, the line that proves this client SAW a war under its player: server
        /// war state -> ring push -> this cache. The wild's answer itself is Patch_SpawnWar's,
        /// and it logs its own proof the first time it raises a chance.
        /// </summary>
        private void WatchWar(Vector3 origin)
        {
            if (_warSeenLogged || !ModConfig.EnableRivalry.Value) return;
            if (Time.time < _nextWarWatch) return;
            _nextWarWatch = Time.time + 5f;

            float war = ZoneSync.WarAt(ZoneKey.FromWorldPos(origin));
            if (war <= 0f) return;

            _warSeenLogged = true;
            RagnaroksWrath.Log.LogInfo($"ConsequenceEffects: war intensity {war:F1} underfoot.");
        }
    }
}
