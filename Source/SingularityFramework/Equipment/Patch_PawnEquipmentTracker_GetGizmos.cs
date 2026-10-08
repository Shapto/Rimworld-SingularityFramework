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
    /// Adds resource bar gizmos for the wielder's weapon. Vanilla only asks one comp for equipment gizmos.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
    public static class Patch_PawnEquipmentTracker_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn_EquipmentTracker __instance)
        {
            foreach (Gizmo gizmo in gizmos)
                yield return gizmo;

            if (!__instance.pawn.IsColonistPlayerControlled) yield break;

            ThingWithComps weapon = __instance.Primary;
            if (weapon == null) yield break;

            foreach (ThingComp weaponComp in weapon.AllComps)
            {
                if (!(weaponComp is IEquippedGizmoProvider gizmoProvider)) continue;

                foreach (Gizmo providedGizmo in gizmoProvider.GetEquippedGizmos(__instance.pawn))
                    yield return providedGizmo;
            }
        }
    }
}
