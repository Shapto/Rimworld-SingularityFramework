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
    /// Requires a minimum level in one skill, e.g. Melee 18.
    /// </summary>
    public class AptitudeRequirement_Skill : AptitudeRequirement
    {
        public SkillDef skill;

        public override float CurrentValue(Pawn pawn) => pawn.skills?.GetSkill(skill)?.Level ?? 0;

        public override string Label => skill.LabelCap;

        protected override string FormatValue(float value) => ((int)value).ToString();
    }
}
