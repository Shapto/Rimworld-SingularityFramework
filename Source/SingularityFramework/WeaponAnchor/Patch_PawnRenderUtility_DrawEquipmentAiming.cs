using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.WeaponAnchor
{
    /// <summary>
    /// Records where each held weapon is drawn, using the same angle and mirroring math as vanilla.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming
    {
        public static void Postfix(Thing eq, Vector3 drawLoc, float aimAngle, bool __runOriginal)
        {
            if (!__runOriginal) return;

            if (!(eq?.ParentHolder is Pawn_EquipmentTracker equipmentTracker) || equipmentTracker.pawn == null) return;

            // Same angle and mirroring math as vanilla's DrawEquipmentAiming.
            float drawAngle = aimAngle - 90f;
            bool flipped = false;
            if (aimAngle > 20f && aimAngle < 160f)
            {
                drawAngle += eq.def.equippedAngleOffset;
            }
            else if (aimAngle > 200f && aimAngle < 340f)
            {
                flipped = true;
                drawAngle -= 180f;
                drawAngle -= eq.def.equippedAngleOffset;
            }
            else
            {
                drawAngle += eq.def.equippedAngleOffset;
            }
            drawAngle %= 360f;

            WeaponDrawRecord.Record(equipmentTracker.pawn, eq.def, drawLoc, drawAngle, flipped, eq.Graphic.drawSize);

        }
    }
}
