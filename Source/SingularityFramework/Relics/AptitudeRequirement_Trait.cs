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
    /// Requires the pawn to have a trait, at a specific degree for traits that have degrees.
    /// </summary>
    public class AptitudeRequirement_Trait : AptitudeRequirement
    {
        public TraitDef trait;
        public int degree;

        public AptitudeRequirement_Trait()
        {
            minimum = 1f;
        }

        public override float CurrentValue(Pawn pawn) => pawn.story?.traits?.HasTrait(trait, degree) == true ? 1f : 0f;

        public override string Label => trait.DataAtDegree(degree).GetLabelCapFor(null);

        protected override string FormatValue(float value) => value >= 1f ? "Sing_AptitudeYes".Translate() : "Sing_AptitudeNo".Translate();
    }
}
