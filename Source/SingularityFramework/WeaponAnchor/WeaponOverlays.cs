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
    /// Asks whether to draw something over a pawn's held weapon. True with the material to draw, and how much bigger than
    /// the weapon to draw it, across and along (1 = exactly the weapon's size; bigger for overlays with room around them).
    /// </summary>
    public delegate bool WeaponOverlayProvider(Pawn pawn, Thing weapon, out Material material, out Vector2 drawScale);

    /// <summary>
    /// Pictures drawn over held weapons (a coating, a glow), following the weapon exactly however it's drawn: by vanilla,
    /// or by an animation mod. Mods register a provider once; it's asked every time a weapon is drawn.
    /// </summary>
    public static class WeaponOverlays
    {
        private const float OverlayAltitude = 0.0004f;   // just above the weapon

        private static readonly List<WeaponOverlayProvider> providers = new List<WeaponOverlayProvider>();

        /// <summary>
        /// Adds a provider (call once, for example from the mod's constructor).
        /// </summary>
        public static void Register(WeaponOverlayProvider provider)
        {
            if (!providers.Contains(provider)) providers.Add(provider);
        }

        /// <summary>
        /// Draws every overlay a provider wants on this weapon, with the weapon's own transform. Called right after the
        /// weapon is drawn and recorded.
        /// </summary>
        internal static void DrawFor(Pawn pawn, Thing weapon, WeaponDrawRecord record)
        {
            if (weapon == null) return;
            foreach (WeaponOverlayProvider provider in providers)
            {
                if (!provider(pawn, weapon, out Material material, out Vector2 drawScale) || material == null) continue;
                Mesh mesh = record.flipped ? MeshPool.plane10Flip : MeshPool.plane10;
                Graphics.DrawMesh(mesh, OverlayMatrix(record, drawScale), material, 0);
            }
        }

        /// <summary>
        /// The weapon's own transform, scaled by "drawScale" about the weapon's middle and lifted just above it.
        /// </summary>
        private static Matrix4x4 OverlayMatrix(WeaponDrawRecord record, Vector2 drawScale)
        {
            Matrix4x4 lift = Matrix4x4.Translate(new Vector3(0f, OverlayAltitude, 0f));
            if (record.usesMatrix)
            {
                // an animation mod's matrix maps the weapon's quad; flipping vertically turns the quad over along its length
                Vector3 scale = new Vector3(drawScale.x, 1f, drawScale.y * (record.flippedVertically ? -1f : 1f));
                return lift * record.drawMatrix * Matrix4x4.Scale(scale);
            }
            Vector3 size = new Vector3(record.drawSize.x * drawScale.x, 0f, record.drawSize.y * drawScale.y);
            return lift * Matrix4x4.TRS(record.position, Quaternion.AngleAxis(record.drawAngle, Vector3.up), size);
        }
    }
}
