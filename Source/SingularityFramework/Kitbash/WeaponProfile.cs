using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A weapon texture, straightened: its long axis, which end is the business end, and its centre line and thickness at
    /// every step along it. Works in canvas pixels (row 0 at the top).
    /// </summary>
    public class WeaponProfile
    {
        private const byte VisibleAlpha = 25;
        private const int OutlineBrightness = 22;   // darker than this (in every channel) counts as black linework

        public readonly Vector2 center;
        public readonly Vector2 axis;     // along the weapon
        public readonly Vector2 normal;   // across it
        public readonly float start;      // the two ends, as distances along the axis from the centre
        public readonly float end;
        public readonly int firstStep;
        public readonly float[] middle;          // the centre line's offset across the axis, at each step
        public readonly float[] halfThickness;   // half the weapon's thickness, at each step
        public readonly int tipSign;             // +1 if the business end is at "end", -1 if it's at "start"

        public float Length => end - start;

        public WeaponProfile(PixelMask silhouette)
        {
            var spread = new PointSpread.Accumulator();
            for (int index = 0; index < silhouette.cells.Length; index++) if (silhouette.cells[index]) spread.Add(index % silhouette.width, index / silhouette.width);
            PointSpread result = spread.Result();
            center = result.center;
            axis = result.major;
            normal = new Vector2(-axis.y, axis.x);

            // every visible pixel's position along the weapon and across it
            var alongs = new List<float>();
            var acrosses = new List<float>();
            for (int index = 0; index < silhouette.cells.Length; index++)
            {
                if (!silhouette.cells[index]) continue;
                Vector2 offset = new Vector2(index % silhouette.width, index / silhouette.width) - center;
                alongs.Add(Vector2.Dot(offset, axis));
                acrosses.Add(Vector2.Dot(offset, normal));
            }
            float lowestAlong = 0f, highestAlong = 0f;
            if (alongs.Count > 0)
            {
                lowestAlong = float.MaxValue;
                highestAlong = float.MinValue;
                foreach (float along in alongs)
                {
                    lowestAlong = Mathf.Min(lowestAlong, along);
                    highestAlong = Mathf.Max(highestAlong, along);
                }
            }
            start = lowestAlong;
            end = highestAlong;

            firstStep = (int)start;
            int stepCount = (int)end - firstStep + 1;
            var lowest = new float[stepCount];
            var highest = new float[stepCount];
            for (int step = 0; step < stepCount; step++)
            {
                lowest[step] = float.MaxValue;
                highest[step] = float.MinValue;
            }
            for (int index = 0; index < alongs.Count; index++)
            {
                int step = Mathf.Clamp(Mathf.RoundToInt(alongs[index]) - firstStep, 0, stepCount - 1);
                lowest[step] = Mathf.Min(lowest[step], acrosses[index]);
                highest[step] = Mathf.Max(highest[step], acrosses[index]);
            }

            middle = new float[stepCount];
            halfThickness = new float[stepCount];
            var measured = new List<int>();
            for (int step = 0; step < stepCount; step++)
            {
                if (lowest[step] > highest[step]) continue;
                middle[step] = (lowest[step] + highest[step]) / 2f;
                halfThickness[step] = (highest[step] - lowest[step]) / 2f + 0.5f;
                measured.Add(step);
            }
            FillGaps(measured);
            middle = Smooth(middle, 5);
            // the business end: RimWorld weapon textures point right or up-right, so it's that end
            tipSign = Vector2.Dot(axis, new Vector2(1f, -1f)) >= 0f ? 1 : -1;
        }

        /// <summary>
        /// Steps with no pixels (where the weapon is thinner than a pixel at an angle) take their values from their neighbours.
        /// </summary>
        private void FillGaps(List<int> measured)
        {
            if (measured.Count == 0) return;
            for (int step = 0; step < middle.Length; step++)
            {
                if (halfThickness[step] > 0f) continue;
                int before = -1, after = -1;
                foreach (int index in measured)
                {
                    if (index < step) before = index;
                    if (index > step && after < 0) after = index;
                }
                if (before < 0) before = after;
                if (after < 0) after = before;
                float share = after == before ? 0f : (step - before) / (float)(after - before);
                middle[step] = middle[before] + (middle[after] - middle[before]) * share;
                halfThickness[step] = halfThickness[before] + (halfThickness[after] - halfThickness[before]) * share;
            }
        }

        private static float[] Smooth(float[] values, int window)
        {
            var smoothed = new float[values.Length];
            int half = window / 2;
            for (int index = 0; index < values.Length; index++)
            {
                float sum = 0f;
                for (int offset = -half; offset <= half; offset++) sum += values[Mathf.Clamp(index + offset, 0, values.Length - 1)];
                smoothed[index] = sum / window;
            }
            return smoothed;
        }

        /// <summary>
        /// The centre line's offset and the half-thickness at a distance along the axis.
        /// </summary>
        public void At(float along, out float middleOffset, out float half)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(along - firstStep), 0, middle.Length - 1);
            middleOffset = middle[step];
            half = halfThickness[step];
        }

        public Vector2 ToImage(float along, float across) => center + axis * along + normal * across;

        public float MedianHalfThickness()
        {
            var sorted = new List<float>(halfThickness);
            sorted.Sort();
            return sorted.Count == 0 ? 0f : sorted[sorted.Count / 2];
        }

        public float MaximumHalfThickness()
        {
            float maximum = 0f;
            foreach (float half in halfThickness) maximum = Mathf.Max(maximum, half);
            return maximum;
        }

        // ----- reading the texture -----

        /// <summary>
        /// Everywhere the texture shows something.
        /// </summary>
        public static PixelMask Visible(Color32[] pixels, int width, int height)
        {
            var mask = new PixelMask(width, height);
            for (int index = 0; index < pixels.Length; index++) mask.cells[index] = pixels[index].a > VisibleAlpha;
            return mask;
        }

        /// <summary>
        /// The weapon without its black outline: what it's made of, not the lines drawn around and inside it, so a slit
        /// drawn in outline stays open and a copy is as slim as the real thing. Black thicker than a drawn line is a black
        /// part of the weapon (a dark blade back), so it stays. "lineWidth": about how thick the linework is, in pixels.
        /// </summary>
        public static PixelMask Body(Color32[] pixels, int width, int height, int lineWidth)
        {
            PixelMask visible = Visible(pixels, width, height);
            var black = new PixelMask(width, height);
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                black.cells[index] = visible.cells[index] && pixel.r < OutlineBrightness && pixel.g < OutlineBrightness && pixel.b < OutlineBrightness;
            }
            PixelMask blackParts = black.Opened(lineWidth + 1);
            // drop the anti-aliased crumbs the outline leaves behind
            return visible.AndNot(black).Or(blackParts).Opened(1);
        }
    }
}
