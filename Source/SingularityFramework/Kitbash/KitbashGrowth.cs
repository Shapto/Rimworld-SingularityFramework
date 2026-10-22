using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// Pictures growing into place: each pixel shows once the growth reaches its arrival time, so crystals creep out from
    /// where they attach instead of popping in. Growth follows game time, so it pauses with the game. Once grown, the
    /// arrival data is thrown away and the textures are handed to the graphics card for good.
    /// </summary>
    public static class KitbashGrowth
    {
        private const int TicksBetweenUpdates = 3;   // the textures are redrawn every few ticks, not every frame

        private class GrowingTexture
        {
            public Texture2D texture;
            public Color32[] finalPixels;   // in texture order
            public float[] arrival;         // in texture order
            public Color32[] shownPixels;
        }

        private class Growing
        {
            public List<GrowingTexture> textures = new List<GrowingTexture>();
            public int startTick;
            public int durationTicks;
            public int lastUpdateTick = -1;
        }

        private static readonly List<Growing> growing = new List<Growing>();

        /// <summary>
        /// Starts growing textures in (made readable, from canvases that recorded arrival). The west texture is mirrored,
        /// like KitbashGraphic stores it. Main thread only.
        /// </summary>
        public static void Start(Texture2D[] texturesByRotation, KitbashCanvas[] canvasesByRotation, int durationTicks)
        {
            var growth = new Growing { startTick = Find.TickManager.TicksGame, durationTicks = Mathf.Max(1, durationTicks) };
            for (int rotation = 0; rotation < texturesByRotation.Length; rotation++)
            {
                KitbashCanvas canvas = canvasesByRotation[rotation];
                bool mirrored = rotation == Rot4.West.AsInt;
                growth.textures.Add(new GrowingTexture
                {
                    texture = texturesByRotation[rotation],
                    finalPixels = KitbashTextures.OrientForTexture(canvas.pixels, canvas.width, canvas.height, mirrored),
                    arrival = KitbashTextures.OrientForTexture(canvas.arrival, canvas.width, canvas.height, mirrored),
                    shownPixels = new Color32[canvas.pixels.Length]
                });
            }
            growing.Add(growth);
            Show(growth, 0f);
        }

        /// <summary>
        /// Stops growing these textures (they're being destroyed).
        /// </summary>
        public static void Stop(Texture2D[] textures)
        {
            growing.RemoveAll(growth => growth.textures.Exists(growingTexture => System.Array.IndexOf(textures, growingTexture.texture) >= 0));
        }

        /// <summary>
        /// Moves every growing picture along. Called every frame on the main thread.
        /// </summary>
        public static void Update()
        {
            if (growing.Count == 0 || Find.TickManager == null) return;
            int now = Find.TickManager.TicksGame;
            for (int index = growing.Count - 1; index >= 0; index--)
            {
                Growing growth = growing[index];
                float progress = (now - growth.startTick) / (float)growth.durationTicks;
                if (progress >= 1f)
                {
                    Finish(growth);
                    growing.RemoveAt(index);
                    continue;
                }
                if (now - growth.lastUpdateTick < TicksBetweenUpdates) continue;
                growth.lastUpdateTick = now;
                Show(growth, progress);
            }
        }

        private static void Show(Growing growth, float progress)
        {
            foreach (GrowingTexture growingTexture in growth.textures)
            {
                if (growingTexture.texture == null) continue;
                for (int index = 0; index < growingTexture.finalPixels.Length; index++) growingTexture.shownPixels[index] = growingTexture.arrival[index] <= progress ? growingTexture.finalPixels[index] : new Color32(0, 0, 0, 0);
                growingTexture.texture.SetPixels32(growingTexture.shownPixels);
                growingTexture.texture.Apply(true, false);
            }
        }

        /// <summary>
        /// Shows the finished picture and hands the textures to the graphics card for good, freeing their copies here.
        /// </summary>
        private static void Finish(Growing growth)
        {
            foreach (GrowingTexture growingTexture in growth.textures)
            {
                if (growingTexture.texture == null) continue;
                growingTexture.texture.SetPixels32(growingTexture.finalPixels);
                growingTexture.texture.Apply(true, true);
            }
        }
    }
}
