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
    /// where they attach instead of popping in. Growth follows game time, so it pauses with the game.
    /// Done on the graphics card, so it costs almost nothing: every crystal pixel's arrival time is stored in its alpha
    /// (crystals are fully solid, so alpha is otherwise unused), and the cutout shader hides pixels whose alpha times the
    /// material's alpha falls under one half. Raising the material's alpha over the growth reveals the pixels in arrival
    /// order. No texture is touched after it's made; each frame only sets one colour per material.
    /// </summary>
    public static class KitbashGrowth
    {
        private const float ArrivalSpread = 0.98f;   // keeps the latest pixels' alpha just above one half, so they show once grown

        private class Growing
        {
            public Material[] materials;
            public int startTick;
            public int durationTicks;
        }

        private static readonly List<Growing> growing = new List<Growing>();

        /// <summary>
        /// The alpha a solid pixel gets for its arrival time (0 first, 1 last; later than 1 shows at the end).
        /// It's 1 / (1 + spread × arrival), which with the material's alpha from AlphaFor is at least one half exactly
        /// when the pixel has arrived.
        /// </summary>
        public static byte EncodeArrival(float arrival)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(255f / (1f + ArrivalSpread * Mathf.Clamp01(arrival))), 129, 255);
        }

        /// <summary>
        /// The material alpha that shows every pixel arrived by "progress" (0 to 1).
        /// </summary>
        private static float AlphaFor(float progress) => (1f + ArrivalSpread * Mathf.Clamp01(progress)) / 2f;

        /// <summary>
        /// Starts growing in whatever these materials draw (their textures made with arrival in their alpha, by
        /// KitbashTextures.ToTexture). Main thread only.
        /// </summary>
        public static void Start(Material[] materials, int durationTicks)
        {
            var growth = new Growing { materials = materials, startTick = Find.TickManager.TicksGame, durationTicks = Mathf.Max(1, durationTicks) };
            SetAlpha(growth, AlphaFor(0f));
            growing.Add(growth);
        }

        /// <summary>
        /// Stops growing these materials (they're being destroyed).
        /// </summary>
        public static void Stop(Material[] materials)
        {
            growing.RemoveAll(growth => System.Array.Exists(growth.materials, material => System.Array.IndexOf(materials, material) >= 0));
        }

        /// <summary>
        /// Stops growing whatever draws these textures (they're being destroyed).
        /// </summary>
        public static void Stop(Texture2D[] textures)
        {
            growing.RemoveAll(growth => System.Array.Exists(growth.materials, material => material != null && System.Array.IndexOf(textures, material.mainTexture) >= 0));
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
                    SetAlpha(growth, 1f);
                    growing.RemoveAt(index);
                    continue;
                }
                SetAlpha(growth, AlphaFor(progress));
            }
        }

        private static void SetAlpha(Growing growth, float alpha)
        {
            foreach (Material material in growth.materials)
            {
                if (material == null) continue;
                Color color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }
    }
}
