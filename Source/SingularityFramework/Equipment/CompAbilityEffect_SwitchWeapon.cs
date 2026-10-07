using RimWorld;
using SingularityFramework.Relics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Equipment
{
    public class CompProperties_AbilitySwitchWeapon : CompProperties_AbilityEffect
    {
        /// <summary>
        /// The ThingDef the caster's weapon turns into.
        /// </summary>
        public ThingDef switchTo;

        public CompProperties_AbilitySwitchWeapon() => compClass = typeof(CompAbilityEffect_SwitchWeapon);
    }

    /// <summary>
    /// Replaces the caster's weapon with another form (sheathed and unsheathed, ...).
    /// The new form keeps the old one's material, quality, condition, biocoding and remaining charges.
    /// Draw time and sound come from the ability's verb (warmupTime, soundCast).
    /// </summary>
    public class CompAbilityEffect_SwitchWeapon : CompAbilityEffect
    {
        public new CompProperties_AbilitySwitchWeapon Props => (CompProperties_AbilitySwitchWeapon)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            ThingWithComps oldForm = caster?.equipment?.Primary;
            if (oldForm == null || Props.switchTo == null) return;

            CompProperties_RelicAptitude aptitudeProperties = Props.switchTo.GetCompProperties<CompProperties_RelicAptitude>();
            if (aptitudeProperties != null && aptitudeProperties.testedOnDraw)
            {
                AptitudeLevel aptitude = RelicAptitude.TestAndReveal(caster, aptitudeProperties, Props.switchTo);
                if (aptitude == AptitudeLevel.Unworthy)
                {
                    RelicAptitude.ApplyRejection(caster, aptitudeProperties);
                    return;
                }
            }

            var newForm = (ThingWithComps)ThingMaker.MakeThing(Props.switchTo, oldForm.Stuff);
            CopyStateTo(oldForm, newForm);

            caster.equipment.Remove(oldForm);
            oldForm.Destroy();
            caster.equipment.AddEquipment(newForm);
        }

        private static void CopyStateTo(ThingWithComps oldForm, ThingWithComps newForm)
        {
            if (oldForm.TryGetQuality(out QualityCategory quality)) newForm.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);

            float healthFraction = oldForm.HitPoints / (float)oldForm.MaxHitPoints;
            newForm.HitPoints = Mathf.Max(1, Mathf.RoundToInt(newForm.MaxHitPoints * healthFraction));

            CompBiocodable oldBiocode = oldForm.TryGetComp<CompBiocodable>();
            CompBiocodable newBiocode = newForm.TryGetComp<CompBiocodable>();
            if (oldBiocode != null && newBiocode != null && oldBiocode.Biocoded) newBiocode.CodeFor(oldBiocode.CodedPawn);

            CompEquippableAbilityReloadable oldAmmo = oldForm.TryGetComp<CompEquippableAbilityReloadable>();
            CompEquippableAbilityReloadable newAmmo = newForm.TryGetComp<CompEquippableAbilityReloadable>();
            if (oldAmmo != null && newAmmo != null) newAmmo.RemainingCharges = Mathf.Min(oldAmmo.RemainingCharges, newAmmo.MaxCharges);
        }
    }
}
