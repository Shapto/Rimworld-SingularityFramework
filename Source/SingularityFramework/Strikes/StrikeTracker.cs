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
    /// Remembers which strike modifiers joined each pawn's current attack, and whether that attack landed.
    /// Driven by the attack patches; modifiers never call this themselves.
    /// </summary>
    public static class StrikeTracker
    {
        private class ActiveStrike
        {
            public List<IStrikeModifier> modifiers = new List<IStrikeModifier>();
            public bool landed;
            public Thing landedTarget;
        }

        private static readonly Dictionary<Pawn, ActiveStrike> activeStrikes = new Dictionary<Pawn, ActiveStrike>();

        /// <summary>
        /// Before an attack: asks every comp on the attacker whether it wants to join, and keeps the ones that do.
        /// Returns true if any joined.
        /// </summary>
        public static bool BeginStrike(Pawn attacker, Verb attackVerb)
        {
            if (attacker == null) return false;

            activeStrikes.Remove(attacker);

            var activeStrike = new ActiveStrike();

            foreach (object candidate in AllCandidateComps(attacker).ToList())
            {
                if (candidate is IStrikeModifier strikeModifier && strikeModifier.TryJoinStrike(attacker, attackVerb))
                    activeStrike.modifiers.Add(strikeModifier);
            }

            if (activeStrike.modifiers.Count == 0) return false;

            activeStrikes[attacker] = activeStrike;
            return true;
        }

        /// <summary>
        /// While damage is being dealt: lets every joined modifier change it, and marks the strike as landed.
        /// Can run several times per strike (cleaves, extra damage), so once-per-swing effects belong in Notify_StrikeLanded.
        /// </summary>
        public static void ModifyDamage(Pawn attacker, Thing target, ref DamageInfo damageInfo)
        {
            if (attacker == null || !activeStrikes.TryGetValue(attacker, out ActiveStrike activeStrike)) return;

            foreach (IStrikeModifier strikeModifier in activeStrike.modifiers)
                strikeModifier.ModifyDamage(attacker, target, ref damageInfo);

            activeStrike.landed = true;

            if (activeStrike.landedTarget == null) activeStrike.landedTarget = target;
        }


        /// <summary>
        /// After the attack: tells every joined modifier whether it landed, then forgets the strike.
        /// </summary>
        public static void EndStrike(Pawn attacker)
        {
            if (attacker == null || !activeStrikes.TryGetValue(attacker, out ActiveStrike activeStrike)) return;

            activeStrikes.Remove(attacker);

            foreach (IStrikeModifier strikeModifier in activeStrike.modifiers)
            {
                if (activeStrike.landed) strikeModifier.Notify_StrikeLanded(attacker, activeStrike.landedTarget);
                else strikeModifier.Notify_StrikeMissed(attacker);
            }
        }

        /// <summary>
        /// Every comp on the attacker that could be a strike modifier: weapon comps, worn apparel comps and hediff comps.
        /// </summary>
        private static IEnumerable<object> AllCandidateComps(Pawn attacker)
        {
            List<ThingComp> weaponComps = attacker.equipment?.Primary?.AllComps;
            if (weaponComps != null)
            {
                foreach (ThingComp weaponComp in weaponComps)
                    yield return weaponComp;
            }

            List<Apparel> wornApparel = attacker.apparel?.WornApparel;
            if (wornApparel != null)
            {
                foreach (Apparel apparel in wornApparel)
                {
                    foreach (ThingComp apparelComp in apparel.AllComps)
                        yield return apparelComp;
                }
            }

            List<Hediff> hediffs = attacker.health?.hediffSet?.hediffs;
            if (hediffs != null)
            {
                foreach (Hediff hediff in hediffs)
                {
                    if (!(hediff is HediffWithComps hediffWithComps)) continue;
                    foreach (HediffComp hediffComp in hediffWithComps.comps)
                        yield return hediffComp;
                }
            }
        }

    }
}
