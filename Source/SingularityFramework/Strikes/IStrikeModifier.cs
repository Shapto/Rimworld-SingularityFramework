using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Implemented by anything that can change an attack: a weapon comp, a hediff comp, and so on.
    /// The tracker finds these on the attacker's weapon and hediffs before every attack.
    /// </summary>
    public interface IStrikeModifier
    {
        bool TryJoinStrike(Pawn attacker, Verb attackVerb);
        void ModifyDamage(Pawn attacker, Thing target, ref DamageInfo damageInfo);
        void Notify_StrikeLanded(Pawn attacker, Thing target);
        void Notify_StrikeMissed(Pawn attacker);
    }
}
