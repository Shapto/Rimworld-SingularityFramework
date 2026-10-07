using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Geometry
{
    /// <summary>
    /// Everything a line strike needs. The caller fills it in; the framework never decides numbers.
    /// </summary>
    public class LineStrikeSettings
    {
        public DamageDef damageDefinition;
        public float damageAmount;
        public float armorPenetration;
        public int maximumPawnsHit = int.MaxValue;
        public float damageFalloffPerHit = 1f;
        public bool hitsDownedPawns;
        public int maximumWallsPierced;
        public bool destroysPiercedWalls;
        public bool piercesNaturalRock = true;
    }

    /// <summary>
    /// A strike along a straight line of cells. Can be used all at once (a beam), or one cell at a time (a dash).
    /// </summary>
    public class LineStrike
    {
        private readonly Pawn attacker;
        private readonly LineStrikeSettings settings;
        private readonly HashSet<Thing> alreadyHit = new HashSet<Thing>();
        private float currentDamage;
        private int pawnsHit;
        private int wallsPierced;

        public List<Vector3> impactPoints = new List<Vector3>();

        public bool Finished => pawnsHit >= settings.maximumPawnsHit;

        public LineStrike(Pawn attacker, LineStrikeSettings settings)
        {
            this.attacker = attacker;
            this.settings = settings;
            currentDamage = settings.damageAmount;
        }

        /// <summary>
        /// The cells from one point to another, in order, without the starting cell.
        /// </summary>
        public static List<IntVec3> CellsOnLine(IntVec3 startCell, IntVec3 endCell)
        {
            return GenSight.PointsOnLineOfSight(startCell, endCell)
                .Where(cell => cell != startCell)
                .ToList();
        }

        /// <summary>
        /// True if a pawn could travel the whole line: every cell walkable, no walls or closed doors in the way.
        /// </summary>
        public static bool IsLineWalkable(IntVec3 startCell, IntVec3 endCell, Map map, LineStrikeSettings settings)
        {
            int blockersOnLine = 0;
            foreach (IntVec3 cell in CellsOnLine(startCell, endCell))
            {
                if (!cell.InBounds(map)) return false;

                Building blocker = GetBlocker(cell, map);
                if (blocker != null)
                {
                    if (!CanPierce(blocker, settings)) return false;
                    blockersOnLine++;
                    if (blockersOnLine > settings.maximumWallsPierced) return false;
                    continue;
                }

                if (!cell.Walkable(map)) return false;
            }
            return true;
        }

        /// <summary>
        /// The building blocking this cell, if any: a full wall or a closed door. Null if the cell is open.
        /// </summary>
        public static Building GetBlocker(IntVec3 cell, Map map)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice == null) return null;
            if (edifice is Building_Door door) return door.Open ? null : door;
            return edifice.def.Fillage == FillCategory.Full ? edifice : null;
        }

        /// <summary>
        /// True if the strike is allowed to go through this blocker at all.
        /// </summary>
        public static bool CanPierce(Building blocker, LineStrikeSettings settings)
        {
            return settings.piercesNaturalRock || !blocker.def.building.isNaturalRock;
        }

        /// <summary>
        /// Damages every eligible pawn standing in the cell. Each pawn is hit at most once per strike.
        /// </summary>
        public bool HitCell(IntVec3 cell, LocalTargetInfo mainTarget)
        {
            Map map = attacker.Map;
            if (map == null || settings.damageDefinition == null) return false;

            float hitAngle = (cell - attacker.Position).AngleFlat;
            ThingDef weaponDefinition = attacker.equipment?.Primary?.def;

            Building blocker = GetBlocker(cell, map);
            if (blocker != null)
            {
                if (!CanPierce(blocker, settings) || wallsPierced >= settings.maximumWallsPierced) return false;
                wallsPierced++;

                if (settings.destroysPiercedWalls) blocker.Destroy(DestroyMode.KillFinalize);
                else blocker.TakeDamage(new DamageInfo(settings.damageDefinition, currentDamage, settings.armorPenetration, hitAngle, attacker, null, weaponDefinition));

                impactPoints.Add(cell.ToVector3Shifted());
                currentDamage *= settings.damageFalloffPerHit;

                if (!blocker.Destroyed) return false;
            }

            foreach (Thing thing in cell.GetThingList(map).ToList())
            {
                if (!(thing is Pawn hitPawn) || hitPawn == attacker || hitPawn.Dead) continue;
                if (hitPawn.Downed && !settings.hitsDownedPawns && hitPawn != mainTarget.Thing) continue;
                if (!alreadyHit.Add(hitPawn)) continue;

                var damageInfo = new DamageInfo(settings.damageDefinition, currentDamage, settings.armorPenetration, hitAngle, attacker, null, weaponDefinition);
                hitPawn.TakeDamage(damageInfo);
                impactPoints.Add(hitPawn.DrawPos);

                pawnsHit++;
                currentDamage *= settings.damageFalloffPerHit;
                if (Finished) return true;
            }
            return true;
        }

        /// <summary>
        /// Hits the whole line at once, like a beam.
        /// </summary>
        public void HitWholeLine(IntVec3 startCell, IntVec3 endCell, LocalTargetInfo mainTarget)
        {
            foreach (IntVec3 cell in CellsOnLine(startCell, endCell))
            {
                if (!HitCell(cell, mainTarget) || Finished) return;
            }
        }
    }
}
