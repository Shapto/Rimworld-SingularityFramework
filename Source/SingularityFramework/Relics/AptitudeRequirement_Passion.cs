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
    /// Requires at least a given passion in one skill. Minimum 1 is a minor passion, 2 is a major passion.
    /// </summary>
    public class AptitudeRequirement_Passion : AptitudeRequirement
    {
        public SkillDef skill;

        public AptitudeRequirement_Passion()
        {
            minimum = 1f;
        }

        public override float CurrentValue(Pawn pawn) => (float)(pawn.skills?.GetSkill(skill)?.passion ?? Passion.None);

        public override string Label => "Sing_AptitudePassionLabel".Translate(skill.LabelCap);

        protected override string FormatValue(float value)
        {
            if (value >= (float)Passion.Major) return "Sing_AptitudePassionMajor".Translate();
            if (value >= (float)Passion.Minor) return "Sing_AptitudePassionMinor".Translate();
            return "Sing_AptitudePassionNone".Translate();
        }
    }
}
