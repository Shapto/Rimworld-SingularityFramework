using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// A dashing pawn is drawn at its exact point along the line, so it glides instead of hopping between cells.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Patch_Pawn_DrawPos
    {
        public static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            if (__instance.jobs?.curDriver is JobDriver_LineDash dashDriver) __result = dashDriver.CurrentDrawPosition;
        }
    }
}
