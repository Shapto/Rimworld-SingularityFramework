using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Sequences
{
    public class CompProperties_AbilityChain : CompProperties_AbilityEffect
    {
        public int stageCount = 3;

        /// <summary>
        /// How long the player has to use the next stage before the chain resets. 300 ticks is 5 seconds.
        /// </summary>
        public int stageWindowTicks = 300;

        /// <summary>
        /// Cooldown after the final stage. Kept here instead of on the AbilityDef, because a vanilla cooldown refills charges.
        /// </summary>
        public int cooldownAfterFinalStageTicks = 1800;

        public CompProperties_AbilityChain() => compClass = typeof(CompAbilityEffect_Chain);
    }

    /// <summary>
    /// Turns one ability into a chain of stages: each cast does the current stage and moves to the next.
    /// The chain resets if the next stage isn't used in time, and goes on cooldown after the final stage.
    /// </summary>
    public class CompAbilityEffect_Chain : CompAbilityEffect
    {
        private int storedStage;
        private int chainExpiresAtTick = -1;
        private int cooldownEndsAtTick = -1;
        private int stageCastThisTick;
        private int lastCastTick = -1;

        public new CompProperties_AbilityChain Props => (CompProperties_AbilityChain)props;

        /// <summary>
        /// The stage the next cast will perform, from 0. Falls back to 0 once the window has run out.
        /// </summary>
        public int CurrentStage => Find.TickManager.TicksGame > chainExpiresAtTick ? 0 : storedStage;

        public bool IsFinalStage => CurrentStage == Props.stageCount - 1;

        /// <summary>
        /// The stage of the cast happening right now. Other comps on the same ability read this in their Apply,
        /// so it gives the same answer whether they run before or after the chain advances.
        /// </summary>
        public int StageBeingCast
        {
            get
            {
                if (lastCastTick == Find.TickManager.TicksGame) return stageCastThisTick;
                return CurrentStage;
            }
        }

        public bool IsFinalStageBeingCast => StageBeingCast == Props.stageCount - 1;


        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            stageCastThisTick = CurrentStage;
            lastCastTick = Find.TickManager.TicksGame;
            if (IsFinalStage)
            {
                storedStage = 0;
                chainExpiresAtTick = -1;
                cooldownEndsAtTick = Find.TickManager.TicksGame + Props.cooldownAfterFinalStageTicks;
                return;
            }

            storedStage = CurrentStage + 1;
            chainExpiresAtTick = Find.TickManager.TicksGame + Props.stageWindowTicks;
        }

        public override bool GizmoDisabled(out string reason)
        {
            int ticksLeft = cooldownEndsAtTick - Find.TickManager.TicksGame;
            if (ticksLeft > 0)
            {
                reason = "AbilityOnCooldown".Translate(ticksLeft.ToStringTicksToPeriod());
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedStage, "storedStage", 0);
            Scribe_Values.Look(ref chainExpiresAtTick, "chainExpiresAtTick", -1);
            Scribe_Values.Look(ref cooldownEndsAtTick, "cooldownEndsAtTick", -1);
        }
    }
}
