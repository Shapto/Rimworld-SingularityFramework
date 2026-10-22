using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Visuals.Brushlines
{
    /// <summary>
    /// Collects every brush line drawn this frame into a few meshes (one per colour and fade level) and draws them, so
    /// many strokes cost only a handful of draw calls.
    /// </summary>
    public class BrushLineDrawer
    {
        private const int FadeLevels = 8;

        public readonly BrushPalette palette;
        private readonly float altitude;

        private class MeshBuffer
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
        }

        private readonly Dictionary<int, MeshBuffer> buffers = new Dictionary<int, MeshBuffer>();
        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private readonly List<Mesh> meshPool = new List<Mesh>();

        public BrushLineDrawer(BrushPalette palette, float altitude)
        {
            this.palette = palette;
            this.altitude = altitude;
        }

        private MeshBuffer BufferFor(int band, float alpha)
        {
            int fadeLevel = Mathf.Clamp(Mathf.CeilToInt(alpha * FadeLevels), 1, FadeLevels);
            int key = band * 16 + fadeLevel;
            if (!buffers.TryGetValue(key, out MeshBuffer buffer))
            {
                buffer = new MeshBuffer();
                buffers[key] = buffer;
            }
            return buffer;
        }

        /// <summary>
        /// A four-cornered patch of a stroke (corners in order round its edge).
        /// </summary>
        public void AddQuad(int band, float alpha, Vector3 first, Vector3 second, Vector3 third, Vector3 fourth)
        {
            MeshBuffer buffer = BufferFor(band, alpha);
            int start = buffer.vertices.Count;
            buffer.vertices.Add(Lifted(first));
            buffer.vertices.Add(Lifted(second));
            buffer.vertices.Add(Lifted(third));
            buffer.vertices.Add(Lifted(fourth));
            // both windings, so it shows whichever way round the corners came
            buffer.triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3, start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        public void AddTriangle(int band, float alpha, Vector3 first, Vector3 second, Vector3 third)
        {
            MeshBuffer buffer = BufferFor(band, alpha);
            int start = buffer.vertices.Count;
            buffer.vertices.Add(Lifted(first));
            buffer.vertices.Add(Lifted(second));
            buffer.vertices.Add(Lifted(third));
            buffer.triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 1 });
        }

        private Vector3 Lifted(Vector3 position) => new Vector3(position.x, altitude, position.z);

        /// <summary>
        /// Draws everything collected this frame and empties the buffers for the next.
        /// </summary>
        public void DrawAll()
        {
            int meshIndex = 0;
            // deepest band first, so the bright core lands on top
            var keys = new List<int>(buffers.Keys);
            keys.Sort((first, second) => second.CompareTo(first));
            foreach (int key in keys)
            {
                MeshBuffer buffer = buffers[key];
                if (buffer.triangles.Count == 0) continue;
                if (meshIndex >= meshPool.Count) meshPool.Add(new Mesh());
                Mesh mesh = meshPool[meshIndex++];
                mesh.Clear();
                mesh.SetVertices(buffer.vertices);
                mesh.SetTriangles(buffer.triangles, 0);
                mesh.RecalculateBounds();
                Graphics.DrawMesh(mesh, Matrix4x4.identity, MaterialFor(key), 0);
                buffer.vertices.Clear();
                buffer.triangles.Clear();
            }
        }

        private Material MaterialFor(int key)
        {
            if (materials.TryGetValue(key, out Material material)) return material;
            Color color = palette.ColorOfBand(key / 16);
            color.a = (key % 16) / (float)FadeLevels;
            material = MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, ShaderDatabase.Transparent, color));
            materials[key] = material;
            return material;
        }
    }
}
