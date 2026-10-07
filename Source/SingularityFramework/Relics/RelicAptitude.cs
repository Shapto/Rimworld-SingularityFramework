using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// Works out, reveals and applies relic aptitude.
    /// </summary>
    public static class RelicAptitude
    {
        /// <summary>
        /// The pawn's aptitude for the relic. Fixed per pawn and relic: the roll is seeded, so it never changes.
        /// </summary>
        public static AptitudeLevel GetAptitude(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, ThingDef relicDefinition)
        {
            if (HasGuaranteedAptitude(pawn, aptitudeProperties)) return AptitudeLevel.Full;

            int seed = pawn.thingIDNumber ^ GenText.StableStringHash(aptitudeProperties.RelicKey(relicDefinition));
            float roll = Rand.ValueSeeded(seed);
            if (roll < aptitudeProperties.fullAptitudeChance) return AptitudeLevel.Full;
            if (roll < aptitudeProperties.partialAptitudeChance) return AptitudeLevel.Partial;
            return AptitudeLevel.Unworthy;
        }

        /// <summary>
        /// Gets the aptitude and, the first time, tells the player.
        /// </summary>
        public static AptitudeLevel TestAndReveal(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, ThingDef relicDefinition)
        {
            AptitudeLevel aptitude = GetAptitude(pawn, aptitudeProperties, relicDefinition);
            string relicKey = aptitudeProperties.RelicKey(relicDefinition);

            if (GameComponent_RelicAptitude.Instance.MarkKnown(pawn, relicKey) && pawn.Faction == Faction.OfPlayer)
            {
                MessageTypeDef messageType = aptitude == AptitudeLevel.Unworthy ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent;
                Messages.Message("Sing_AptitudeRevealed".Translate(pawn.LabelShort, relicDefinition.label, Label(aptitude)), pawn, messageType);
            }
            return aptitude;
        }

        public static void ApplyRejection(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties)
        {
            if (aptitudeProperties.rejectionThought != null) pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(aptitudeProperties.rejectionThought);
            if (aptitudeProperties.rejectionHediff != null) pawn.health.AddHediff(aptitudeProperties.rejectionHediff);
        }

        /// <summary>
        /// Adds the partial aptitude hediff for partial aptitude, removes it otherwise.
        /// </summary>
        public static void UpdatePartialHediff(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, AptitudeLevel aptitude)
        {
            HediffDef partialHediff = aptitudeProperties.partialAptitudeHediff;
            if (partialHediff == null) return;

            Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(partialHediff);
            if (aptitude == AptitudeLevel.Partial && existingHediff == null) pawn.health.AddHediff(partialHediff);
            else if (aptitude != AptitudeLevel.Partial && existingHediff != null) pawn.health.RemoveHediff(existingHediff);
        }

        public static string Label(AptitudeLevel aptitude) => ("Sing_Aptitude_" + aptitude).Translate();

        private static bool HasGuaranteedAptitude(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties)
        {
            if (aptitudeProperties.guaranteedTraits != null && pawn.story?.traits != null)
            {
                foreach (TraitDef trait in aptitudeProperties.guaranteedTraits)
                {
                    if (pawn.story.traits.HasTrait(trait)) return true;
                }
            }

            if (aptitudeProperties.guaranteedGenes != null && pawn.genes != null)
            {
                foreach (GeneDef gene in aptitudeProperties.guaranteedGenes)
                {
                    if (pawn.genes.HasActiveGene(gene)) return true;
                }
            }
            return false;
        }
    }
}
