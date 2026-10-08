using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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

        /// <summary>
        /// Gets the localized label for the specified singing aptitude level.
        /// </summary>
        /// <remarks>The returned label is localized based on the current language settings. If a
        /// translation is not available for the specified aptitude level, the fallback behavior of the translation
        /// system applies.</remarks>
        /// <param name="aptitude">The aptitude level for which to retrieve the label.</param>
        /// <returns>A localized string representing the label of the specified aptitude level.</returns>
        public static string Label(AptitudeLevel aptitude) => ("Sing_Aptitude_" + aptitude).Translate();

        /// <summary>
        /// Generates a descriptive summary of a pawn's aptitude for a specified relic, including current aptitude level
        /// and requirements for the next tier, if applicable.
        /// </summary>
        /// <remarks>The returned string is intended for display in user interfaces and is localized using
        /// the game's translation system. If the pawn has already reached the highest aptitude tier, no further
        /// requirements are listed.</remarks>
        /// <param name="pawn">The pawn whose aptitude is being described. If null, an empty string is returned.</param>
        /// <param name="aptitudeProperties">The aptitude properties that define the requirements and tiers for the relic.</param>
        /// <param name="relicDefinition">The definition of the relic for which the pawn's aptitude is evaluated.</param>
        /// <returns>A string containing the pawn's current aptitude level and, if applicable, the requirements needed to reach
        /// the next aptitude tier. Returns an empty string if the pawn is null.</returns>
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
