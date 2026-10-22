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
    /// A brushy stream pulled from a spot on the ground into a thing (a pawn), along a gentle arc, with two thinner
    /// strands looping around it. Its head shoots out to the target, then its tail is pulled in after it. A big source
    /// can first rise in short streaks, like blood lifting off a pool.
    /// </summary>
    public class StreamLineEffect : BrushLineEffect
    {
        private readonly Vector3 source;
        private readonly Thing target;
        private readonly float width;
        private readonly bool risesFirst;
        private readonly float bend;
        private readonly BrushStroke stream;
        private readonly BrushStroke[] strands;
        private Vector3 lastTargetPosition;

        public StreamLineEffect(BrushPalette palette, Vector3 source, Thing target, float width, bool risesFirst, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            this.source = source;
            this.target = target;
            this.width = width;
            this.risesFirst = risesFirst;
            lastTargetPosition = target.DrawPos;
            var random = new System.Random(seed);
            bend = ((float)random.NextDouble() * 2f - 1f) * 0.9f;
            stream = new BrushStroke(Path, width, random.Next(), 8, 0.4f);
            strands = new BrushStroke[2];
            for (int index = 0; index < strands.Length; index++)
            {
                float amplitude = width * (0.9f + (float)random.NextDouble() * 0.7f);
                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                strands[index] = new BrushStroke(along => BrushStroke.LoopAround(Path, along, amplitude, 3f, phase), width * 0.4f, random.Next(), 3, 0.2f);
            }
        }

        /// <summary>
        /// A gentle arc from the source to wherever the target is now (it may be moving).
        /// </summary>
        private Vector3 Path(float along)
        {
            Vector3 end = lastTargetPosition;
            Vector3 toEnd = end - source;
            Vector3 side = new Vector3(-toEnd.z, 0f, toEnd.x).normalized;
            Vector3 middle = (source + end) / 2f + side * bend;
            float rest = 1f - along;
            return rest * rest * source + 2f * rest * along * middle + along * along * end;
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            if (target != null && target.Spawned) lastTargetPosition = target.DrawPos;
            if (risesFirst)
            {
                // short streaks lifting off the source, before the stream pulls it away
                float rise = BrushStroke.SmoothStep(0f, 0.25f, progress) * (1f - BrushStroke.SmoothStep(0.3f, 0.45f, progress));
                for (int streak = 0; streak < 5; streak++)
                {
                    float length = width * 3f * rise * (0.6f + 0.4f * Mathf.Sin(streak * 2.1f + 1f));
                    if (length <= 0.01f) continue;
                    Vector3 foot = source + new Vector3((streak - 2) * width * 0.6f, 0f, 0f);
                    float halfThickness = width * 0.12f;
                    drawer.AddQuad(1, 0.9f, foot + new Vector3(-halfThickness, 0f, 0f), foot + new Vector3(-halfThickness, 0f, length), foot + new Vector3(halfThickness, 0f, length), foot + new Vector3(halfThickness, 0f, 0f));
                }
            }
            float head = BrushStroke.SmoothStep(0.05f, 0.45f, progress);
            float tail = BrushStroke.SmoothStep(0.55f, 0.95f, progress);
            stream.Draw(drawer, head, tail, 1f, 0f);
            for (int index = 0; index < strands.Length; index++) strands[index].Draw(drawer, BrushStroke.SmoothStep(0.1f + 0.05f * index, 0.5f, progress), BrushStroke.SmoothStep(0.5f, 0.9f, progress), 0.9f, 0f);
        }
    }
}
