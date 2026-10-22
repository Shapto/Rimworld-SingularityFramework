using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A head or body unrolled flat, so something can be built on it once and then wrapped back around it for every facing.
    /// The surface is treated as an upright cylinder whose width at each row comes from the front-facing texture.
    /// x is the angle around it (0 = the front, +90 = the pawn's left side, 180 = the back), y is the same row as on the
    /// texture. Rows are in canvas pixels, row 0 at the top.
    /// </summary>
    public class SurfaceSpace
    {
        public readonly float angleMinimum;
        public readonly float angleMaximum;
        public readonly int top;
        public readonly int bottom;
        public readonly float surfaceHeight;
        public readonly float referenceRadius;   // half the widest width, seen from the front
        public readonly float pixelsPerDegree;
        public readonly int width;
        public readonly int height;
        public readonly float[] radiusByRow;     // half the width at each row, seen from the front (0 where there's nothing)

        /// <summary>
        /// "southSilhouette": the texture seen from the front. Only angles from angleMinimum to angleMaximum are kept
        /// (-180 to 180 for all the way round).
        /// </summary>
        public SurfaceSpace(PixelMask southSilhouette, float angleMinimum, float angleMaximum)
        {
            this.angleMinimum = angleMinimum;
            this.angleMaximum = angleMaximum;
            height = southSilhouette.height;
            radiusByRow = new float[height];
            int left = int.MaxValue, right = -1;
            top = -1;
            bottom = -1;
            for (int row = 0; row < height; row++)
            {
                if (!TryGetRowExtent(southSilhouette, row, out int rowLeft, out int rowRight)) continue;
                radiusByRow[row] = (rowRight - rowLeft) / 2f;
                left = Mathf.Min(left, rowLeft);
                right = Mathf.Max(right, rowRight);
                if (top < 0) top = row;
                bottom = row;
            }
            surfaceHeight = Mathf.Max(1, bottom - top);
            referenceRadius = Mathf.Max(1f, (right - left) / 2f);
            pixelsPerDegree = referenceRadius * Mathf.PI / 180f;
            width = Mathf.RoundToInt((angleMaximum - angleMinimum) * pixelsPerDegree);
        }

        public float ColumnForAngle(float angleDegrees) => (angleDegrees - angleMinimum) * pixelsPerDegree;

        public float AngleForColumn(float column) => column / pixelsPerDegree + angleMinimum;

        /// <summary>
        /// How far down the surface a row is: 0 at its top, 1 at its bottom.
        /// </summary>
        public float ShareDown(float row) => (row - top) / surfaceHeight;

        public float RowForShare(float shareDown) => top + shareDown * surfaceHeight;

        /// <summary>
        /// Which way the camera looks at the surface for a facing (Rot4.AsInt: 0 north, 1 east, 2 south, 3 west), as an
        /// angle around it. West is drawn from the east texture flipped, so its silhouette must be flipped too.
        /// </summary>
        public static float CameraAngle(int rotation)
        {
            switch (rotation)
            {
                case 0: return 180f;
                case 1: return -90f;
                case 3: return 90f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Wraps the angle into -180 to 180.
        /// </summary>
        public static float WrapAngle(float angleDegrees) => Mathf.Repeat(angleDegrees + 180f, 360f) - 180f;

        public static bool TryGetRowExtent(PixelMask mask, int row, out int left, out int right)
        {
            left = -1;
            right = -1;
            if (row < 0 || row >= mask.height) return false;
            for (int column = 0; column < mask.width; column++)
            {
                if (!mask.cells[row * mask.width + column]) continue;
                if (left < 0) left = column;
                right = column;
            }
            return left >= 0;
        }

        /// <summary>
        /// Wraps a flat picture around the surface as seen from one facing: each screen pixel looks up the angle of the
        /// surface it shows and takes the flat picture's colour there. Good for something that hugs the surface (a mask).
        /// Below "standsUpFrom" (a share down the surface) the cylinder follows this view's own outline row by row, so from
        /// the side it reaches over the chin; above it the picture stands straight up at the widest width, and its far side
        /// shows over the top wherever the surface doesn't hide it. "standoff" makes it sit a little proud of the surface.
        /// </summary>
        public KitbashCanvas WrapFlat(KitbashCanvas flat, PixelMask viewSilhouette, int rotation, float standoff, float standsUpFrom)
        {
            int viewWidth = viewSilhouette.width;
            var result = new KitbashCanvas(viewWidth, viewSilhouette.height);
            float camera = CameraAngle(rotation);
            GetWidest(viewSilhouette, out float widestCenter, out float widestRadius);

            for (int row = 0; row < viewSilhouette.height && row < flat.height; row++)
            {
                bool standing = ShareDown(row) < standsUpFrom;
                float centerX, radius;
                if (standing)
                {
                    centerX = widestCenter;
                    radius = widestRadius;
                }
                else
                {
                    // below the chin there's nothing to sit on
                    if (radiusByRow[row] <= 1f || !TryGetRowExtent(viewSilhouette, row, out int rowLeft, out int rowRight) || rowRight <= rowLeft) continue;
                    centerX = (rowLeft + rowRight) / 2f;
                    radius = (rowRight - rowLeft) / 2f;
                }
                radius *= standoff;

                for (int column = 0; column < viewWidth; column++)
                {
                    float offset = (column + 0.5f - centerX) / radius;
                    if (Mathf.Abs(offset) > 1f) continue;
                    float turn = Mathf.Asin(offset) * Mathf.Rad2Deg;
                    int resultIndex = row * viewWidth + column;

                    // above the surface nothing hides the far side, so the picture's points show over the top facing away
                    if (standing && !viewSilhouette.cells[resultIndex])
                    {
                        int backColumn = Mathf.RoundToInt(ColumnForAngle(WrapAngle(camera + 180f - turn)));
                        if (backColumn >= 0 && backColumn < flat.width) result.pixels[resultIndex] = flat.pixels[row * flat.width + backColumn];
                    }
                    int frontColumn = Mathf.RoundToInt(ColumnForAngle(WrapAngle(camera + turn)));
                    if (frontColumn < 0 || frontColumn >= flat.width) continue;
                    Color32 front = flat.pixels[row * flat.width + frontColumn];
                    if (front.a > 0) result.pixels[resultIndex] = front;
                }
            }
            return result;
        }

        /// <summary>
        /// Places pieces built on the surface for one facing, each drawn whole as a solid rock: only its position wraps
        /// around the body, and it narrows a little as it turns away (never squashed into a sliver). Pieces round the back
        /// are hidden; ones partly behind only show where they stick out past the outline. Good for growths.
        /// </summary>
        public KitbashCanvas PlaceRigid(List<PlacedPiece> pieces, PixelMask viewSilhouette, int rotation, float standoff)
        {
            var result = new KitbashCanvas(viewSilhouette.width, viewSilhouette.height);
            if (!viewSilhouette.TryGetRowRange(out int firstRow, out int lastRow)) return result;
            float camera = CameraAngle(rotation);

            bool growing = pieces.Exists(placedPiece => placedPiece.growthSpan > 0f);
            if (growing) result.EnableGrowth();
            var placements = new List<RigidPlacement>();
            foreach (PlacedPiece placedPiece in pieces)
            {
                Vector2 centroid = placedPiece.outline.Centroid;
                float turn = WrapAngle(AngleForColumn(centroid.x) - camera) * Mathf.Deg2Rad;
                float facing = Mathf.Cos(turn);
                if (facing < -0.35f) continue;   // round the back: hidden behind the body

                int row = Mathf.Clamp(Mathf.RoundToInt(centroid.y), firstRow, lastRow);
                if (!TryGetRowExtent(viewSilhouette, row, out int rowLeft, out int rowRight)) continue;
                float centerX = (rowLeft + rowRight) / 2f;
                float radius = (rowRight - rowLeft) / 2f;
                Vector2 screen = new Vector2(centerX + radius * standoff * Mathf.Sin(turn), centroid.y);
                float squeeze = Mathf.Max(0.6f, Mathf.Abs(facing));
                // from its spot on the flat surface to its spot on screen, narrowed about its middle
                var move = new Affine2D(squeeze, 0f, screen.x - squeeze * centroid.x, 0f, 1f, screen.y - centroid.y);
                placements.Add(new RigidPlacement { facing = facing, piece = placedPiece.piece, placement = Affine2D.Compose(move, placedPiece.placement), growth = placedPiece, origin = move.Apply(placedPiece.growthOrigin) });
            }

            // furthest round first, so nearer pieces cover them
            placements.Sort((first, second) => first.facing.CompareTo(second.facing));
            foreach (RigidPlacement rigid in placements)
            {
                if (rigid.facing >= 0.15f)
                {
                    result.DrawGrowingPiece(rigid.piece, rigid.placement, rigid.origin, rigid.growth.growthStart, rigid.growth.growthSpan);
                    continue;
                }
                // mostly behind the body: the body covers whatever of it isn't sticking out past the outline
                var layer = new KitbashCanvas(result.width, result.height);
                if (growing) layer.EnableGrowth();
                layer.DrawGrowingPiece(rigid.piece, rigid.placement, rigid.origin, rigid.growth.growthStart, rigid.growth.growthSpan);
                layer.CutTo(viewSilhouette.Inverted());
                result.Composite(layer, 0, 0);
            }
            result.Harden();
            return result;
        }

        private struct RigidPlacement
        {
            public float facing;   // 1 facing the camera, 0 side on, below 0 turning away
            public KitbashPiece piece;
            public Affine2D placement;
            public PlacedPiece growth;   // when and from where it grows
            public Vector2 origin;       // where it grows from, on screen
        }

        private static void GetWidest(PixelMask mask, out float center, out float radius)
        {
            int left = int.MaxValue, right = -1;
            for (int row = 0; row < mask.height; row++)
            {
                if (!TryGetRowExtent(mask, row, out int rowLeft, out int rowRight)) continue;
                left = Mathf.Min(left, rowLeft);
                right = Mathf.Max(right, rowRight);
            }
            center = right < 0 ? mask.width / 2f : (left + right) / 2f;
            radius = right < 0 ? 1f : Mathf.Max(1f, (right - left) / 2f);
        }
    }
}
