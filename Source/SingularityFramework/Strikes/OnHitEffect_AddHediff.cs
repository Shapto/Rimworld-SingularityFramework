using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Adds a hediff to the target, or to the attacker for self-buffs. Hitting again adds to the existing hediff's severity.
    /// What the hediff does, its maximum and how fast it fades are all set on the HediffDef in XML.
    /// </summary>
    public class OnHitEffect_AddHediff : OnHitEffect
    {
        public HediffDef hediffDefinition;
        public float severity = 1f;
        public bool applyToAttacker;

        protected override void Apply(Pawn attacker, Thing target)
        {
            Pawn receivingPawn = applyToAttacker ? attacker : target as Pawn;
            if (hediffDefinition == null || receivingPawn == null || receivingPawn.Dead) return;

            Hediff existingHediff = receivingPawn.health.hediffSet.GetFirstHediffOfDef(hediffDefinition);
            if (existingHediff != null)
            {
                existingHediff.Severity += severity;
                return;
            }

            Hediff newHediff = HediffMaker.MakeHediff(hediffDefinition, receivingPawn);
            newHediff.Severity = severity;
            receivingPawn.health.AddHediff(newHediff);
        }
    }
}
