using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Something that can change a single attack: decide to join it, change its damage, and react to whether it landed.
    /// Subclass once per effect and override only the hooks the effect needs.
    /// </summary>
    public abstract class StrikeModifier
    {
        /// <summary>
        /// Called right before an attack. Return true to join this strike.
        /// Up-front costs (spending ammo, mood) are paid here, so a modifier that can't afford it simply doesn't join.
        /// </summary>
        public abstract bool TryJoinStrike(Pawn attacker, Verb attackVerb);

        /// <summary>
        /// Called for each instance of damage this strike deals, before it lands. Change the damage here.
        /// </summary>
        public virtual void ModifyDamage(Pawn attacker, Thing target, ref DamageInfo damageInfo) { }

        /// <summary>
        /// The strike hit. On-hit effects go here.
        /// </summary>
        public virtual void Notify_StrikeLanded(Pawn attacker, Thing target) { }

        /// <summary>
        /// The strike missed or was dodged.
        /// </summary>
        public virtual void Notify_StrikeMissed(Pawn attacker) { }
    }
}
