using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// A status box showing the wielder's current aptitude for their relic, with what's missing for the next tier on hover.
    /// </summary>
    public class Gizmo_RelicAptitude : Gizmo
    {
        public CompRelicAptitude aptitudeComp;
        public Pawn wielder;

        public Gizmo_RelicAptitude()
        {
            Order = -100f;
        }
        public override float GetWidth(float maximumWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maximumWidth, GizmoRenderParms renderParameters)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maximumWidth), 75f);
            Rect innerRect = outerRect.ContractedBy(6f);
            Widgets.DrawWindowBackground(outerRect);

            AptitudeLevel aptitude = RelicAptitude.GetAptitude(wielder, aptitudeComp.Props, aptitudeComp.parent.def);

            Rect relicLabelRect = innerRect;
            relicLabelRect.height = innerRect.height / 2f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(relicLabelRect, aptitudeComp.parent.LabelCap);

            Rect tierLabelRect = innerRect;
            tierLabelRect.yMin = innerRect.y + innerRect.height / 2f;
            GUI.color = RelicAptitude.TierColor(aptitude);
            Text.Font = GameFont.Small;
            Widgets.Label(tierLabelRect, RelicAptitude.Label(aptitude).CapitalizeFirst());

            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Mouse.IsOver(outerRect))
            {
                Widgets.DrawHighlight(outerRect);
                TooltipHandler.TipRegion(outerRect, RelicAptitude.DescribeAptitude(wielder, aptitudeComp.Props, aptitudeComp.parent.def));
            }
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
