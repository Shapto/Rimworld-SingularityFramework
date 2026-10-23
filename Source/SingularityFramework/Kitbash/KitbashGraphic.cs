using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A four-facing graphic made from generated textures instead of files, so it can be handed to anything that draws a
    /// Graphic_Multi (render nodes, weapon drawing).
    /// The west texture is stored mirrored and the graphic marked as west-flipped: humanlike meshes draw west flipped, so
    /// the flip undoes the mirror and the real west view shows. (Kitbashed results aren't symmetric, so west can't just be
    /// east flipped like vanilla does.)
    /// </summary>
    public class KitbashGraphic : Graphic_Multi
    {
        private static readonly FieldInfo MaterialsField = AccessTools.Field(typeof(Graphic_Multi), "mats");
        private static readonly FieldInfo WestFlippedField = AccessTools.Field(typeof(Graphic_Multi), "westFlipped");

        public readonly Texture2D[] textures = new Texture2D[4];
        public readonly Material[] materials = new Material[4];

        /// <summary>
        /// "canvasesByRotation": the picture for each facing, indexed by Rot4.AsInt (north, east, south, west), each as
        /// seen from that facing. "growthTicks": if above 0 (and the canvases recorded growth), the picture grows into
        /// place over that many ticks instead of appearing at once. Main thread only.
        /// </summary>
        public static KitbashGraphic Create(KitbashCanvas[] canvasesByRotation, string name, Shader shader, Vector2 drawSize, int growthTicks = 0)
        {
            bool grows = growthTicks > 0 && System.Array.TrueForAll(canvasesByRotation, canvas => canvas.arrival != null);
            var graphic = new KitbashGraphic
            {
                path = name,
                drawSize = drawSize,
                color = Color.white,
                colorTwo = Color.white
            };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                bool west = rotation == Rot4.West.AsInt;
                graphic.textures[rotation] = KitbashTextures.ToTexture(canvasesByRotation[rotation], $"{name}_{rotation}", west);
                // its own material, not one from the shared pool, so it can be freed with the texture
                graphic.materials[rotation] = new Material(shader)
                {
                    name = $"{name}_{rotation}",
                    mainTexture = graphic.textures[rotation],
                    color = Color.white
                };
            }
            if (MaterialsField == null || WestFlippedField == null)
            {
                Log.Error("[Singularity Framework] Graphic_Multi has changed: KitbashGraphic can't set its materials.");
                return graphic;
            }
            MaterialsField.SetValue(graphic, graphic.materials);
            WestFlippedField.SetValue(graphic, true);
            if (grows) KitbashGrowth.Start(graphic.materials, growthTicks);
            return graphic;
        }

        /// <summary>
        /// Frees the generated textures and materials (when the pawn or weapon this was made for is gone).
        /// </summary>
        public void Destroy()
        {
            KitbashGrowth.Stop(materials);
            foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            foreach (Texture2D texture in textures) if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        /// <summary>
        /// A single generated texture as a material (for things drawn with one facing, like a weapon overlay).
        /// "growthTicks": if above 0 (and the canvas recorded growth), it grows into place over that many ticks.
        /// </summary>
        public static Material SingleMaterial(KitbashCanvas canvas, string name, Shader shader, out Texture2D texture, int growthTicks = 0)
        {
            texture = KitbashTextures.ToTexture(canvas, name, false);
            var material = new Material(shader)
            {
                name = name,
                mainTexture = texture,
                color = Color.white
            };
            if (growthTicks > 0 && canvas.arrival != null) KitbashGrowth.Start(new[] { material }, growthTicks);
            return material;
        }
    }
}
