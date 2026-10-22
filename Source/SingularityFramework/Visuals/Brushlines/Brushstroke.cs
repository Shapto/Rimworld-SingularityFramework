using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Visuals.Brushlines
{
    /// <summary>
    /// A tapered, dry-brushed stroke along a path in the world: several thin bristles side by side, each with its own
    /// weight and its own skips (more toward the edges), so the stroke's edges fray and its middle stays solid.
    /// The path can change every frame (to follow a moving pawn); the bristles stay the same.
    /// </summary>
    public class BrushStroke
    {
        private const int Steps = 40;   // samples along the stroke

        private struct Bristle
        {
            public float across;    // -1 to 1, across the stroke
            public float edge;      // 0 in the middle, 1 at the edge
            public float start;     // where along the stroke it starts and stops (ragged ends)
            public float stop;
            public float seed;
            public float weight;
            public List<Vector2> skips;   // (where it skips, how long)
        }

        private readonly Func<float, Vector3> path;
        private readonly float width;
        private readonly List<Bristle> bristles = new List<Bristle>();

        /// <summary>
        /// "path": the stroke's centre line, from 0 (where it starts) to 1 (its far end), in world positions.
        /// "width": how wide it is, in cells. "ragged": how ragged its ends and edges are (0 to about 0.5).
        /// </summary>
        public BrushStroke(Func<float, Vector3> path, float width, int seed, int bristleCount, float ragged)
        {
            this.path = path;
            this.width = width;
            var random = new System.Random(seed);
            for (int index = 0; index < bristleCount; index++)
            {
                float across = -1f + 2f * index / Mathf.Max(1, bristleCount - 1) + Range(random, -0.12f, 0.12f);
                float edge = Mathf.Min(1f, Mathf.Abs(across));
                var skips = new List<Vector2>();
                int skipCount = (int)(edge * 6f + Range(random, 0f, 2f));
                for (int skip = 0; skip < skipCount; skip++) skips.Add(new Vector2(Range(random, 0f, 1f), Range(random, 0.02f, 0.08f)));
                bristles.Add(new Bristle
                {
                    across = across,
                    edge = edge,
                    start = Range(random, 0f, ragged * edge),
                    stop = 1f - Range(random, 0f, ragged * edge),
                    seed = Range(random, 0f, 1000f),
                    weight = Range(random, 0.6f, 1.4f),
                    skips = skips
                });
            }
        }

        private static float Range(System.Random random, float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        /// <summary>
        /// Draws the stretch of the stroke from "tail" to "head" (both 0 to 1 along it): a stream shooting out has its
        /// head moving ahead, and its tail catching up as it's pulled in. "broken": 0 a solid stream, 1 broken into a
        /// dashed row of chunks.
        /// </summary>
        public void Draw(BrushLineDrawer drawer, float head, float tail, float alpha, float broken)
        {
            if (head <= tail || alpha <= 0f) return;
            foreach (Bristle bristle in bristles)
            {
                int band = drawer.palette.BandFor(bristle.edge);
                float thickness = width / bristles.Count * 1.6f * (1.2f - 0.5f * bristle.edge) * bristle.weight;
                bool hasPrevious = false;
                Vector3 previousLeft = Vector3.zero, previousRight = Vector3.zero;
                for (int step = 0; step <= Steps; step++)
                {
                    float along = tail + (head - tail) * step / Steps;
                    if (!Visible(bristle, along, broken))
                    {
                        hasPrevious = false;
                        continue;
                    }
                    Frame(along, out Vector3 center, out Vector3 normal);
                    // tapered: thin at the leading end and where it's being pulled in at the tail
                    float taper = head < 0.999f ? 1f - SmoothStep(head - 0.12f, head, along) : 1f;
                    taper *= SmoothStep(tail, tail + 0.05f, along);
                    Vector3 middle = center + normal * (bristle.across * width / 2f * taper);
                    float halfThickness = thickness / 2f * Mathf.Max(taper, 0.35f);
                    Vector3 left = middle + normal * halfThickness;
                    Vector3 right = middle - normal * halfThickness;
                    if (hasPrevious) drawer.AddQuad(band, alpha, previousLeft, left, right, previousRight);
                    previousLeft = left;
                    previousRight = right;
                    hasPrevious = true;
                }
            }
        }

        private static bool Visible(Bristle bristle, float along, float broken)
        {
            if (along < bristle.start || along > bristle.stop) return false;
            foreach (Vector2 skip in bristle.skips) if (along >= skip.x && along <= skip.x + skip.y) return false;
            // dashes: a row of chunks along the stroke, gaps opening as it breaks
            float dash = (Mathf.Sin(along * 38f + bristle.seed) + 1f) / 2f;
            return broken <= 0f || dash >= broken * 0.75f;
        }

        /// <summary>
        /// The stroke's centre and the direction across it (on the ground) at a point along it.
        /// </summary>
        private void Frame(float along, out Vector3 center, out Vector3 normal)
        {
            center = path(along);
            Vector3 ahead = along < 0.998f ? path(along + 0.002f) - center : center - path(along - 0.002f);
            ahead.y = 0f;
            Vector3 direction = ahead.sqrMagnitude > 1e-10f ? ahead.normalized : Vector3.forward;
            normal = new Vector3(-direction.z, 0f, direction.x);
        }

        public static float SmoothStep(float edge0, float edge1, float value)
        {
            float share = Mathf.Clamp01((value - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
            return share * share * (3f - 2f * share);
        }

        /// <summary>
        /// A point on a strand looping around another path: it swings to either side of it and back and forth along it,
        /// so it crosses the path and itself in loops and Xs. Thin at both ends.
        /// </summary>
        public static Vector3 LoopAround(Func<float, Vector3> around, float along, float amplitude, float frequency, float phase)
        {
            Vector3 center = around(along);
            Vector3 ahead = along < 0.99f ? around(along + 0.01f) - center : center - around(along - 0.01f);
            ahead.y = 0f;
            Vector3 direction = ahead.sqrMagnitude > 1e-10f ? ahead.normalized : Vector3.forward;
            Vector3 normal = new Vector3(-direction.z, 0f, direction.x);
            float angle = along * frequency * Mathf.PI + phase;
            float taper = Mathf.Sin(Mathf.PI * along);
            return center + normal * (Mathf.Sin(angle) * amplitude * taper) + direction * (Mathf.Cos(angle) * amplitude * 0.6f * taper);
        }
    }
}
