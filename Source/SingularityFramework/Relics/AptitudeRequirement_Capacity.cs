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
    /// Represents an aptitude requirement based on a specific pawn capacity.
    /// </summary>
    /// <remarks>This class evaluates whether a pawn meets a requirement by checking the level of a specified
    /// capacity, such as movement or manipulation. It is typically used in scenarios where certain actions or roles
    /// require a minimum capacity level.</remarks>
    public class AptitudeRequirement_Capacity : AptitudeRequirement
    {
        public PawnCapacityDef capacity;

        public override float CurrentValue(Pawn pawn) => pawn.health?.capacities?.GetLevel(capacity) ?? 0f;

        public override string Label => capacity.LabelCap;

        protected override string FormatValue(float value) => value.ToStringPercent();
    }
}
