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
            int stage = job.count;
            ChainStageSettings stageSettings = job.ability?.CompOfType<CompAbilityEffect_Chain>()?.SettingsForStage(stage);
            this.FailOn(() => job.ability == null);

            Toil chargeUp = Toils_General.Wait(stageSettings?.chargeUpTicks ?? 0, TargetIndex.A);
            if (stageSettings?.chargeUpSound != null) chargeUp.PlaySoundAtStart(stageSettings.chargeUpSound);
            yield return chargeUp;

            Toil performStage = ToilMaker.MakeToil("PerformChargedStage");
            performStage.defaultCompleteMode = ToilCompleteMode.Instant;
            performStage.initAction = () =>
            {
                foreach (CompAbilityEffect effectComp in job.ability.EffectComps)
                {
                    if (!(effectComp is IChainStageAction stageAction) || !stageAction.ActsOnStage(stage)) continue;
                    stageAction.PerformStageAction(job.targetA);
                    return;
                }
            };
            yield return performStage;
        }
    }
}
