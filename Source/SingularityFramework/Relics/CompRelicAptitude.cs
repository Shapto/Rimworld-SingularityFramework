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
        /// When false, unworthy pawns can still wield the relic; they just get the unworthy stat changes.
        /// </summary>
        public bool rejectsUnworthy = true;

        /// <summary>
        /// Changes to the weapon's own stats while held by a wielder with partial or no aptitude
        /// (e.g. MeleeWeapon_DamageMultiplier, or a mod's own stats like heat buildup).
        /// </summary>
        public List<StatModifier> partialStatFactors;
        public List<StatModifier> partialStatOffsets;
        public List<StatModifier> unworthyStatFactors;
        public List<StatModifier> unworthyStatOffsets;

        /// <summary>
        /// When set, aptitude comes from skills instead of the hidden roll: meeting every full requirement gives full aptitude,
        /// meeting every partial one gives partial. Aptitude then grows as the pawn's skills do.
        /// </summary>
        public List<SkillRequirement> fullAptitudeSkills;
        public List<SkillRequirement> partialAptitudeSkills;

        /// <summary>
        /// Given to an unworthy pawn when the relic rejects them. Both optional.
        /// </summary>
        public ThoughtDef rejectionThought;
        public HediffDef rejectionHediff;

        public CompProperties_RelicAptitude() => compClass = typeof(CompRelicAptitude);

        public string RelicKey(ThingDef parentDefinition) => relicKey.NullOrEmpty() ? parentDefinition.defName : relicKey;
    }



    /// <summary>
    /// Makes a weapon a relic: each pawn has an aptitude for it (from a hidden roll, guaranteed traits or genes, or skills), revealed the first time they try it.
    /// Unworthy pawns can be rejected; pawns with partial or no aptitude can wield it with weaker stats.
    /// </summary>
    public class CompRelicAptitude : ThingComp
    {
        public CompProperties_RelicAptitude Props => (CompProperties_RelicAptitude)props;

        public string RelicKey => Props.RelicKey(parent.def);

        private Pawn Wielder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            // For relics tested on draw, the switch ability already refused unworthy pawns; this catches every other way of getting it.
            AptitudeLevel aptitude = RelicAptitude.TestAndReveal(pawn, Props, parent.def);
            if (aptitude == AptitudeLevel.Unworthy && Props.rejectsUnworthy)
            {
                RelicAptitude.ApplyRejection(pawn, Props);
                GameComponent_RelicAptitude.Instance.QueueDrop(pawn, parent);
            }
        }

        public override float GetStatFactor(StatDef stat)
        {
            List<StatModifier> statFactors = StatListForWielder(Props.partialStatFactors, Props.unworthyStatFactors);
            return statFactors.NullOrEmpty() ? 1f : statFactors.GetStatFactorFromList(stat);
        }

        public override float GetStatOffset(StatDef stat)
        {
            List<StatModifier> statOffsets = StatListForWielder(Props.partialStatOffsets, Props.unworthyStatOffsets);
            return statOffsets.NullOrEmpty() ? 0f : statOffsets.GetStatOffsetFromList(stat);
        }

        /// <summary>
        /// The partial or unworthy list for the current wielder's aptitude. Null when nobody holds it or the wielder has full aptitude.
        /// </summary>
        private List<StatModifier> StatListForWielder(List<StatModifier> partialList, List<StatModifier> unworthyList)
        {
            Pawn wielder = Wielder;
            if (wielder == null) return null;

            AptitudeLevel aptitude = RelicAptitude.GetAptitude(wielder, Props, parent.def);
            if (aptitude == AptitudeLevel.Partial) return partialList;
            if (aptitude == AptitudeLevel.Unworthy) return unworthyList;
            return null;
        }

    }
}
