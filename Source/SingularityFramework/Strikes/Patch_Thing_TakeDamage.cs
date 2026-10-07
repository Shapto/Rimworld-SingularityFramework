using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Every attack's damage passes through Thing.TakeDamage. If the attacker is in the middle of a strike,
    /// its modifiers get to change the damage, and the strike counts as landed.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(dinfo.Instigator is Pawn attacker)) return;
            StrikeTracker.ModifyDamage(attacker, __instance, ref dinfo);
        }
    }

}
