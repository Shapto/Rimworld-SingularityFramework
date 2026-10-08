using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using static System.Net.Mime.MediaTypeNames;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// Works out, reveals and applies relic aptitude.
    /// </summary>
    public static class RelicAptitude
    {
        /// <summary>
        /// The pawn's aptitude for the relic. From the roll it's fixed per pawn and relic; from skills it changes as the pawn's skills do.
        /// </summary>
        public static AptitudeLevel GetAptitude(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, ThingDef relicDefinition)
        {
            if (HasGuaranteedAptitude(pawn, aptitudeProperties)) return AptitudeLevel.Full;

            // Requirement-based relics: aptitude comes from the pawn's skills, capacities and stats instead of the roll.
            if (aptitudeProperties.UsesRequirementAptitude)
            {
                if (MeetsRequirements(pawn, aptitudeProperties.fullAptitudeRequirements)) return AptitudeLevel.Full;
                if (MeetsRequirements(pawn, aptitudeProperties.partialAptitudeRequirements)) return AptitudeLevel.Partial;
                return AptitudeLevel.Unworthy;
            }

            int seed = pawn.thingIDNumber ^ GenText.StableStringHash(aptitudeProperties.RelicKey(relicDefinition));
            float roll = Rand.ValueSeeded(seed);
            if (roll < aptitudeProperties.fullAptitudeChance) return AptitudeLevel.Full;
            if (roll < aptitudeProperties.partialAptitudeChance) return AptitudeLevel.Partial;
            return AptitudeLevel.Unworthy;
        }

        /// <summary>
        /// True if the pawn meets every requirement in the list. An empty list is never met, so a tier without requirements can't be reached.
        /// </summary>
        private static bool MeetsRequirements(Pawn pawn, List<AptitudeRequirement> requirements)
        {
            if (requirements.NullOrEmpty() || pawn == null) return false;

            foreach (AptitudeRequirement requirement in requirements)
            {
                if (!requirement.PawnSatisfies(pawn)) return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the aptitude and, the first time, tells the player.
        /// </summary>
        public static AptitudeLevel TestAndReveal(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, ThingDef relicDefinition)
        {
            AptitudeLevel aptitude = GetAptitude(pawn, aptitudeProperties, relicDefinition);
            string relicKey = aptitudeProperties.RelicKey(relicDefinition);

            if (pawn.Faction == Faction.OfPlayer && pawn.Spawned && GameComponent_RelicAptitude.Instance.MarkKnown(pawn, relicKey))
            {
                string revealText = "Sing_AptitudeRevealedShort".Translate(relicDefinition.LabelCap, Label(aptitude));
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, revealText, TierColor(aptitude), 3.5f);
            }
            return aptitude;
        }

        /// <summary>
        /// The color tier text is shown in: green for full, yellow for partial, red for unworthy.
        /// </summary>
        public static Color TierColor(AptitudeLevel aptitude)
        {
            if (aptitude == AptitudeLevel.Unworthy) return ColorLibrary.RedReadable;
            if (aptitude == AptitudeLevel.Partial) return Color.yellow;
            return Color.green;
        }

        public static void ApplyRejection(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties)
        {
            if (aptitudeProperties.rejectionThought != null) pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(aptitudeProperties.rejectionThought);
            if (aptitudeProperties.rejectionHediff != null) pawn.health.AddHediff(aptitudeProperties.rejectionHediff);
        }

        public static string Label(AptitudeLevel aptitude) => ("Sing_Aptitude_" + aptitude).Translate();

        public static string DescribeAptitude(Pawn pawn, CompProperties_RelicAptitude aptitudeProperties, ThingDef relicDefinition)
        {
            if (pawn == null) return string.Empty;
            AptitudeLevel aptitude = GetAptitude(pawn, aptitudeProperties, relicDefinition);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Sing_AptitudeCurrent".Translate(Label(aptitude)));
            List<AptitudeRequirement> nextTierRequirements = aptitude == AptitudeLevel.Unworthy ? aptitudeProperties.partialAptitudeRequirements : aptitude == AptitudeLevel.Partial ? aptitudeProperties.fullAptitudeRequirements : null;
            if (!nextTierRequirements.NullOrEmpty())
            {
                foreach (AptitudeRequirement requirement in nextTierRequirements)
                    builder.AppendLine(requirement.Describe(pawn));
            }
            return builder.ToString().TrimEndNewlines();
        }

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
