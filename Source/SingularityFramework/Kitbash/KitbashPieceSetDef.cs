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
    /// One piece texture in a set: where it is, how big a piece it's meant to be, and whether it can stand up as a point.
    /// </summary>
    public class KitbashPieceEntry
    {
        public string texPath;
        public KitbashSize size = KitbashSize.Medium;
        public bool pointed;
    }

    /// <summary>
    /// The pieces an artist made for kitbashing (for example the hardblood crystals). Their pixels are read the first time
    /// they're needed, which must be on the main thread.
    /// </summary>
    public class KitbashPieceSetDef : Def
    {
        private const int MaximumPieceSize = 512;   // bigger piece textures are read scaled down to this

        public List<KitbashPieceEntry> pieces = new List<KitbashPieceEntry>();

        [Unsaved]
        private List<KitbashPiece> loadedPieces;

        /// <summary>
        /// The pieces, read from their textures on first use (main thread only).
        /// </summary>
        public List<KitbashPiece> Pieces
        {
            get
            {
                if (loadedPieces == null) loadedPieces = LoadPieces();
                return loadedPieces;
            }
        }

        private List<KitbashPiece> LoadPieces()
        {
            var result = new List<KitbashPiece>();
            foreach (KitbashPieceEntry entry in pieces)
            {
                Texture2D texture = ContentFinder<Texture2D>.Get(entry.texPath, false);
                if (texture == null)
                {
                    Log.Error($"[Singularity Framework] {defName}: no texture at {entry.texPath}");
                    continue;
                }
                float downscale = Mathf.Min(1f, MaximumPieceSize / (float)Mathf.Max(texture.width, texture.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * downscale));
                int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * downscale));
                result.Add(new KitbashPiece(entry.texPath, entry.size, entry.pointed, KitbashTextures.ReadPixels(texture, width, height, false), width, height));
            }
            return result;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (pieces.NullOrEmpty()) yield return "has no pieces";
            if (!pieces.NullOrEmpty() && !pieces.Exists(entry => entry.size == KitbashSize.Large)) yield return "has no Large pieces (the outline and fill need them)";
        }
    }
}
