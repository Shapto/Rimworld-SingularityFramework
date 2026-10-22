using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// Reading a colour between pixels.
    /// </summary>
    public static class ColorSampling
    {
        /// <summary>
        /// The colour at a point (in pixels, row 0 at the top), blended between the four nearest pixels. Colours are
        /// weighted by their alpha, so see-through pixels don't darken the edge of a shape.
        /// </summary>
        public static Color32 Bilinear(Color32[] pixels, int width, int height, float x, float y)
        {
            x -= 0.5f;
            y -= 0.5f;
            int left = Mathf.FloorToInt(x);
            int top = Mathf.FloorToInt(y);
            float rightShare = x - left;
            float bottomShare = y - top;
            Color32 topLeft = PixelAt(pixels, width, height, left, top);
            Color32 topRight = PixelAt(pixels, width, height, left + 1, top);
            Color32 bottomLeft = PixelAt(pixels, width, height, left, top + 1);
            Color32 bottomRight = PixelAt(pixels, width, height, left + 1, top + 1);
            float topLeftWeight = (1f - rightShare) * (1f - bottomShare) * topLeft.a;
            float topRightWeight = rightShare * (1f - bottomShare) * topRight.a;
            float bottomLeftWeight = (1f - rightShare) * bottomShare * bottomLeft.a;
            float bottomRightWeight = rightShare * bottomShare * bottomRight.a;
            float alphaSum = topLeftWeight + topRightWeight + bottomLeftWeight + bottomRightWeight;
            if (alphaSum <= 0f) return new Color32(0, 0, 0, 0);
            byte red = (byte)Mathf.Clamp((topLeft.r * topLeftWeight + topRight.r * topRightWeight + bottomLeft.r * bottomLeftWeight + bottomRight.r * bottomRightWeight) / alphaSum, 0f, 255f);
            byte green = (byte)Mathf.Clamp((topLeft.g * topLeftWeight + topRight.g * topRightWeight + bottomLeft.g * bottomLeftWeight + bottomRight.g * bottomRightWeight) / alphaSum, 0f, 255f);
            byte blue = (byte)Mathf.Clamp((topLeft.b * topLeftWeight + topRight.b * topRightWeight + bottomLeft.b * bottomLeftWeight + bottomRight.b * bottomRightWeight) / alphaSum, 0f, 255f);
            byte alpha = (byte)Mathf.Clamp(alphaSum, 0f, 255f);
            return new Color32(red, green, blue, alpha);
        }

        public static Color32 PixelAt(Color32[] pixels, int width, int height, int column, int row)
        {
            if (column < 0 || row < 0 || column >= width || row >= height) return new Color32(0, 0, 0, 0);
            return pixels[row * width + column];
        }
    }
}
