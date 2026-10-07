using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    public enum AptitudeLevel
    {
        Unworthy,
        Partial,
        Full
    }

    public class CompProperties_RelicAptitude : CompProperties
    {
        /// <summary>
        /// Shared by every form of the same relic, so sheathed and unsheathed forms give the same aptitude. Defaults to the defName.
        /// </summary>
        public string relicKey;

        /// <summary>
        /// Chance of full aptitude, and of at least partial aptitude. Everyone else is unworthy.
        /// </summary>
        public float fullAptitudeChance = 0.1f;
        public float partialAptitudeChance = 0.4f;

        /// <summary>
        /// Pawns with any of these always have full aptitude ("tied to blood"). Use MayRequire in XML for DLC genes.
        /// </summary>
        public List<TraitDef> guaranteedTraits;
        public List<GeneDef> guaranteedGenes;

        /// <summary>
        /// When true, aptitude is tested when the relic is drawn (switch ability), not when it's equipped.
        /// </summary>
        public bool testedOnDraw;

        /// <summary>
        /// Changes to the weapon's own stats while held by a wielder with only partial aptitude (e.g. MeleeWeapon_DamageMultiplier, MeleeWeapon_CooldownMultiplier).
        /// </summary>
        public List<StatModifier> partialStatFactors;
        public List<StatModifier> partialStatOffsets;

        /// <summary>
        /// Given to an unworthy pawn when the relic rejects them. Both optional.
        /// </summary>
        public ThoughtDef rejectionThought;
        public HediffDef rejectionHediff;

        public CompProperties_RelicAptitude() => compClass = typeof(CompRelicAptitude);

        public string RelicKey(ThingDef parentDefinition) => relicKey.NullOrEmpty() ? parentDefinition.defName : relicKey;
    }



    /// <summary>
    /// Makes a weapon a relic: each pawn has a hidden aptitude for it, revealed the first time they try it.
    /// Unworthy pawns are rejected; pawns with partial aptitude wield it with weaker stats.
    /// </summary>
    public class CompRelicAptitude : ThingComp
    {
        public CompProperties_RelicAptitude Props => (CompProperties_RelicAptitude)props;

        public string RelicKey => Props.RelicKey(parent.def);

        private Pawn Wielder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        /// <summary>
        /// True while the weapon is held by a pawn whose aptitude for it is only partial.
        /// </summary>
        private bool HeldWithPartialAptitude
        {
            get
            {
                Pawn wielder = Wielder;
                return wielder != null && RelicAptitude.GetAptitude(wielder, Props, parent.def) == AptitudeLevel.Partial;
            }
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            if (RelicAptitude.TestAndReveal(pawn, Props, parent.def) == AptitudeLevel.Unworthy)
            {
                RelicAptitude.ApplyRejection(pawn, Props);
                GameComponent_RelicAptitude.Instance.QueueDrop(pawn, parent);
            }
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (Props.partialStatFactors.NullOrEmpty() || !HeldWithPartialAptitude) return 1f;
            return Props.partialStatFactors.GetStatFactorFromList(stat);
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (Props.partialStatOffsets.NullOrEmpty() || !HeldWithPartialAptitude) return 0f;
            return Props.partialStatOffsets.GetStatOffsetFromList(stat);
        }

    }
}
