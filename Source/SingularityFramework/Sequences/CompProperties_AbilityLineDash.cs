using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using SingularityFramework.Geometry;

namespace SingularityFramework.Sequences
{
    public class CompProperties_AbilityLineDash : CompProperties_AbilityEffect
    {
        public LineStrikeSettings strikeSettings = new LineStrikeSettings();

        /// <summary>
        /// Dash speed in cells per tick. 0.5 crosses 10 cells in 20 ticks (a third of a second).
        /// </summary>
        public float cellsPerTick = 0.5f;

        public JobDef dashJob;

        public CompProperties_AbilityLineDash() => compClass = typeof(CompAbilityEffect_LineDash);
    }

    /// <summary>
    /// Dashes the caster in a straight line to the target cell, damaging what's on the way.
    /// In a chained ability, does nothing on the final stage, so the finisher can take over.
    /// </summary>
    public class CompAbilityEffect_LineDash : CompAbilityEffect
    {
        public new CompProperties_AbilityLineDash Props => (CompProperties_AbilityLineDash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            CompAbilityEffect_Chain chain = parent.CompOfType<CompAbilityEffect_Chain>();
            if (chain != null && chain.IsFinalStage) return;

            Pawn caster = parent.pawn;
            Job dashJob = JobMaker.MakeJob(Props.dashJob, target.Cell);
            dashJob.ability = parent;
            caster.jobs.StartJob(dashJob, JobCondition.InterruptForced);
        }
    }
}
