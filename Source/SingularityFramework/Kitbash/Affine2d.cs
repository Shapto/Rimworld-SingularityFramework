using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A 2D affine transform (rotate, scale, stretch, mirror, move), mapping a piece's pixels onto a canvas.
    /// Works in image pixels: x to the right, y downward.
    /// </summary>
    public struct Affine2D
    {
        public float xFromX, xFromY, xOffset;
        public float yFromX, yFromY, yOffset;

        public static readonly Affine2D Identity = new Affine2D(1f, 0f, 0f, 0f, 1f, 0f);

        public Affine2D(float xFromX, float xFromY, float xOffset, float yFromX, float yFromY, float yOffset)
        {
            this.xFromX = xFromX;
            this.xFromY = xFromY;
            this.xOffset = xOffset;
            this.yFromX = yFromX;
            this.yFromY = yFromY;
            this.yOffset = yOffset;
        }

        public Vector2 Apply(Vector2 point)
        {
            return new Vector2(xFromX * point.x + xFromY * point.y + xOffset, yFromX * point.x + yFromY * point.y + yOffset);
        }

        public Vector2[] ApplyAll(IList<Vector2> points)
        {
            var result = new Vector2[points.Count];
            for (int index = 0; index < points.Count; index++) result[index] = Apply(points[index]);
            return result;
        }

        /// <summary>
        /// The transform that does "inner" first, then "outer".
        /// </summary>
        public static Affine2D Compose(Affine2D outer, Affine2D inner)
        {
            return new Affine2D(
                outer.xFromX * inner.xFromX + outer.xFromY * inner.yFromX,
                outer.xFromX * inner.xFromY + outer.xFromY * inner.yFromY,
                outer.xFromX * inner.xOffset + outer.xFromY * inner.yOffset + outer.xOffset,
                outer.yFromX * inner.xFromX + outer.yFromY * inner.yFromX,
                outer.yFromX * inner.xFromY + outer.yFromY * inner.yFromY,
                outer.yFromX * inner.xOffset + outer.yFromY * inner.yOffset + outer.yOffset);
        }

        /// <summary>
        /// The transform that undoes this one. False if it squashes everything onto a line.
        /// </summary>
        public bool TryInvert(out Affine2D inverse)
        {
            float determinant = xFromX * yFromY - xFromY * yFromX;
            if (Mathf.Abs(determinant) < 1e-9f)
            {
                inverse = Identity;
                return false;
            }
            float inverseXFromX = yFromY / determinant;
            float inverseXFromY = -xFromY / determinant;
            float inverseYFromX = -yFromX / determinant;
            float inverseYFromY = xFromX / determinant;
            inverse = new Affine2D(
                inverseXFromX, inverseXFromY, -(inverseXFromX * xOffset + inverseXFromY * yOffset),
                inverseYFromX, inverseYFromY, -(inverseYFromX * xOffset + inverseYFromY * yOffset));
            return true;
        }

        /// <summary>
        /// Flips a piece left to right within its own width.
        /// </summary>
        public static Affine2D MirrorHorizontal(float width)
        {
            return new Affine2D(-1f, 0f, width, 0f, 1f, 0f);
        }

        /// <summary>
        /// Lays a piece's edge (sourceStart to sourceEnd) exactly along a target edge, the other way round, so the new
        /// piece sits on the far side of that edge like a lego brick. "stretch" scales the piece across the edge only.
        /// False if the source edge has no length.
        /// </summary>
        public static bool TryLayEdge(Vector2 sourceStart, Vector2 sourceEnd, Vector2 targetStart, Vector2 targetEnd, float stretch, out Affine2D transform)
        {
            transform = Identity;
            Vector2 sourceVector = sourceEnd - sourceStart;
            Vector2 targetVector = targetStart - targetEnd;   // reversed: neighbours share the edge with opposite winding
            float sourceLength = sourceVector.magnitude;
            if (sourceLength < 1e-6f) return false;

            float scale = targetVector.magnitude / sourceLength;
            float rotation = Mathf.Atan2(targetVector.y, targetVector.x) - Mathf.Atan2(sourceVector.y, sourceVector.x);
            float cosine = Mathf.Cos(rotation);
            float sine = Mathf.Sin(rotation);

            // stretch across the edge only: identity plus (stretch - 1) along the across direction
            Vector2 across = new Vector2(-sourceVector.y, sourceVector.x) / sourceLength;
            float extra = stretch - 1f;
            float stretchXX = 1f + extra * across.x * across.x;
            float stretchXY = extra * across.x * across.y;
            float stretchYY = 1f + extra * across.y * across.y;

            float linearXX = scale * (cosine * stretchXX - sine * stretchXY);
            float linearXY = scale * (cosine * stretchXY - sine * stretchYY);
            float linearYX = scale * (sine * stretchXX + cosine * stretchXY);
            float linearYY = scale * (sine * stretchXY + cosine * stretchYY);

            transform = new Affine2D(
                linearXX, linearXY, targetEnd.x - (linearXX * sourceStart.x + linearXY * sourceStart.y),
                linearYX, linearYY, targetEnd.y - (linearYX * sourceStart.x + linearYY * sourceStart.y));
            return true;
        }

        /// <summary>
        /// The transform sending three piece points exactly onto three target points (used to stand a crystal on its base
        /// with its tip aimed at a chosen spot). False if the piece points lie on one line.
        /// </summary>
        public static bool TryFromThreePoints(Vector2 sourceFirst, Vector2 sourceSecond, Vector2 sourceThird, Vector2 targetFirst, Vector2 targetSecond, Vector2 targetThird, out Affine2D transform)
        {
            transform = Identity;
            // rows of [x y 1] for each source point; solve for the x row and the y row of the transform
            float determinant = sourceFirst.x * (sourceSecond.y - sourceThird.y) - sourceFirst.y * (sourceSecond.x - sourceThird.x) + (sourceSecond.x * sourceThird.y - sourceThird.x * sourceSecond.y);
            if (Mathf.Abs(determinant) < 1e-6f) return false;

            RowSolution xRow = SolveRow(sourceFirst, sourceSecond, sourceThird, targetFirst.x, targetSecond.x, targetThird.x, determinant);
            RowSolution yRow = SolveRow(sourceFirst, sourceSecond, sourceThird, targetFirst.y, targetSecond.y, targetThird.y, determinant);
            transform = new Affine2D(xRow.first, xRow.second, xRow.third, yRow.first, yRow.second, yRow.third);
            return true;
        }

        private struct RowSolution
        {
            public float first, second, third;
        }

        /// <summary>
        /// Cramer's rule for a * x + b * y + c = value at the three source points.
        /// </summary>
        private static RowSolution SolveRow(Vector2 first, Vector2 second, Vector2 third, float firstValue, float secondValue, float thirdValue, float determinant)
        {
            float a = (firstValue * (second.y - third.y) - first.y * (secondValue - thirdValue) + (secondValue * third.y - thirdValue * second.y)) / determinant;
            float b = (first.x * (secondValue - thirdValue) - firstValue * (second.x - third.x) + (second.x * thirdValue - third.x * secondValue)) / determinant;
            float c = (first.x * (second.y * thirdValue - third.y * secondValue) - first.y * (second.x * thirdValue - third.x * secondValue) + firstValue * (second.x * third.y - third.x * second.y)) / determinant;
            return new RowSolution { first = a, second = b, third = c };
        }

        /// <summary>
        /// Lays a piece's long axis (major, minor: unit directions in piece pixels, around pieceCenter) along "direction",
        /// scaled to "length" along it and "thickness" across, centred on "middle".
        /// </summary>
        public static Affine2D AlongDirection(Vector2 pieceCenter, Vector2 major, Vector2 minor, float pieceLength, float pieceThickness, Vector2 middle, Vector2 direction, float length, float thickness)
        {
            direction = direction.normalized;
            Vector2 across = new Vector2(-direction.y, direction.x);
            // linear = [direction * length / pieceLength, across * thickness / pieceThickness] * inverse([major, minor])
            Vector2 targetMajor = direction * (length / pieceLength);
            Vector2 targetMinor = across * (thickness / pieceThickness);
            float determinant = major.x * minor.y - minor.x * major.y;
            float inverseXX = minor.y / determinant;
            float inverseXY = -minor.x / determinant;
            float inverseYX = -major.y / determinant;
            float inverseYY = major.x / determinant;
            float linearXX = targetMajor.x * inverseXX + targetMinor.x * inverseYX;
            float linearXY = targetMajor.x * inverseXY + targetMinor.x * inverseYY;
            float linearYX = targetMajor.y * inverseXX + targetMinor.y * inverseYX;
            float linearYY = targetMajor.y * inverseXY + targetMinor.y * inverseYY;
            return new Affine2D(
                linearXX, linearXY, middle.x - (linearXX * pieceCenter.x + linearXY * pieceCenter.y),
                linearYX, linearYY, middle.y - (linearYX * pieceCenter.x + linearYY * pieceCenter.y));
        }
    }
}
