using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// Passes when the pawn meets at least one of its options, e.g. "Bloodlust or Brawler".
    /// The label is a translation key so the group can have a name like "Battle freak".
    /// </summary>
    public class AptitudeRequirement_AnyOf : AptitudeRequirement
    {
        public string labelKey;
        public List<AptitudeRequirement> options = new List<AptitudeRequirement>();

        public AptitudeRequirement_AnyOf()
        {
            minimum = 1f;
        }

        public override float CurrentValue(Pawn pawn) => options.Any(option => option.PawnSatisfies(pawn)) ? 1f : 0f;

        public override string Label => labelKey.Translate();

        protected override string FormatValue(float value) => value >= 1f ? "Sing_AptitudeYes".Translate() : "Sing_AptitudeNo".Translate();

        public override string Describe(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            StringBuilder builder = new StringBuilder(base.Describe(pawn));
            foreach (AptitudeRequirement option in options)
            {
                builder.AppendLine();
                builder.Append("    " + "Sing_AptitudeAnyOfOption".Translate(option.Describe(pawn)).Resolve());
            }
            return builder.ToString();
        }
    }
}
