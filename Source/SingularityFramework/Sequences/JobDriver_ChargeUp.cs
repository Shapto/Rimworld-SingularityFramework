using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using RimWorld;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// The caster stands facing the target for the stage's charge-up time, then performs that stage's action.
    /// The stage number travels in job.count.
    /// </summary>
    public class JobDriver_ChargeUp : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompAbilityEffect_Chain chain = job.ability?.CompOfType<CompAbilityEffect_Chain>();
            int stage = chain?.ChargingStage ?? -1;
            ChainStageSettings stageSettings = chain?.SettingsForStage(stage);
            this.FailOn(() => job.ability == null);

            Toil chargeUp = Toils_General.Wait(stageSettings?.chargeUpTicks ?? 0, TargetIndex.A);
            if (stageSettings?.chargeUpSound != null) chargeUp.PlaySoundAtStart(stageSettings.chargeUpSound);
            yield return chargeUp;

            Toil performStage = ToilMaker.MakeToil("PerformChargedStage");
            performStage.defaultCompleteMode = ToilCompleteMode.Instant;
            performStage.initAction = () =>
            {
                Ability chargedAbility = job.ability;
                LocalTargetInfo chargedTarget = job.targetA;

                EndJobWith(JobCondition.Succeeded);

                foreach (CompAbilityEffect effectComp in chargedAbility.EffectComps)
                {
                    if (!(effectComp is IChainStageAction stageAction) || !stageAction.ActsOnStage(stage)) continue;
                    stageAction.PerformStageAction(chargedTarget);
                    return;
                }
            };
            yield return performStage;
        }
    }
}
