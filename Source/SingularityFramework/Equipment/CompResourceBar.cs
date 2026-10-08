using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Equipment
{
    public class CompProperties_ResourceBar : CompProperties
    {
        /// <summary>
        /// Shown above the bar. Leave empty to use the reloadable comp's charge noun (e.g. "burner charge").
        /// </summary>
        public string label;

        public Color barColor = new Color(0.9f, 0.7f, 0.2f);

        public CompProperties_ResourceBar() => compClass = typeof(CompResourceBar);
    }

    /// <summary>
    /// Shows the weapon's vanilla reloadable charges (ammo, charge) as a bar gizmo while equipped.
    /// Needs a CompEquippableAbilityReloadable on the same weapon.
    /// </summary>
    public class CompResourceBar : ThingComp, IEquippedGizmoProvider
    {
        private Texture2D cachedBarTexture;

        public CompProperties_ResourceBar Props => (CompProperties_ResourceBar)props;

        public CompEquippableAbilityReloadable ReloadableComp => parent.TryGetComp<CompEquippableAbilityReloadable>();

        /// <summary>
        /// Made on first draw, which is always on the main thread.
        /// </summary>
        public Texture2D BarTexture
        {
            get
            {
                if (cachedBarTexture == null) cachedBarTexture = SolidColorMaterials.NewSolidColorTexture(Props.barColor);
                return cachedBarTexture;
            }
        }

        public string BarLabel => Props.label.NullOrEmpty() ? ReloadableComp?.Props.chargeNoun.CapitalizeFirst() : Props.label;

        public IEnumerable<Gizmo> GetEquippedGizmos(Pawn wielder)
        {
            if (ReloadableComp == null) yield break;
            yield return new Gizmo_ResourceBar { resourceBar = this };
        }
    }

    /// <summary>
    /// A bar with a label above it and "current / maximum" on it.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Gizmo_ResourceBar : Gizmo
    {
        private static readonly Texture2D EmptyBarTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.1f, 0.1f, 0.1f));

        public CompResourceBar resourceBar;

        public Gizmo_ResourceBar()
        {
            Order = -100f;
        }

        public override float GetWidth(float maximumWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maximumWidth, GizmoRenderParms renderParameters)
        {
            CompEquippableAbilityReloadable reloadableComp = resourceBar.ReloadableComp;

            var outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maximumWidth), 75f);
            Rect innerRect = outerRect.ContractedBy(6f);
            Widgets.DrawWindowBackground(outerRect);

            Rect labelRect = innerRect;
            labelRect.height = innerRect.height / 2f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, resourceBar.BarLabel);

            Rect barRect = innerRect;
            barRect.yMin = innerRect.y + innerRect.height / 2f;
            float fillPercentage = reloadableComp.MaxCharges > 0 ? reloadableComp.RemainingCharges / (float)reloadableComp.MaxCharges : 0f;
            Widgets.FillableBar(barRect, fillPercentage, resourceBar.BarTexture, EmptyBarTexture, false);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, reloadableComp.LabelRemaining);
            Text.Anchor = TextAnchor.UpperLeft;

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
