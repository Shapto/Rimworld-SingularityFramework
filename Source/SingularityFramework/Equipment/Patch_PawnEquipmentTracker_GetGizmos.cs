using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Equipment
{
    /// <summary>
    /// Adds the weapon form switch gizmo to the wielder's gizmos.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
    public static class Patch_PawnEquipmentTracker_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn_EquipmentTracker __instance)
        {
            foreach (Gizmo gizmo in gizmos)
                yield return gizmo;

            CompWeaponForm formComp = __instance.Primary?.TryGetComp<CompWeaponForm>();
            if (formComp == null) yield break;

            foreach (Gizmo formGizmo in formComp.GetFormGizmos(__instance.pawn))
                yield return formGizmo;
        }
    }
}
