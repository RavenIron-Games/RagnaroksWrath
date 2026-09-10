using System;
using System.Collections.Generic;
using UnityEngine;

namespace RavenIron.RagnaroksWrath.Core
{
    /// <summary>
    /// One question, asked by two systems: is this position near anything player-built?
    /// "Player-built" means any ZDO carrying a builder id — pieces and plants alike, the
    /// same `s_creator` fact RivalrySystem's tending verified at source.
    ///
    /// Lightning asks it as the bolt standoff, and fails CLOSED (an uncheckable world
    /// means the bolt is lost, never risked). The storm scheduler asks it to anchor
    /// storms on players in the wild, and fails OPEN (a storm still does real work
    /// without lightning — wind, plague carriage, war escalation — so an uncheckable
    /// world must not silently kill the weather). The caller states its failure answer
    /// for exactly that reason; there is no safe shared default.
    ///
    /// Scans the 3x3 sectors around the position via vanilla's own index
    /// (`ZDOMan.FindSectorObjects`, public, decompile-verified 2026-08-27), which bounds
    /// an honest radius at 64m — the config clamps agree. Distances are XZ-planar (the
    /// 0.22.3 lesson: vertical distance is meaningless for "near this ground").
    /// </summary>
    public static class Homestead
    {
        public static bool IsNearPlayerBuilt(Vector3 pos, float radius, List<ZDO> scratch,
                                             bool resultWhenUncheckable)
        {
            if (radius <= 0f) return false;

            try
            {
                ZDOMan man = ZDOMan.instance;
                if (man == null) return resultWhenUncheckable;

                scratch.Clear();
                // Valheim 1.0.7 replaced the (sector, area, distantArea) integers with a
                // SimulationDistance. `classic: true` is load-bearing, not decoration: without
                // it the near ring is filtered by ZonesWithinRadius (a circle, not our square)
                // AND the distant loop runs from 1 instead of near+1, sweeping the same ring a
                // second time. With it, (near 1, far 0) is exactly the old (area 1, distant 0):
                // one full 3x3 block of sectors, nothing beyond.
                man.FindSectorObjects(ZoneKey.FromWorldPos(pos).ToVector2s(),
                                      new SimulationDistance(1, 0, classic: true), scratch);

                float sqr = radius * radius;
                for (int i = 0; i < scratch.Count; i++)
                {
                    ZDO zdo = scratch[i];
                    if (zdo == null || !zdo.IsValid()) continue;
                    if (zdo.GetLong(ZDOVars.s_creator, 0L) == 0) continue;

                    Vector3 p = zdo.GetPosition();
                    float dx = p.x - pos.x;
                    float dz = p.z - pos.z;
                    if (dx * dx + dz * dz <= sqr) return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                RagnaroksWrath.Log.LogWarning($"[Homestead] player-built scan failed ({ex.Message}).");
                return resultWhenUncheckable;
            }
        }
    }
}
