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
    /// in its ambient lists (base and alt-biome, never event lists) rolls ContestWildSpawnChance
    /// instead of its own, never lower. The value is raised IN PLACE and put back by the finalizer,
    /// which Harmony runs even when the original throws. A cloned SpawnData would do the same job
    /// but lose the object identity other spawn mods hang their per-spawner settings on.
    ///
    /// NEVER PAST THE CAP, AND WHY THAT TAKES A GATE. Vanilla 1.0.16 gates each attempt on
    /// `count + spawnedThisPass >= m_maxSpawned`, but sizes the group from `m_maxSpawned - count`
    /// alone, and `count` is a snapshot taken before the pass. So a CATCH-UP pass (several intervals
    /// due at once, as on arrival) can spawn a group, then another sized as if the first did not
    /// exist: cap 3 with groups of 1-2 reaches 4. Vanilla's own low chance makes that rare; 100%
    /// would make it routine. So the chance is raised only on a pass where vanilla will make
    /// exactly ONE attempt for that spawner, computed with vanilla's own key and expression. One
    /// attempt is one group of at most `m_maxSpawned - count`, which cannot pass the cap. Catch-up
    /// passes roll at vanilla's chance, and a spawner with no cap at all (0) is never raised.
    /// Found by the review of this change.
    ///
    /// A KNOWN SIDE EFFECT, vanilla's: `spawnedThisPass` is shared by every entry in the list, so a
    /// wildlife spawn early in a pass can stop a later spawner, hostiles included, for that
    /// interval. The war makes wildlife spawns more common, so it makes this more common too. It
    /// costs at most one interval per pass, and the in-game test counts hostile spawns to size it.
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
                                   DateTime currentTime, bool eventSpawners, string groupSalt,
                                   bool __runOriginal)
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

                ZDO zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
                if (zdo == null) return;

                // Vanilla skips a spawner whose biome this zone lacks before it rolls, so raising one
                // would change nothing, and the log line would claim an answer that never came. Found
                // the way vanilla's own Awake finds it: m_heightmap is private (rule 5).
                Heightmap hmap = Heightmap.FindHeightmap(__instance.transform.position);

                string wildlife = ModConfig.WildlifePrefabs.Value;
                for (int i = 0; i < spawners.Count; i++)
                {
                    SpawnSystem.SpawnData s = spawners[i];
                    if (s == null || !s.m_enabled || s.m_prefab == null) continue;
                    if (s.m_maxSpawned <= 0) continue;   // no cap to refill toward: raising would grow it forever
                    if (hmap != null && !hmap.HaveBiome(s.m_biome)) continue;
                    if (!ConsequenceMath.IsPassivePrefab(s.m_prefab.name, wildlife)) continue;
                    if (AttemptsDue(zdo, s, i + 1, groupSalt, currentTime) != 1) continue;   // see NEVER PAST THE CAP

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

        /// <summary>
        /// How many attempts vanilla's UpdateSpawnList will make for this spawner in this pass,
        /// with its own key and its own expression (decompiled 1.0.16, and identical in 1.0.15): the
        /// timestamp key is the group salt, the prefab name and the spawner's 1-based position in the
        /// list, counted over every entry, disabled ones included. Read before vanilla rewrites it,
        /// so it is the value vanilla is about to read.
        /// </summary>
        private static int AttemptsDue(ZDO zdo, SpawnSystem.SpawnData s, int position, string groupSalt, DateTime now)
        {
            int key = (groupSalt + s.m_prefab.name + position).GetStableHashCode();
            TimeSpan since = now - new DateTime(zdo.GetLong(key, 0L));
            return Mathf.Min((s.m_maxSpawned == 0) ? 1 : s.m_maxSpawned,
                             (int)(since.TotalSeconds / (double)s.m_spawnInterval));
        }

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
