using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    public class CompProperties_ChargedStrikes : CompProperties
    {
        public int chargesSpentPerAttack = 1;

        /// <summary>
        /// Damage multiplier when the attack had charge to spend, and when the weapon was empty.
        /// </summary>
        public float damageFactorWhenCharged = 1f;
        public float damageFactorWhenEmpty = 1f;

        /// <summary>
        /// Effects that happen only when a charged attack lands.
        /// </summary>
        public List<OnHitEffect> onHitEffectsWhenCharged;

        public CompProperties_ChargedStrikes() => compClass = typeof(CompChargedStrikes);
    }


    /// <summary>
    /// Makes a weapon spend charges from its vanilla reloadable comp on every attack.
    /// Charged attacks can deal more damage and trigger on-hit effects; empty attacks can be weaker.
    /// Needs a CompEquippableAbilityReloadable on the same weapon.
    /// </summary>
    public class CompChargedStrikes : ThingComp, IStrikeModifier
    {
        private bool chargedThisStrike;

        public CompProperties_ChargedStrikes Props => (CompProperties_ChargedStrikes)props;

        public bool TryJoinStrike(Pawn attacker, Verb attackVerb)
        {
            CompEquippableAbilityReloadable reloadableComp = parent.TryGetComp<CompEquippableAbilityReloadable>();
            if (reloadableComp == null) return false;

            chargedThisStrike = reloadableComp.RemainingCharges >= Props.chargesSpentPerAttack;
            if (chargedThisStrike)
            {
                reloadableComp.RemainingCharges -= Props.chargesSpentPerAttack;
                reloadableComp.UsedOnce();
            }
            return true;
        }

        public void ModifyDamage(Pawn attacker, Thing target, ref DamageInfo damageInfo)
        {
            float damageFactor = chargedThisStrike ? Props.damageFactorWhenCharged : Props.damageFactorWhenEmpty;
            damageInfo.SetAmount(damageInfo.Amount * damageFactor);
        }

        public void Notify_StrikeLanded(Pawn attacker, Thing target)
        {
            if (chargedThisStrike) OnHitEffect.TryApplyAll(Props.onHitEffectsWhenCharged, attacker, target);
        }

        public void Notify_StrikeMissed(Pawn attacker) { }
    }
}
