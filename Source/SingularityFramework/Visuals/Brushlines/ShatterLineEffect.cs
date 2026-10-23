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
    /// Represents a visual effect that simulates shattering lines or streams falling from a source point to the ground,
    /// typically used for rendering dynamic, animated brush strokes in a shatter or breakage visual.
    /// </summary>
    /// <remarks>The effect generates multiple falling streams, each following a curved trajectory from the
    /// source to a landing point on the ground. The appearance and spread of the streams can be customized via
    /// constructor parameters. The landing points for each stream are exposed through the landingPoints collection,
    /// which can be used for further visual effects or interactions. This effect is rendered above most other visual
    /// elements, as indicated by its AltitudeLayer.</remarks>
    public class ShatterLineEffect : BrushLineEffect
    {
        private class FallingStream
        {
            public BrushStroke stroke;
            public float delay;   // share of the effect before it breaks off
        }

        private readonly List<FallingStream> streams = new List<FallingStream>();
        public readonly List<Vector3> landingPoints = new List<Vector3>();

        public override AltitudeLayer Layer => AltitudeLayer.MoteOverhead;

        /// <summary>
        /// Initializes a new instance of the ShatterLineEffect class, creating multiple falling streams that originate
        /// from a source point and land near a specified ground center, simulating a shattering line effect.
        /// </summary>
        /// <remarks>The generated streams will have randomized positions and delays based on the provided
        /// seed, allowing for consistent visual results when the same parameters are used. The effect is intended for
        /// use in visualizations or animations where a shattering or splitting line is desired.</remarks>
        /// <param name="palette">The brush palette used to determine the colors for the effect's visual elements.</param>
        /// <param name="source">The starting position in world coordinates from which the streams originate.</param>
        /// <param name="groundCenter">The central point on the ground toward which the streams are directed.</param>
        /// <param name="side">A directional multiplier that determines the orientation of the effect. Positive or negative values control
        /// which side of the ground center the streams fan out toward.</param>
        /// <param name="streamCount">The number of individual falling streams to generate. Must be greater than zero.</param>
        /// <param name="spread">The maximum distance, in world units, that streams can deviate from the central axis, controlling the
        /// overall width of the effect.</param>
        /// <param name="width">The base width, in world units, of each stream's visual stroke.</param>
        /// <param name="seed">The seed value for the random number generator, ensuring reproducible stream placement and variation.</param>
        /// <param name="durationSeconds">The total duration, in seconds, for which the effect remains active.</param>
        public ShatterLineEffect(BrushPalette palette, Vector3 source, Vector3 groundCenter, float side, int streamCount, float spread, float width, int seed, float durationSeconds) : base(palette, durationSeconds)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < streamCount; index++)
            {
                float fan = streamCount > 1 ? index / (float)(streamCount - 1) : 0.5f;
                Vector3 start = source + new Vector3(Range(random, -0.3f, 1f) * spread * side, 0f, Range(random, -1f, 1f) * spread);
                Vector3 landing = groundCenter + new Vector3(side * (0.5f + 0.9f * fan + Range(random, -0.1f, 0.1f)), 0f, -(0.75f - 0.5f * fan) + Range(random, -0.08f, 0.08f));
                Vector3 peak = (start + landing) / 2f + new Vector3(side * 0.3f, 0f, 0.25f + 0.4f * fan);
                landingPoints.Add(landing);
                streams.Add(new FallingStream
                {
                    stroke = new BrushStroke(along => Arc(start, peak, landing, along), width * Range(random, 0.75f, 1.15f), random.Next(), 5, 0.3f),
                    delay = Range(random, 0f, 0.12f)
                });
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        private static Vector3 Arc(Vector3 start, Vector3 peak, Vector3 landing, float along)
        {
            float rest = 1f - along;
            return rest * rest * start + 2f * rest * along * peak + along * along * landing;
        }

        public override void Draw(BrushLineDrawer drawer, float progress)
        {
            foreach (FallingStream stream in streams)
            {
                float local = Mathf.Max(0f, (progress - stream.delay) / (1f - stream.delay));
                if (local <= 0f || local >= 1f) continue;
                float head = 0.1f + 0.9f * BrushStroke.SmoothStep(0f, 0.5f, local);
                float tail = BrushStroke.SmoothStep(0.35f, 0.95f, local);
                // crystal chunks at first, running together into liquid as they fall
                float broken = 0.8f * (1f - BrushStroke.SmoothStep(0.05f, 0.4f, local));
                stream.stroke.Draw(drawer, head, tail, 1f, broken);
            }
        }
    }
}
