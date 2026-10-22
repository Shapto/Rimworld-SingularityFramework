using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// Moving pictures between the game's textures and the kitbash engine. Must run on the main thread.
    /// </summary>
    public static class KitbashTextures
    {
        /// <summary>
        /// A texture's pixels at the given size (scaled up or down on the way), row 0 at the top as the engine expects.
        /// Textures on the graphics card can't be read directly, so it's drawn into a temporary readable copy first.
        /// "mirrored" flips it left to right (west views are the east textures flipped).
        /// </summary>
        public static Color32[] ReadPixels(Texture source, int width, int height, bool mirrored)
        {
            RenderTexture temporaryTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            if (mirrored) Graphics.Blit(source, temporaryTexture, new Vector2(-1f, 1f), new Vector2(1f, 0f));
            else Graphics.Blit(source, temporaryTexture);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = temporaryTexture;
            var readableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temporaryTexture);

            Color32[] bottomUp = readableTexture.GetPixels32();
            UnityEngine.Object.Destroy(readableTexture);
            return FlipRows(bottomUp, width, height);
        }

        /// <summary>
        /// Where a texture shows anything, at the given size (a silhouette for the engine to build on).
        /// </summary>
        public static PixelMask ReadSilhouette(Texture source, int width, int height, bool mirrored)
        {
            return WeaponProfile.Visible(ReadPixels(source, width, height, mirrored), width, height);
        }

        /// <summary>
        /// A finished canvas as a texture the game can draw. "mirrored" flips it left to right.
        /// </summary>
        public static Texture2D ToTexture(KitbashCanvas canvas, string name, bool mirrored)
        {
            var pixels = new Color32[canvas.pixels.Length];
            for (int row = 0; row < canvas.height; row++)
            {
                int sourceRow = (canvas.height - 1 - row) * canvas.width;   // Unity textures start at the bottom
                for (int column = 0; column < canvas.width; column++)
                {
                    int sourceColumn = mirrored ? canvas.width - 1 - column : column;
                    pixels[row * canvas.width + column] = canvas.pixels[sourceRow + sourceColumn];
                }
            }
            var texture = new Texture2D(canvas.width, canvas.height, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Color32[] FlipRows(Color32[] pixels, int width, int height)
        {
            var flipped = new Color32[pixels.Length];
            for (int row = 0; row < height; row++) System.Array.Copy(pixels, (height - 1 - row) * width, flipped, row * width, width);
            return flipped;
        }
    }
}
