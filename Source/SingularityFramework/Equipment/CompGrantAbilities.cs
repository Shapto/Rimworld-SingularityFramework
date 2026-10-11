using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Equipment
{
    public class CompProperties_GrantAbilities : CompProperties
    {
        public List<AbilityDef> abilities;

        public CompProperties_GrantAbilities()
        {
            compClass = typeof(CompGrantAbilities);
        }
    }

    /// <summary>
    /// Gives the wielder any number of abilities while this weapon is equipped, and takes them away when it's unequipped.
    /// The abilities live on the pawn, so each one saves correctly, unlike several CompEquippableAbility comps on one weapon,
    /// which overwrite each other's saved data.
    /// </summary>
    public class CompGrantAbilities : ThingComp
    {
        public CompProperties_GrantAbilities Props => (CompProperties_GrantAbilities)props;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn.abilities == null || Props.abilities == null) return;

            foreach (AbilityDef abilityDefinition in Props.abilities)
            {
                if (pawn.abilities.GetAbility(abilityDefinition) == null) pawn.abilities.GainAbility(abilityDefinition);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (pawn.abilities == null || Props.abilities == null) return;

            foreach (AbilityDef abilityDefinition in Props.abilities)
                pawn.abilities.RemoveAbility(abilityDefinition);
        }
    }
}
