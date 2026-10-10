using RimWorld;
using SingularityFramework.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// Slides the pawn along a straight line without pathing. The pawn really occupies each cell as it passes,
    /// so it stays hittable, it damages what it meets, and stops early if a wall survives being hit.
    /// </summary>
    public class JobDriver_LineDash : JobDriver
    {
        private IntVec3 startCell;
        private float progress;
        private int cellsEntered;

        private List<IntVec3> lineCells;
        private LineStrike lineStrike;

        private CompProperties_AbilityLineDash DashProps => job.ability?.CompOfType<CompAbilityEffect_LineDash>()?.Props;

        private int ticksElapsed;

        /// <summary>
        /// Where the pawn should be drawn this frame
        /// </summary>
        public Vector3 CurrentDrawPosition
        {
            get
            {
                Vector3 startPosition = startCell.ToVector3Shifted();
                Vector3 endPosition = job.targetA.Cell.ToVector3Shifted();
                Vector3 drawPosition = Vector3.Lerp(startPosition, endPosition, progress);
                drawPosition.y = pawn.Drawer.DrawPos.y;
                return drawPosition;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var dashToil = new Toil();
            dashToil.AddFinishAction(() => pawn.Drawer.tweener.ResetTweenedPosToRoot());
            dashToil.initAction = () =>
            {
                if (cellsEntered == 0 && progress == 0f) startCell = pawn.Position;
                pawn.pather.StopDead();
            };
            dashToil.tickAction = DashTick;
            dashToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return dashToil;
        }

        private void DashTick()
        {
            CompProperties_AbilityLineDash dashProps = DashProps;
            if (dashProps == null)
            {
                EndJobWith(JobCondition.Errored);
                return;
            }

            if (lineCells == null) lineCells = LineStrike.CellsOnLine(startCell, job.targetA.Cell);
            if (lineStrike == null) lineStrike = new LineStrike(pawn, dashProps.strikeSettings);
            if (lineCells.Count == 0)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            // Progress along the line follows an ease-out curve over a fixed duration: fast off the line, slowing into the landing.
            float totalTicks = Mathf.Max(1f, lineCells.Count / Mathf.Max(0.01f, dashProps.cellsPerTick));
            ticksElapsed++;
            float timeFraction = Mathf.Clamp01(ticksElapsed / totalTicks);
            progress = 1f - Mathf.Pow(1f - timeFraction, Mathf.Max(1f, dashProps.easingPower));

            int cellsReached = Mathf.Min(lineCells.Count, Mathf.FloorToInt(progress * lineCells.Count));
            while (cellsEntered < cellsReached)
            {
                IntVec3 nextCell = lineCells[cellsEntered];
                if (!lineStrike.HitCell(nextCell, job.targetA))
                {
                    // A surviving wall: stop in the last cell reached.
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.Position = nextCell;
                pawn.Notify_Teleported(false, false);
                cellsEntered++;
            }

            SpawnTrailFleck(dashProps);

            if (timeFraction >= 1f) EndJobWith(JobCondition.Succeeded);
        }

        /// <summary>
        /// Leaves a puff behind the dashing pawn every few ticks, drifting off in a random direction.
        /// </summary>
        private void SpawnTrailFleck(CompProperties_AbilityLineDash dashProps)
        {
            if (dashProps.trailFleck == null || pawn.Map == null) return;
            if (ticksElapsed % Mathf.Max(1, dashProps.trailFleckIntervalTicks) != 0) return;

            FleckCreationData fleckData = FleckMaker.GetDataStatic(CurrentDrawPosition, pawn.Map, dashProps.trailFleck, dashProps.trailFleckScale.RandomInRange);
            fleckData.velocityAngle = Rand.Range(0f, 360f);
            fleckData.velocitySpeed = Rand.Range(0.2f, 0.6f);
            pawn.Map.flecks.CreateFleck(fleckData);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startCell, "startCell");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref cellsEntered, "cellsEntered");
            Scribe_Values.Look(ref ticksElapsed, "ticksElapsed");
        }
    }
}
