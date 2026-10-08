using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// One condition a pawn has to meet for a relic aptitude tier.
    /// Written in XML as a list, with Class="SingularityFramework.Relics.AptitudeRequirement_..." on each entry.
    /// </summary>
    public abstract class AptitudeRequirement
    {
        public float minimum;

        /// <summary>
        /// The pawn's current value for this requirement.
        /// </summary>
        public abstract float CurrentValue(Pawn pawn);

        /// <summary>
        /// The requirement's name as the player sees it, e.g. "Melee" or "Move speed".
        /// </summary>
        public abstract string Label { get; }

        /// <summary>
        /// How far below the minimum still counts as meeting it, matching the rounding the player sees.
        /// Without it, a stat that shows "5.50 / 5.50" can still fail because it's really 5.4999995.
        /// </summary>
        protected virtual float Tolerance => 0f;

        /// <summary>
        /// Turns a value into text the way the player is used to seeing it (a level, a percentage, cells per second).
        /// </summary>
        protected abstract string FormatValue(float value);

        /// <summary>
        /// True if the pawn's current value is at least the minimum.
        /// </summary>
        public bool PawnSatisfies(Pawn pawn)
        {
            return pawn != null && CurrentValue(pawn) >= minimum - Tolerance;
        }

        /// <summary>
        /// A line like "Manipulation: 84% / 90%", colored by whether it's met.
        /// </summary>
        public string Describe(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            string requirementLine = "Sing_AptitudeRequirementLine".Translate(Label, FormatValue(CurrentValue(pawn)), FormatValue(minimum));
            if (!PawnSatisfies(pawn)) requirementLine = requirementLine.Colorize(ColorLibrary.RedReadable);
            return requirementLine;
        }
    }
}
