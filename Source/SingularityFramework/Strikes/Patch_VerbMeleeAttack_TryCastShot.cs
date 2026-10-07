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

            DamageDef damageDefinition = __instance.GetDamageDef();
            if (damageDefinition == null || !damageDefinition.harmsHealth) return;

            __state = StrikeTracker.BeginStrike(attacker, __instance);
        }

        // Runs after other mods' postfixes so their strikes have already ended
        // and damage from our on-hit effects isn't boosted by them.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Verb_MeleeAttack __instance, bool __state)
        {
            if (!__state) return;
            StrikeTracker.EndStrike(__instance.CasterPawn);
        }
    }

}

