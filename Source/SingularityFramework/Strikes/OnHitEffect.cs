using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// A small effect that happens when an attack lands (extra damage or a stun).
    /// Written in XML as a list on a comp, with Class="SingularityFramework.Strikes.OnHitEffect_..." on each entry.
    /// </summary>
    public abstract class OnHitEffect
    {
        /// <summary>
        /// Chance from 0 to 1 that this effect happens on a given hit.
        /// </summary>
        public float chance = 1f;

        /// <summary>
        /// Does the effect. Only called when the chance roll succeeded and the target can still be affected.
        /// </summary>
        protected abstract void Apply(Pawn attacker, Thing target);

        /// <summary>
        /// Rolls the chance and applies the effect if the target is still there to be hit.
        /// </summary>
        public void TryApply(Pawn attacker, Thing target)
        {
            if (attacker == null || target == null || target.Destroyed || !target.Spawned) return;
            if (!Rand.Chance(chance)) return;
            Apply(attacker, target);
        }

        /// <summary>
        /// Applies every effect in the list, the whole list repeated "repeatCount" times.
        /// </summary>
        public static void TryApplyAll(List<OnHitEffect> effects, Pawn attacker, Thing target)
        {
            if (effects == null) return;

            foreach (OnHitEffect effect in effects)
                effect.TryApply(attacker, target);
        }
    }

}
