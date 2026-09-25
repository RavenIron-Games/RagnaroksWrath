using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using RavenIron.RagnaroksWrath.Config;
using RavenIron.RagnaroksWrath.Core;
using RavenIron.RagnaroksWrath.Net;

namespace RavenIron.RagnaroksWrath.Patches
{
    /// <summary>
    /// Task 13 phase D, the wild side of a spawn war: on contested ground the wild answers,
    /// refilling wildlife faster than vanilla would. Rebuilt 2026-09-25.
    ///
    /// WHY A PATCH. Until Valheim 1.0.7 this rode vanilla's own pheromone machinery: invisible
    /// SE_Stats "war horns" on the player, whose m_pheromoneSpawnChanceOverride
    /// SpawnSystem.UpdateSpawnList read within 100m of the carrier. 1.0.7 rewrote UpdateSpawnList
    /// and nothing in the game reads those fields any more (IL-verified on 1.0.12, 1.0.15 and
    /// 1.0.16), so from RW 0.27.0 the horns sounded and nothing answered. The chance roll is inline
    /// in UpdateSpawnList; there is no smaller seam.
    ///
    /// WHAT IT DOES. While the zone a SpawnSystem stands in is at war, every wildlife-list spawner
    /// in its base lists rolls ContestWildSpawnChance instead of its own, never lower. The value is
    /// raised IN PLACE and put back by the finalizer, which Harmony runs even when the original
    /// throws. A cloned SpawnData would do the same job but lose the object identity other spawn
    /// mods hang their per-spawner settings on. Only the CHANCE moves: vanilla's cap (a ZDO count
    /// over the zone's 5x5 sector snapshot) and its raw-cap group budget are untouched, so the war
    /// refills toward the stock population and never past it.
    ///
    /// WHERE IT RUNS. UpdateSpawning returns unless this machine owns the zone AND has a local
    /// player, so clients and listen hosts, never a dedicated server: the setting is read from the
    /// zone owner's own config, like every other consequence. Event spawners (raids) are never
    /// touched. AwayFromHome's rule holds: GetPlayersInZone and FindBaseSpawnPoint stay unpatched.
    ///
    /// Rule 1: a behaviour prefix at Priority.Low that honours __runOriginal and never skips the
    /// original. Rule 3: every body is wrapped; a war that throws must never take spawning down.
    /// </summary>
    [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawnList")]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_SpawnSystem_UpdateSpawnList
    {
        private const float CensusIntervalSeconds = 60f;
        private const float ErrorLogCooldown = 60f;

        private struct Raised
        {
            public SpawnSystem.SpawnData Spawner;
            public float Vanilla;
        }

        // Main thread only, and UpdateSpawnList never re-enters itself, so one list serves every call.
        private static readonly List<Raised> _raised = new List<Raised>(4);
        private static readonly List<ZDO> _census = new List<ZDO>();
        private static bool _answerLogged;
        private static float _nextCensus;
        private static float _nextErrorLog;

        private static void Prefix(SpawnSystem __instance, List<SpawnSystem.SpawnData> spawners,
                                   bool eventSpawners, bool __runOriginal)
        {
            try
            {
                Restore();   // never carry a raise from one pass into the next
                if (!__runOriginal) return;   // someone earlier already cancelled; no opinion
                if (eventSpawners || spawners == null || __instance == null) return;
                if (!ModConfig.EnableRivalry.Value) return;

                float warChance = ModConfig.ContestWildSpawnChance.Value;
                if (warChance <= 0f) return;

                ZoneKey zone = ZoneKey.FromWorldPos(__instance.transform.position);
                float war = ZoneSync.WarAt(zone);
                if (war <= 0f) return;

                string wildlife = ModConfig.WildlifePrefabs.Value;
                for (int i = 0; i < spawners.Count; i++)
                {
                    SpawnSystem.SpawnData s = spawners[i];
                    if (s == null || !s.m_enabled || s.m_prefab == null) continue;
                    if (!ConsequenceMath.IsPassivePrefab(s.m_prefab.name, wildlife)) continue;

                    float chance = ConsequenceMath.WarSpawnChance(s.m_spawnChance, war, warChance);
                    if (!(chance > s.m_spawnChance)) continue;

                    _raised.Add(new Raised { Spawner = s, Vanilla = s.m_spawnChance });
                    s.m_spawnChance = chance;
                }

                if (_raised.Count > 0) Report(zone, war);
            }
            catch (Exception ex)
            {
                Restore();
                if (Time.time >= _nextErrorLog)
                {
                    _nextErrorLog = Time.time + ErrorLogCooldown;
                    RagnaroksWrath.Log.LogWarning($"SpawnWar: pass failed, vanilla chances kept: {ex.Message}");
                }
            }
        }

        // Void on purpose: it observes, it does not change what vanilla throws.
        private static void Finalizer() => Restore();

        private static void Restore()
        {
            for (int i = _raised.Count - 1; i >= 0; i--)
            {
                Raised r = _raised[i];
                if (r.Spawner != null) r.Spawner.m_spawnChance = r.Vanilla;
            }
            _raised.Clear();
        }

        /// <summary>
        /// One Info line the first time the wild answers in a session, the proof the code ran at
        /// all. Under VerboseLogging, a census every minute of what vanilla's cap will count: the
        /// same snapshot UpdateSpawnList takes (ZDOs of the prefab in the zone's 5x5 sectors), read
        /// just before it takes it.
        /// </summary>
        private static void Report(ZoneKey zone, float war)
        {
            bool census = ModConfig.VerboseLogging.Value && Time.time >= _nextCensus;
            if (_answerLogged && !census) return;

            var c = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            if (census)
            {
                _nextCensus = Time.time + CensusIntervalSeconds;
                _census.Clear();
                ZDOMan.instance?.FindSectorObjects(zone.ToVector2s(), SimulationDistance.OriginalDistance, _census);
                sb.Append($"SpawnWar: war census in zone {zone}, intensity {war.ToString("0.0", c)} —");
            }
            else
            {
                sb.Append($"SpawnWar: the wild answers the war in zone {zone}, intensity {war.ToString("0.0", c)} —");
            }

            for (int i = 0; i < _raised.Count; i++)
            {
                SpawnSystem.SpawnData s = _raised[i].Spawner;
                sb.Append($" {s.m_prefab.name} {_raised[i].Vanilla.ToString("0.#", c)}% -> {s.m_spawnChance.ToString("0.#", c)}%");
                if (census)
                    sb.Append($", {SpawnSystem.GetNrOfZDOInstances(s.m_prefab, _census)} of cap {s.m_maxSpawned}");
                sb.Append(';');
            }

            if (census) _census.Clear();
            _answerLogged = true;
            RagnaroksWrath.Log.LogInfo(sb.ToString());
        }
    }
}
