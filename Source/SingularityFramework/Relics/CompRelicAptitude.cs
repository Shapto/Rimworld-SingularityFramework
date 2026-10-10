using RimWorld;
using SingularityFramework.Equipment;
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
        /// When true, the switch ability tests aptitude before drawing, so an unworthy pawn's draw fails instead of the relic dropping afterwards.
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
        /// When set, aptitude comes from these requirements instead of the hidden roll, and changes live as the pawn does:
        /// meeting every full requirement gives full aptitude, meeting every partial one gives partial.
        /// </summary>
        public List<AptitudeRequirement> fullAptitudeRequirements;
        public List<AptitudeRequirement> partialAptitudeRequirements;

        /// <summary>
        /// True when aptitude comes from requirements instead of the hidden roll.
        /// </summary>
        public bool UsesRequirementAptitude => !fullAptitudeRequirements.NullOrEmpty() || !partialAptitudeRequirements.NullOrEmpty();

        /// <summary>
        /// Given to the wielder while they hold the relic, by aptitude tier (strain, fatigue, pain). All optional.
        /// </summary>
        public HediffDef fullAptitudeWielderHediff;
        public HediffDef partialAptitudeWielderHediff;
        public HediffDef unworthyWielderHediff;

        /// <summary>
        /// Given to an unworthy pawn when the relic rejects them. Both optional.
        /// </summary>
        public ThoughtDef rejectionThought;
        public HediffDef rejectionHediff;

        public CompProperties_RelicAptitude() => compClass = typeof(CompRelicAptitude);

        /// <summary>
        /// When false, the framework's own aptitude box isn't shown, for weapons that show aptitude in their own status gizmo.
        /// </summary>
        public bool showAptitudeGizmo = true;

        public string RelicKey(ThingDef parentDefinition) => relicKey.NullOrEmpty() ? parentDefinition.defName : relicKey;
    }



    /// <summary>
    /// Makes a weapon a relic: each pawn has an aptitude for it (from a hidden roll, guaranteed traits or genes, or skills), revealed the first time they try it.
    /// Unworthy pawns can be rejected; pawns with partial or no aptitude can wield it with weaker stats.
    /// </summary>
    public class CompRelicAptitude : ThingComp, IEquippedGizmoProvider
    {
        public CompProperties_RelicAptitude Props => (CompProperties_RelicAptitude)props;

        public string RelicKey => Props.RelicKey(parent.def);

        private Pawn Wielder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            // For relics tested on draw, the switch ability already refused unworthy pawns; this catches every other way of getting it.
            AptitudeLevel aptitude = RelicAptitude.GetAptitude(pawn, Props, parent.def);
            if (aptitude == AptitudeLevel.Unworthy && Props.rejectsUnworthy)
            {
                RelicAptitude.ApplyRejection(pawn, Props);
                GameComponent_RelicAptitude.Instance.QueueDrop(pawn, parent);
                return;
            }

            HediffDef wielderHediff = WielderHediffFor(aptitude);
            if (wielderHediff != null && !pawn.health.hediffSet.HasHediff(wielderHediff)) pawn.health.AddHediff(wielderHediff);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            RemoveWielderHediff(pawn, Props.fullAptitudeWielderHediff);
            RemoveWielderHediff(pawn, Props.partialAptitudeWielderHediff);
            RemoveWielderHediff(pawn, Props.unworthyWielderHediff);
        }

        private HediffDef WielderHediffFor(AptitudeLevel aptitude)
        {
            if (aptitude == AptitudeLevel.Full) return Props.fullAptitudeWielderHediff;
            if (aptitude == AptitudeLevel.Partial) return Props.partialAptitudeWielderHediff;
            return Props.unworthyWielderHediff;
        }

        private static void RemoveWielderHediff(Pawn pawn, HediffDef hediffDefinition)
        {
            if (hediffDefinition == null) return;
            Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDefinition);
            if (existingHediff != null) pawn.health.RemoveHediff(existingHediff);
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (Props.partialStatFactors.NullOrEmpty() && Props.unworthyStatFactors.NullOrEmpty()) return 1f;
            List<StatModifier> statFactors = StatListForWielder(Props.partialStatFactors, Props.unworthyStatFactors);
            return statFactors.NullOrEmpty() ? 1f : statFactors.GetStatFactorFromList(stat);
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (Props.partialStatOffsets.NullOrEmpty() && Props.unworthyStatOffsets.NullOrEmpty()) return 0f;
            List<StatModifier> statOffsets = StatListForWielder(Props.partialStatOffsets, Props.unworthyStatOffsets);
            return statOffsets.NullOrEmpty() ? 0f : statOffsets.GetStatOffsetFromList(stat);
        }

        /// <summary>
        /// Adds a line to a stat's breakdown on the info card when the wielder's aptitude changes it,
        /// e.g. "Partial aptitude: x80%".
        /// </summary>
        public override void GetStatsExplanation(StatDef stat, StringBuilder stringBuilder, string whitespace = "")
        {
            Pawn wielder = Wielder;
            if (wielder == null) return;

            AptitudeLevel aptitude = RelicAptitude.GetAptitude(wielder, Props, parent.def);
            if (aptitude == AptitudeLevel.Full) return;

            string tierLabel = RelicAptitude.Label(aptitude).CapitalizeFirst();

            List<StatModifier> statFactors = StatListForWielder(Props.partialStatFactors, Props.unworthyStatFactors);
            if (!statFactors.NullOrEmpty())
            {
                float factor = statFactors.GetStatFactorFromList(stat);
                if (factor != 1f) stringBuilder.AppendLine($"{whitespace}{tierLabel}: x{factor.ToStringPercent()}");
            }

            List<StatModifier> statOffsets = StatListForWielder(Props.partialStatOffsets, Props.unworthyStatOffsets);
            if (!statOffsets.NullOrEmpty())
            {
                float offset = statOffsets.GetStatOffsetFromList(stat);
                if (offset != 0f) stringBuilder.AppendLine($"{whitespace}{tierLabel}: {stat.ValueToString(offset, ToStringNumberSense.Offset)}");
            }
        }

        /// <summary>
        /// The aptitude status box, shown while a pawn holds the relic.
        /// </summary>
        public IEnumerable<Gizmo> GetEquippedGizmos(Pawn wielder)
        {
            if (!Props.showAptitudeGizmo) yield break;
            yield return new Gizmo_RelicAptitude { aptitudeComp = this, wielder = wielder };
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
