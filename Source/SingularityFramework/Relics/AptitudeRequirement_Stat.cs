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
    /// Represents an aptitude requirement based on a specific stat value for a pawn.
    /// </summary>
    /// <remarks>Use this class to define requirements that depend on a pawn's stat, such as skill level or
    /// attribute. The requirement is evaluated using the stat defined by the associated StatDef.</remarks>
    public class AptitudeRequirement_Stat : AptitudeRequirement
    {
        public StatDef stat;

        public override float CurrentValue(Pawn pawn) => pawn.GetStatValue(stat);

        public override string Label => stat.LabelCap;

        protected override string FormatValue(float value) => stat.ValueToString(value);
    }
}
