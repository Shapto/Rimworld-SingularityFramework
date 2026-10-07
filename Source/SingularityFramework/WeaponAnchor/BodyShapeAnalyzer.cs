using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.WeaponAnchor
{
    /// <summary>
    /// How wide a pawn's torso is in its body texture, and where the body starts and ends vertically.
    /// Results are cached per texture. Must run on the main thread, since it reads textures.
    /// </summary>
    public static class BodyShapeAnalyzer
    {
        private const float VisibleAlphaThreshold = 0.1f;
        private const float TorsoLowestShare = 0.25f;   // the torso is measured between these shares of the body's height
        private const float TorsoHighestShare = 0.7f;

        public class BodyShape
        {
            public float torsoWidth;   // widest point of the torso, as a share of the texture's width
            public float bottom;       // lowest visible pixel, in sprite units (-0.5 to 0.5)
            public float top;          // highest visible pixel, in sprite units
        }

        private static readonly Dictionary<Texture2D, BodyShape> cachedShapes = new Dictionary<Texture2D, BodyShape>();

        public static BodyShape GetShape(Texture2D bodyTexture)
        {
            if (cachedShapes.TryGetValue(bodyTexture, out BodyShape cachedShape)) return cachedShape;

            BodyShape shape = Analyze(bodyTexture);
            cachedShapes[bodyTexture] = shape;
            return shape;
        }

        private static BodyShape Analyze(Texture2D bodyTexture)
        {
            Color[] pixels = WeaponShapeAnalyzer.ReadPixels(bodyTexture);
            int textureWidth = bodyTexture.width;
            int textureHeight = bodyTexture.height;

            // The visible extent of each row, and the body's lowest and highest rows.
            var rowMinimumX = new int[textureHeight];
            var rowMaximumX = new int[textureHeight];
            int lowestRow = int.MaxValue;
            int highestRow = int.MinValue;
            for (int row = 0; row < textureHeight; row++)
            {
                rowMinimumX[row] = int.MaxValue;
                rowMaximumX[row] = int.MinValue;
                for (int column = 0; column < textureWidth; column++)
                {
                    if (pixels[row * textureWidth + column].a < VisibleAlphaThreshold) continue;
                    rowMinimumX[row] = Mathf.Min(rowMinimumX[row], column);
                    rowMaximumX[row] = Mathf.Max(rowMaximumX[row], column);
                    lowestRow = Mathf.Min(lowestRow, row);
                    highestRow = Mathf.Max(highestRow, row);
                }
            }

            var shape = new BodyShape { torsoWidth = 0.5f, bottom = -0.3f, top = 0.3f };
            if (lowestRow > highestRow) return shape;

            // The widest row within the torso's part of the body.
            int bodyHeight = highestRow - lowestRow;
            int torsoLowestRow = lowestRow + Mathf.RoundToInt(bodyHeight * TorsoLowestShare);
            int torsoHighestRow = lowestRow + Mathf.RoundToInt(bodyHeight * TorsoHighestShare);
            int widestPixels = 0;
            for (int row = torsoLowestRow; row <= torsoHighestRow; row++)
            {
                if (rowMinimumX[row] > rowMaximumX[row]) continue;
                widestPixels = Mathf.Max(widestPixels, rowMaximumX[row] - rowMinimumX[row] + 1);
            }

            shape.torsoWidth = widestPixels / (float)textureWidth;
            shape.bottom = (lowestRow + 0.5f) / textureHeight - 0.5f;
            shape.top = (highestRow + 0.5f) / textureHeight - 0.5f;
            return shape;
        }
    }
}
