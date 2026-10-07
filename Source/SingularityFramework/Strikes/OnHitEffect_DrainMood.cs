using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Takes mood from the target directly. Mood recovers on its own toward what the pawn's thoughts say it should be.
    /// </summary>
    public class OnHitEffect_DrainMood : OnHitEffect
    {
        public float moodDrained = 0.05f;

        protected override void Apply(Pawn attacker, Thing target)
        {
            if (!(target is Pawn targetPawn) || targetPawn.Dead) return;

            Need_Mood targetMood = targetPawn.needs?.mood;
            if (targetMood == null) return;

            targetMood.CurLevel -= moodDrained;
        }
    }
}
