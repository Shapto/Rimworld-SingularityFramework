using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// The picture being built: pieces are drawn onto it one by one, then it's cleaned up (hard edges, seams closed).
    /// Row 0 is the top of the image.
    /// </summary>
    public class KitbashCanvas
    {
        public const byte SolidAlpha = 100;

        public readonly int width;
        public readonly int height;
        public readonly Color32[] pixels;

        public KitbashCanvas(int width, int height)
        {
            this.width = width;
            this.height = height;
            pixels = new Color32[width * height];
        }

        /// <summary>
        /// For each pixel, when it appears while the picture grows in, as a share of the whole growth (0 to 1; above 1
        /// never). Null if the picture doesn't grow in.
        /// </summary>
        public float[] arrival;

        /// <summary>
        /// Starts recording when each pixel arrives, for pictures that grow into place.
        /// </summary>
        public void EnableGrowth()
        {
            arrival = new float[pixels.Length];
            for (int index = 0; index < arrival.Length; index++) arrival[index] = NeverArrives;
        }

        public const float NeverArrives = 2f;

        /// <summary>
        /// Draws a piece, placed by "placement" (piece pixels to canvas pixels), over what's already there.
        /// </summary>
        public void DrawPiece(KitbashPiece piece, Affine2D placement)
        {
            DrawGrowingPiece(piece, placement, Vector2.zero, 0f, 0f);
        }

        /// <summary>
        /// Draws a piece that grows out from "origin" (a canvas point, usually where it attaches): it starts appearing at
        /// "start" and is fully grown "span" later (both shares of the whole growth), its pixels arriving by how far they
        /// are from the origin. Only recorded if growth is enabled.
        /// </summary>
        public void DrawGrowingPiece(KitbashPiece piece, Affine2D placement, Vector2 origin, float start, float span)
        {
            if (!placement.TryInvert(out Affine2D canvasToPiece)) return;
            Vector2[] corners = placement.ApplyAll(new[]
            {
                new Vector2(0f, 0f), new Vector2(piece.width, 0f), new Vector2(piece.width, piece.height), new Vector2(0f, piece.height)
            });
            ConvexPolygon placedOutline = new ConvexPolygon(corners);
            placedOutline.GetBounds(out float minimumX, out float minimumY, out float maximumX, out float maximumY);
            int firstColumn = Mathf.Max(0, Mathf.FloorToInt(minimumX) - 1);
            int lastColumn = Mathf.Min(width - 1, Mathf.CeilToInt(maximumX) + 1);
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(minimumY) - 1);
            int lastRow = Mathf.Min(height - 1, Mathf.CeilToInt(maximumY) + 1);

            // how far the piece reaches from its origin, so its far end arrives last
            float reach = 1f;
            if (arrival != null) foreach (Vector2 corner in placedOutline.points) reach = Mathf.Max(reach, Vector2.Distance(corner, origin));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    Vector2 piecePoint = canvasToPiece.Apply(new Vector2(column + 0.5f, row + 0.5f));
                    Color32 color = piece.Sample(piecePoint.x, piecePoint.y);
                    if (color.a == 0) continue;
                    int index = row * width + column;
                    pixels[index] = Over(color, pixels[index]);
                    if (arrival == null) continue;
                    float pixelArrival = start + span * Vector2.Distance(new Vector2(column + 0.5f, row + 0.5f), origin) / reach;
                    arrival[index] = Mathf.Min(arrival[index], pixelArrival);
                }
            }
        }

        /// <summary>
        /// Standard "over" blending: the new colour in front, the old one showing through what's see-through of it.
        /// </summary>
        private static Color32 Over(Color32 front, Color32 back)
        {
            float frontAlpha = front.a / 255f;
            float backAlpha = back.a / 255f;
            float alpha = frontAlpha + backAlpha * (1f - frontAlpha);
            if (alpha <= 0f) return new Color32(0, 0, 0, 0);
            return new Color32(Blend(front.r, back.r, frontAlpha, backAlpha, alpha), Blend(front.g, back.g, frontAlpha, backAlpha, alpha), Blend(front.b, back.b, frontAlpha, backAlpha, alpha), (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
        }

        private static byte Blend(byte frontChannel, byte backChannel, float frontAlpha, float backAlpha, float alpha)
        {
            return (byte)Mathf.Clamp((frontChannel * frontAlpha + backChannel * backAlpha * (1f - frontAlpha)) / alpha, 0f, 255f);
        }

        public PixelMask SolidMask()
        {
            var mask = new PixelMask(width, height);
            for (int index = 0; index < pixels.Length; index++) mask.cells[index] = pixels[index].a > SolidAlpha;
            return mask;
        }

        /// <summary>
        /// Hard edges: every pixel becomes fully solid or fully see-through.
        /// </summary>
        public void Harden()
        {
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                pixel.a = pixel.a > SolidAlpha ? (byte)255 : (byte)0;
                pixels[index] = pixel;
            }
        }

        /// <summary>
        /// Closes the hairline seams between neighbouring pieces and tiny holes inside the shape, with the colour of the
        /// nearest crystal, then hardens the edges.
        /// </summary>
        public void CloseSeams(int seamWidth, int largestHoleArea)
        {
            PixelMask solid = SolidMask();
            PixelMask closed = solid.Closed(seamWidth);
            PixelMask holes = closed.HolesFilled().AndNot(closed);
            int[] holeLabels = holes.Label(out int holeCount);
            var holeSizes = new int[holeCount + 1];
            foreach (int label in holeLabels) holeSizes[label]++;

            var fill = new PixelMask(width, height);
            for (int index = 0; index < pixels.Length; index++)
            {
                if (solid.cells[index]) continue;
                bool smallHole = holeLabels[index] != 0 && holeSizes[holeLabels[index]] < largestHoleArea;
                fill.cells[index] = closed.cells[index] || smallHole;
            }
            FillFromNearest(solid, fill);
            Harden();
        }

        /// <summary>
        /// Paints every pixel of "bare" that the crystals left uncovered with the colour of the nearest crystal, so the
        /// result is one solid piece.
        /// </summary>
        public void FillBare(PixelMask bare)
        {
            PixelMask solid = SolidMask();
            FillFromNearest(solid, bare.AndNot(solid));
            Harden();
        }

        private void FillFromNearest(PixelMask solid, PixelMask fill)
        {
            if (!fill.Any() || !solid.Any()) return;
            int[] nearest = solid.NearestSolid();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (!fill.cells[index] || nearest[index] < 0) continue;
                Color32 color = pixels[nearest[index]];
                color.a = 255;
                pixels[index] = color;
            }
        }

        /// <summary>
        /// Clears everything outside "keep" (cuts a shape back out, so slits and gaps stay open).
        /// </summary>
        public void CutTo(PixelMask keep)
        {
            for (int index = 0; index < pixels.Length; index++) if (!keep.cells[index]) pixels[index] = new Color32(0, 0, 0, 0);
        }

        /// <summary>
        /// This canvas moved, turned or stretched onto a new canvas of the given size ("placement": this canvas's pixels to
        /// the new canvas's pixels).
        /// </summary>
        public KitbashCanvas Transformed(Affine2D placement, int newWidth, int newHeight)
        {
            var result = new KitbashCanvas(newWidth, newHeight);
            if (!placement.TryInvert(out Affine2D newToThis)) return result;
            for (int row = 0; row < newHeight; row++)
            {
                for (int column = 0; column < newWidth; column++)
                {
                    Vector2 source = newToThis.Apply(new Vector2(column + 0.5f, row + 0.5f));
                    result.pixels[row * newWidth + column] = Sample(source.x, source.y);
                }
            }
            return result;
        }

        /// <summary>
        /// The colour at a point, blended between the four nearest pixels (see-through pixels don't darken the blend).
        /// </summary>
        public Color32 Sample(float x, float y)
        {
            return ColorSampling.Bilinear(pixels, width, height, x, y);
        }

        /// <summary>
        /// Draws another canvas on top of this one, shifted by an offset.
        /// </summary>
        public void Composite(KitbashCanvas front, int columnOffset, int rowOffset)
        {
            for (int row = 0; row < front.height; row++)
            {
                int targetRow = row + rowOffset;
                if (targetRow < 0 || targetRow >= height) continue;
                for (int column = 0; column < front.width; column++)
                {
                    int targetColumn = column + columnOffset;
                    if (targetColumn < 0 || targetColumn >= width) continue;
                    Color32 color = front.pixels[row * front.width + column];
                    if (color.a == 0) continue;
                    int index = targetRow * width + targetColumn;
                    pixels[index] = Over(color, pixels[index]);
                    if (arrival != null && front.arrival != null) arrival[index] = Mathf.Min(arrival[index], front.arrival[row * front.width + column]);
                }
            }
        }
    }
}
