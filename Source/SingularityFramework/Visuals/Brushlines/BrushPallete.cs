using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Visuals.Brushlines
{
    /// <summary>
    /// The three colours a brush line is painted in: its bright core, its main body and its dark frayed edges.
    /// </summary>
    public class BrushPalette
    {
        public readonly Color bright;
        public readonly Color main;
        public readonly Color deep;

        public BrushPalette(Color bright, Color main, Color deep)
        {
            this.bright = bright;
            this.main = main;
            this.deep = deep;
        }

        /// <summary>
        /// The colour for a bristle by how far out it is: 0 the middle of the stroke, 1 its edge.
        /// </summary>
        public int BandFor(float edge) => edge < 0.34f ? 0 : edge < 0.67f ? 1 : 2;

        public Color ColorOfBand(int band) => band == 0 ? bright : band == 1 ? main : deep;
    }
}
