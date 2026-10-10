using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Sequences
{
    public class CompProperties_AbilityJumpSlam : CompProperties_AbilityEffect
    {
        /// <summary>
        /// The flyer ThingDef, with thingClass PawnFlyer_LandingSlam and a ModExtension_LandingSlam.
        /// </summary>
        public ThingDef flyerDefinition;

        public CompProperties_AbilityJumpSlam() => compClass = typeof(CompAbilityEffect_JumpSlam);
    }

    /// <summary>
    /// Jumps the caster to the target cell and slams on landing.
    /// In a chained ability, only acts on the final stage.
    /// </summary>
    public class CompAbilityEffect_JumpSlam : CompAbilityEffect
    {
        public new CompProperties_AbilityJumpSlam Props => (CompProperties_AbilityJumpSlam)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            CompAbilityEffect_Chain chain = parent.CompOfType<CompAbilityEffect_Chain>();
            if (chain != null && !chain.IsFinalStage) return true;

            return JumpUtility.ValidJumpTarget(parent.pawn, parent.pawn.Map, target.Cell);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            CompAbilityEffect_Chain chain = parent.CompOfType<CompAbilityEffect_Chain>();
            if (chain != null && !chain.IsFinalStageBeingCast) return;

            Pawn caster = parent.pawn;
            Map map = caster.Map;
            if (map == null || Props.flyerDefinition == null) return;

            PawnFlyer flyer = PawnFlyer.MakeFlyer(Props.flyerDefinition, caster, target.Cell, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, caster.Position, map);
        }
    }
}
