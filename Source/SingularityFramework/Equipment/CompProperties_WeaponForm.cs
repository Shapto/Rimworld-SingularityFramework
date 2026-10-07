using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SingularityFramework.Equipment
{
    public class CompProperties_WeaponForm : CompProperties
    {
        /// <summary>
        /// The ThingDef this weapon turns into.
        /// </summary>
        public ThingDef switchTo;

        public string commandLabel;
        public string commandDescription;
        [NoTranslate] public string commandIconPath;
        public SoundDef switchSound;

        public CompProperties_WeaponForm() => compClass = typeof(CompWeaponForm);
    }

    /// <summary>
    /// Lets an equipped weapon switch into another form (sheathed and unsheathed, gun and blade, ...).
    /// The new form keeps the old one's material, quality, condition, biocoding and remaining charges.
    /// </summary>
    public class CompWeaponForm : ThingComp
    {
        private Texture2D cachedIcon;

        public CompProperties_WeaponForm Props => (CompProperties_WeaponForm)props;

        private Texture2D CommandIcon
        {
            get
            {
                if (cachedIcon == null) cachedIcon = Props.commandIconPath.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(Props.commandIconPath);
                return cachedIcon;
            }
        }

        /// <summary>
        /// The switch gizmo, shown while the weapon is equipped. Added by Patch_PawnEquipmentTracker_GetGizmos.
        /// </summary>
        public IEnumerable<Gizmo> GetFormGizmos(Pawn wielder)
        {
            if (Props.switchTo == null || !wielder.IsColonistPlayerControlled) yield break;

            yield return new Command_Action
            {
                defaultLabel = Props.commandLabel,
                defaultDesc = Props.commandDescription,
                icon = CommandIcon,
                action = () => SwitchForm(wielder)
            };
        }

        /// <summary>
        /// Replaces this weapon in the wielder's hands with its other form.
        /// </summary>
        public void SwitchForm(Pawn wielder)
        {
            ThingWithComps oldForm = parent;
            var newForm = (ThingWithComps)ThingMaker.MakeThing(Props.switchTo, oldForm.Stuff);

            CopyStateTo(oldForm, newForm);

            wielder.equipment.Remove(oldForm);
            oldForm.Destroy();
            wielder.equipment.AddEquipment(newForm);

            Props.switchSound?.PlayOneShot(new TargetInfo(wielder.Position, wielder.Map));
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
