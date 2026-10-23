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
        /// A finished canvas as a texture the game can draw. "mirrored" flips it left to right. If the canvas recorded
        /// growth, its arrival times go into the alpha (see KitbashGrowth); fully grown it looks the same.
        /// </summary>
        public static Texture2D ToTexture(KitbashCanvas canvas, string name, bool mirrored)
        {
            var texture = new Texture2D(canvas.width, canvas.height, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(TexturePixels(canvas, mirrored));
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>
        /// A canvas's pixels in texture order, with each solid pixel's arrival time in its alpha when it recorded growth.
        /// </summary>
        public static Color32[] TexturePixels(KitbashCanvas canvas, bool mirrored)
        {
            Color32[] pixels = OrientForTexture(canvas.pixels, canvas.width, canvas.height, mirrored);
            if (canvas.arrival == null) return pixels;
            float[] arrival = OrientForTexture(canvas.arrival, canvas.width, canvas.height, mirrored);
            for (int index = 0; index < pixels.Length; index++) pixels[index].a = pixels[index].a > KitbashCanvas.SolidAlpha ? KitbashGrowth.EncodeArrival(arrival[index]) : (byte)0;
            return pixels;
        }

        /// <summary>
        /// Puts per-pixel values (colours, arrival times) in the order a texture stores them: Unity textures start at the
        /// bottom, the engine's canvases at the top. "mirrored" also flips them left to right.
        /// </summary>
        public static T[] OrientForTexture<T>(T[] values, int width, int height, bool mirrored)
        {
            var oriented = new T[values.Length];
            for (int row = 0; row < height; row++)
            {
                int sourceRow = (height - 1 - row) * width;
                for (int column = 0; column < width; column++)
                {
                    int sourceColumn = mirrored ? width - 1 - column : column;
                    oriented[row * width + column] = values[sourceRow + sourceColumn];
                }
            }
            return oriented;
        }

        private static Color32[] FlipRows(Color32[] pixels, int width, int height)
        {
            var flipped = new Color32[pixels.Length];
            for (int row = 0; row < height; row++) System.Array.Copy(pixels, (height - 1 - row) * width, flipped, row * width, width);
            return flipped;
        }
    }
}
