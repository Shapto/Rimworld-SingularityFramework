using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// How big a piece is meant to be used: big pieces outline and fill first, medium and small fill what's left.
    /// </summary>
    public enum KitbashSize
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// A piece's own axes, in its pixels: its middle, its long direction, the direction across that, and how long and
    /// thick it is along each. Used to lay a piece along a direction at a chosen length and thickness.
    /// </summary>
    public struct PieceFrame
    {
        public Affine2D mirror;     // identity, or the flip that mirrors the piece
        public Vector2 center;
        public Vector2 major;
        public Vector2 minor;
        public float length;
        public float thickness;
    }

    /// <summary>
    /// One piece to build with (one crystal): its pixels, and its outline as a handful of straight edges. A piece is only
    /// ever scaled, rotated, mirrored and stretched, never reshaped, so the result is always made of the artist's pieces.
    /// </summary>
    public class KitbashPiece
    {
        private const byte SolidAlpha = 100;
        private const float OutlineTolerance = 0.03f;   // outline corners closer than this share of the piece's size are smoothed away

        public readonly string name;
        public readonly KitbashSize size;
        public readonly bool pointed;   // a long shard that can stand up as a point
        public readonly int width;
        public readonly int height;
        public readonly Color32[] pixels;   // row 0 is the top
        public readonly ConvexPolygon outline;

        public KitbashPiece(string name, KitbashSize size, bool pointed, Color32[] topDownPixels, int width, int height)
        {
            this.name = name;
            this.size = size;
            this.pointed = pointed;
            Crop(topDownPixels, width, height, out this.pixels, out this.width, out this.height);
            outline = TraceOutline();
        }

        /// <summary>
        /// Cuts away the empty border, so the piece's pixels start where the crystal does.
        /// </summary>
        private static void Crop(Color32[] source, int sourceWidth, int sourceHeight, out Color32[] cropped, out int croppedWidth, out int croppedHeight)
        {
            int left = sourceWidth, top = sourceHeight, right = -1, bottom = -1;
            for (int row = 0; row < sourceHeight; row++)
            {
                for (int column = 0; column < sourceWidth; column++)
                {
                    if (source[row * sourceWidth + column].a == 0) continue;
                    left = Mathf.Min(left, column);
                    right = Mathf.Max(right, column);
                    top = Mathf.Min(top, row);
                    bottom = Mathf.Max(bottom, row);
                }
            }
            if (right < 0)
            {
                cropped = new Color32[1];
                croppedWidth = croppedHeight = 1;
                return;
            }
            croppedWidth = right - left + 1;
            croppedHeight = bottom - top + 1;
            cropped = new Color32[croppedWidth * croppedHeight];
            for (int row = 0; row < croppedHeight; row++)
            {
                for (int column = 0; column < croppedWidth; column++) cropped[row * croppedWidth + column] = source[(row + top) * sourceWidth + column + left];
            }
        }

        /// <summary>
        /// The crystal's outline: the convex hull of its solid pixels, simplified to its real edges.
        /// Only the leftmost and rightmost solid pixel of each row can be on the hull, so only those are used.
        /// </summary>
        private ConvexPolygon TraceOutline()
        {
            var rowEnds = new List<Vector2>();
            for (int row = 0; row < height; row++)
            {
                int first = -1, last = -1;
                for (int column = 0; column < width; column++)
                {
                    if (pixels[row * width + column].a <= SolidAlpha) continue;
                    if (first < 0) first = column;
                    last = column;
                }
                if (first < 0) continue;
                rowEnds.Add(new Vector2(first + 0.5f, row + 0.5f));
                rowEnds.Add(new Vector2(last + 0.5f, row + 0.5f));
            }
            ConvexPolygon hull = ConvexPolygon.Hull(rowEnds);
            List<Vector2> simplified = ConvexPolygon.SimplifyRing(hull.points, Mathf.Max(width, height) * OutlineTolerance);
            return new ConvexPolygon(simplified.ToArray());
        }

        public Affine2D MirrorTransform(bool mirrored)
        {
            return mirrored ? Affine2D.MirrorHorizontal(width) : Affine2D.Identity;
        }

        /// <summary>
        /// The piece's outline, flipped if "mirrored".
        /// </summary>
        public Vector2[] OutlinePoints(bool mirrored)
        {
            return MirrorTransform(mirrored).ApplyAll(outline.points);
        }

        /// <summary>
        /// The piece's long and short axes, from the spread of its outline corners.
        /// </summary>
        public PieceFrame Frame(bool mirrored)
        {
            Vector2[] points = OutlinePoints(mirrored);
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in points) center += point;
            center /= points.Length;

            float spreadXX = 0f, spreadXY = 0f, spreadYY = 0f;
            foreach (Vector2 point in points)
            {
                Vector2 offset = point - center;
                spreadXX += offset.x * offset.x;
                spreadXY += offset.x * offset.y;
                spreadYY += offset.y * offset.y;
            }
            // the long axis is the direction the corners spread out the most (the main eigenvector of their spread)
            float angle = 0.5f * Mathf.Atan2(2f * spreadXY, spreadXX - spreadYY);
            Vector2 major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 minor = new Vector2(-major.y, major.x);

            float minimumAlong = float.MaxValue, maximumAlong = float.MinValue, minimumAcross = float.MaxValue, maximumAcross = float.MinValue;
            foreach (Vector2 point in points)
            {
                float along = Vector2.Dot(point - center, major);
                float across = Vector2.Dot(point - center, minor);
                minimumAlong = Mathf.Min(minimumAlong, along);
                maximumAlong = Mathf.Max(maximumAlong, along);
                minimumAcross = Mathf.Min(minimumAcross, across);
                maximumAcross = Mathf.Max(maximumAcross, across);
            }
            return new PieceFrame
            {
                mirror = MirrorTransform(mirrored),
                center = center,
                major = major,
                minor = minor,
                length = maximumAlong - minimumAlong,
                thickness = maximumAcross - minimumAcross
            };
        }

        /// <summary>
        /// The piece's colour at a point in its own pixels, blended between the four nearest pixels.
        /// </summary>
        public Color32 Sample(float x, float y)
        {
            return ColorSampling.Bilinear(pixels, width, height, x, y);
        }
    }
}
