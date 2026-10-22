using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A weapon turned so its length runs left to right, squeezed along its length. Pieces are assembled here at their
    /// normal proportions, so once it's stretched back every piece comes out long and thin, laid along the weapon.
    /// </summary>
    public class WeaponSpace
    {
        private const float MaximumCompression = 2.5f;

        public readonly WeaponProfile profile;
        public readonly float compression;
        public readonly int width;
        public readonly int height;
        public readonly Affine2D toSpace;   // weapon texture pixels to space pixels
        public readonly Affine2D toImage;   // space pixels back to weapon texture pixels
        public readonly PixelMask silhouette;   // the given silhouette, in space
        public readonly int imageWidth;
        public readonly int imageHeight;

        /// <summary>
        /// "silhouette": what to bring into the space (the whole weapon, or just its body without outline).
        /// "margin": empty room around it, in space pixels. Long thin weapons are squeezed the most; a chunky gun barely,
        /// or its grip and magazine would squeeze to nothing. "minimumCompression" forces at least that much squeeze.
        /// </summary>
        public WeaponSpace(WeaponProfile profile, PixelMask silhouette, int margin, float minimumCompression)
        {
            this.profile = profile;
            imageWidth = silhouette.width;
            imageHeight = silhouette.height;
            float typicalThickness = Mathf.Max(1f, profile.MedianHalfThickness() * 2f);
            compression = Mathf.Clamp(profile.Length / typicalThickness / 4f, minimumCompression, MaximumCompression);
            width = Mathf.Max(1, (int)(profile.Length / compression + 2 * margin));
            height = Mathf.Max(1, (int)(profile.MaximumHalfThickness() * 2f + 2 * margin));

            // image -> space: along the axis (squeezed) becomes x, across it becomes y, the weapon's start at the margin
            Vector2 offset = new Vector2(margin - profile.start / compression, height / 2f);
            Vector2 alongRow = profile.axis / compression;
            Vector2 acrossRow = profile.normal;
            toSpace = new Affine2D(
                alongRow.x, alongRow.y, offset.x - Vector2.Dot(alongRow, profile.center),
                acrossRow.x, acrossRow.y, offset.y - Vector2.Dot(acrossRow, profile.center));
            toSpace.TryInvert(out toImage);
            this.silhouette = Bring(silhouette);
        }

        /// <summary>
        /// A mask from the weapon texture, brought into this space (blended between pixels, so thin parts survive).
        /// </summary>
        public PixelMask Bring(PixelMask imageMask)
        {
            var result = new PixelMask(width, height);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Vector2 source = toImage.Apply(new Vector2(column + 0.5f, row + 0.5f));
                    result.cells[row * width + column] = Coverage(imageMask, source.x - 0.5f, source.y - 0.5f) > 0.5f;
                }
            }
            return result;
        }

        private static float Coverage(PixelMask mask, float x, float y)
        {
            int left = Mathf.FloorToInt(x);
            int top = Mathf.FloorToInt(y);
            float rightShare = x - left;
            float bottomShare = y - top;
            float coverage = 0f;
            if (mask[left, top]) coverage += (1f - rightShare) * (1f - bottomShare);
            if (mask[left + 1, top]) coverage += rightShare * (1f - bottomShare);
            if (mask[left, top + 1]) coverage += (1f - rightShare) * bottomShare;
            if (mask[left + 1, top + 1]) coverage += rightShare * bottomShare;
            return coverage;
        }

        /// <summary>
        /// A picture built in this space, put back onto the weapon texture.
        /// </summary>
        public KitbashCanvas BackToImage(KitbashCanvas built)
        {
            return built.Transformed(toImage, imageWidth, imageHeight);
        }

        /// <summary>
        /// The column of the business end's tip in this space.
        /// </summary>
        public bool TryGetTipColumn(PixelMask shape, out int tipColumn, out int otherEndColumn)
        {
            tipColumn = otherEndColumn = -1;
            int first = -1, last = -1;
            for (int column = 0; column < shape.width; column++)
            {
                if (!ColumnHasAny(shape, column)) continue;
                if (first < 0) first = column;
                last = column;
            }
            if (first < 0) return false;
            tipColumn = profile.tipSign > 0 ? last : first;
            otherEndColumn = profile.tipSign > 0 ? first : last;
            return true;
        }

        /// <summary>
        /// The shape with its business end narrowed evenly to a clean point over the last "share" of its length, starting
        /// from the shape's own width there so the point doesn't flare into barbs.
        /// </summary>
        public PixelMask PointedEnd(PixelMask shape, float share)
        {
            if (!TryGetTipColumn(shape, out int tipColumn, out int otherEndColumn)) return shape;
            int direction = profile.tipSign > 0 ? 1 : -1;
            int stretch = (int)(Mathf.Abs(tipColumn - otherEndColumn) * share);
            var halves = new List<float>();
            var centres = new List<float>();
            for (int step = 0; step <= stretch; step++)
            {
                int column = tipColumn - direction * step;
                if (!TryGetColumnExtent(shape, column, out int topRow, out int bottomRow))
                {
                    halves.Add(0f);
                    continue;
                }
                halves.Add((bottomRow - topRow) / 2f);
                centres.Add((topRow + bottomRow) / 2f);
            }
            float centre = centres.Count > 0 ? Median(centres) : shape.height / 2f;
            // the width where the point begins: the last fifth of the stretch, away from the tip
            var startWidths = new List<float>();
            for (int index = Mathf.Max(0, halves.Count - Mathf.Max(1, stretch / 5)); index < halves.Count; index++) if (halves[index] > 0f) startWidths.Add(halves[index]);
            float widest = startWidths.Count > 0 ? Median(startWidths) : 0f;

            PixelMask pointed = shape.Copy();
            for (int step = 0; step <= stretch; step++)
            {
                int column = tipColumn - direction * step;
                if (column < 0 || column >= shape.width) continue;
                float half = widest * step / Mathf.Max(1, stretch);
                for (int row = 0; row < shape.height; row++) pointed[column, row] = Mathf.Abs(row - centre) <= half;
            }
            return pointed;
        }

        private static bool ColumnHasAny(PixelMask mask, int column)
        {
            for (int row = 0; row < mask.height; row++) if (mask.cells[row * mask.width + column]) return true;
            return false;
        }

        public static bool TryGetColumnExtent(PixelMask mask, int column, out int topRow, out int bottomRow)
        {
            topRow = bottomRow = -1;
            if (column < 0 || column >= mask.width) return false;
            for (int row = 0; row < mask.height; row++)
            {
                if (!mask.cells[row * mask.width + column]) continue;
                if (topRow < 0) topRow = row;
                bottomRow = row;
            }
            return topRow >= 0;
        }

        private static float Median(List<float> values)
        {
            var sorted = new List<float>(values);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }
    }
}
