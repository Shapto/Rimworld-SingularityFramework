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
    /// Stuns the target for a set time. The RimWorld-native stand-in for Tremor.
    /// </summary>
    public class OnHitEffect_Stun : OnHitEffect
    {
        /// <summary>
        /// How long the stun lasts. 60 ticks is one second.
        /// </summary>
        public int stunTicks = 60;

        /// <summary>
        /// When true, a target that's already stunned isn't stunned again, so repeated hits can't chain a stun forever.
        /// </summary>
        public bool skipIfAlreadyStunned = true;

        protected override void Apply(Pawn attacker, Thing target)
        {
            if (!(target is Pawn targetPawn) || targetPawn.Dead) return;

            StunHandler stunHandler = targetPawn.stances?.stunner;
            if (stunHandler == null) return;
            if (skipIfAlreadyStunned && stunHandler.Stunned) return;

            stunHandler.StunFor(stunTicks, attacker);
        }
    }

}
