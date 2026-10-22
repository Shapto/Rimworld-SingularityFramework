using SingularityFramework.WeaponAnchor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Visuals.Brushlines
{
    /// <summary>
    /// Brush lines forming a weapon in a pawn's hand: a stream runs from the pawn's body to the grip, a thick stroke
    /// shoots along the weapon from grip to tip with strands looping around it, then the lines break into dashed chunks
    /// and fade while the weapon's own picture hardens in under them.
    /// Dissolving plays it backwards: the chunks join up, pull back from the tip to the grip and drain into the body.
    /// </summary>
    public class WeaponFormingEffect : BrushLineEffect
    {
        private readonly Pawn holder;
        private readonly Thing weapon;                 // null when dissolving (the weapon is already gone)
        private readonly Func<Vector2> gripOnSprite;   // the weapon's ends on its sprite (-0.5 to 0.5); can change once its look is built
        private readonly Func<Vector2> tipOnSprite;
        private readonly bool dissolving;
        private readonly float bend;
        private readonly BrushStroke feed;
        private readonly BrushStroke core;
        private readonly BrushStroke[] strands;
        private Vector3 holderPosition;
        private Vector3 gripOffset;   // from the holder's draw position, kept for frames the weapon isn't drawn
        private Vector3 tipOffset;

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        /// <summary>
        /// "gripOffset", "tipOffset": where the weapon's ends are to start with, from the holder's draw position (see
        /// TryGetWeaponEnds and DefaultEnds). While forming they follow the weapon as it's drawn.
        /// </summary>
        public WeaponFormingEffect(BrushPalette palette, Pawn holder, Thing weapon, Func<Vector2> gripOnSprite, Func<Vector2> tipOnSprite, Vector3 gripOffset, Vector3 tipOffset, float width, bool dissolving, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            this.holder = holder;
            this.weapon = weapon;
            this.gripOnSprite = gripOnSprite;
            this.tipOnSprite = tipOnSprite;
            this.gripOffset = gripOffset;
            this.tipOffset = tipOffset;
            this.dissolving = dissolving;
            holderPosition = holder.DrawPos;
            var random = new System.Random(seed);
            bend = ((float)random.NextDouble() * 2f - 1f) * 0.3f;
            feed = new BrushStroke(FeedPath, width * 0.7f, random.Next(), 6, 0.3f);
            core = new BrushStroke(CorePath, width, random.Next(), 8, 0.25f);
            strands = new BrushStroke[3];
            for (int index = 0; index < strands.Length; index++)
            {
                float amplitude = width * (1f + (float)random.NextDouble() * 0.6f);
                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                strands[index] = new BrushStroke(along => BrushStroke.LoopAround(CorePath, along, amplitude, 4f, phase), width * 0.35f, random.Next(), 3, 0.2f);
            }
        }

        /// <summary>
        /// Where a held weapon's ends are right now, from the holder's draw position. False if the weapon wasn't drawn
        /// this frame (off screen, or not held).
        /// </summary>
        public static bool TryGetWeaponEnds(Pawn holder, Thing weapon, Vector2 gripOnSprite, Vector2 tipOnSprite, out Vector3 gripOffset, out Vector3 tipOffset)
        {
            gripOffset = Vector3.zero;
            tipOffset = Vector3.zero;
            if (!WeaponDrawRecord.TryGetCurrent(holder, out WeaponDrawRecord record) || record.weapon != weapon) return false;
            Vector3 holderPosition = holder.DrawPos;
            gripOffset = record.SpritePointToWorld(gripOnSprite) - holderPosition;
            tipOffset = record.SpritePointToWorld(tipOnSprite) - holderPosition;
            return true;
        }

        /// <summary>
        /// A guess at where a weapon held at rest would be, for when it hasn't been drawn yet.
        /// </summary>
        public static void DefaultEnds(out Vector3 gripOffset, out Vector3 tipOffset)
        {
            gripOffset = new Vector3(0.2f, 0f, -0.15f);
            tipOffset = new Vector3(0.75f, 0f, 0.4f);
        }

        private Vector3 Grip => holderPosition + gripOffset;

        private Vector3 Tip => holderPosition + tipOffset;

        /// <summary>
        /// From the middle of the holder's body to the grip, on a slight curve.
        /// </summary>
        private Vector3 FeedPath(float along)
        {
            Vector3 start = holderPosition;
            Vector3 end = Grip;
            Vector3 toEnd = end - start;
            Vector3 side = new Vector3(-toEnd.z, 0f, toEnd.x).normalized;
            Vector3 middle = (start + end) / 2f + side * bend;
            float rest = 1f - along;
            return rest * rest * start + 2f * rest * along * middle + along * along * end;
        }

        private Vector3 CorePath(float along) => Vector3.Lerp(Grip, Tip, along);

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            Follow();
            if (dissolving) DrawDissolving(drawer, progress);
            else DrawForming(drawer, progress);
        }

        private void Follow()
        {
            if (holder.Spawned) holderPosition = holder.DrawPos;
            if (weapon == null || weapon.Destroyed) return;
            if (TryGetWeaponEnds(holder, weapon, gripOnSprite(), tipOnSprite(), out Vector3 grip, out Vector3 tip))
            {
                gripOffset = grip;
                tipOffset = tip;
            }
        }

        private void DrawForming(BrushLineDrawer drawer, float progress)
        {
            feed.Draw(drawer, BrushStroke.SmoothStep(0f, 0.2f, progress), BrushStroke.SmoothStep(0.3f, 0.5f, progress), 1f, 0f);
            // breaks into chunks and fades as the crystal hardens in under it
            float broken = BrushStroke.SmoothStep(0.55f, 0.85f, progress);
            float alpha = 1f - BrushStroke.SmoothStep(0.75f, 1f, progress);
            core.Draw(drawer, BrushStroke.SmoothStep(0.1f, 0.45f, progress), 0f, alpha, broken);
            for (int index = 0; index < strands.Length; index++) strands[index].Draw(drawer, BrushStroke.SmoothStep(0.15f + 0.05f * index, 0.5f, progress), 0f, alpha * 0.9f, broken);
        }

        private void DrawDissolving(BrushLineDrawer drawer, float progress)
        {
            // the chunks join up, then pull back from the tip to the grip
            float broken = 1f - BrushStroke.SmoothStep(0f, 0.3f, progress);
            float head = 1f - BrushStroke.SmoothStep(0.3f, 0.7f, progress);
            core.Draw(drawer, head, 0f, 1f, broken);
            for (int index = 0; index < strands.Length; index++) strands[index].Draw(drawer, head, 0f, 0.9f, broken);
            // and drain from the grip back into the body
            if (progress >= 0.45f) feed.Draw(drawer, 1f - BrushStroke.SmoothStep(0.7f, 1f, progress), 0f, BrushStroke.SmoothStep(0.45f, 0.55f, progress), 0f);
        }
    }
}
