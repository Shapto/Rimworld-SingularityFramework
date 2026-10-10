using HarmonyLib;
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
    /// Wraps every melee attack in a strike: modifiers join right before the swing,
    /// and learn whether it landed right after.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_VerbMeleeAttack_TryCastShot
    {
        public static void Prefix(Verb_MeleeAttack __instance, out bool __state)
        {
            __state = false;
            Pawn attacker = __instance.CasterPawn;
            if (attacker == null) return;

            if (!attacker.Spawned || attacker.stances.FullBodyBusy) return;
            if (!__instance.CanHitTarget(__instance.CurrentTarget)) return;

            DamageDef damageDefinition = __instance.GetDamageDef();
            if (damageDefinition == null || !damageDefinition.harmsHealth) return;

            __state = StrikeTracker.BeginStrike(attacker, __instance);
        }

         /// <summary>
         /// Ends the strike after the attack, even if the attack threw an error, so a crash can't leave
         /// the strike open and boost every later damage the pawn deals.Finalizers run after every postfix,
         /// so other mods' strikes have already ended and our on-hit effects aren't boosted by them.
         ///</summary>
        public static void Finalizer(Verb_MeleeAttack __instance, bool __state)
        {
            if (!__state) return;
            StrikeTracker.EndStrike(__instance.CasterPawn);
        }
    }

}

