using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Equipment
{
    /// <summary>
    /// Vanilla only takes the first equippable ability comp on a weapon. This adds the abilities of every other one,
    /// so a weapon can grant several abilities by listing several comps.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.AllAbilitiesForReading), MethodType.Getter)]
    public static class Patch_PawnAbilityTracker_AllAbilitiesForReading
    {
        private static readonly AccessTools.FieldRef<Pawn_AbilityTracker, bool> CacheIsDirty = AccessTools.FieldRefAccess<Pawn_AbilityTracker, bool>("allAbilitiesCachedDirty");

        public static void Prefix(Pawn_AbilityTracker __instance, out bool __state)
        {
            __state = CacheIsDirty(__instance);
        }

        public static void Postfix(Pawn_AbilityTracker __instance, bool __state, List<Ability> __result)
        {
            if (!__state) return;

            ThingWithComps weapon = __instance.pawn.equipment?.Primary;
            if (weapon == null) return;

            bool isFirstAbilityComp = true;
            foreach (ThingComp weaponComp in weapon.AllComps)
            {
                if (!(weaponComp is CompEquippableAbility abilityComp)) continue;

                // Vanilla already added the first one.
                if (isFirstAbilityComp)
                {
                    isFirstAbilityComp = false;
                    continue;
                }

                if (abilityComp.AbilityForReading != null) __result.Add(abilityComp.AbilityForReading);
            }
        }
    }
}
