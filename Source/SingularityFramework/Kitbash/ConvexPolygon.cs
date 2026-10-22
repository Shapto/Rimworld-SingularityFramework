using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A row of pixels a shape covers: from firstColumn to lastColumn, inclusive.
    /// </summary>
    public struct PixelSpan
    {
        public int row;
        public int firstColumn;
        public int lastColumn;
    }

    /// <summary>
    /// One straight edge of an outline, from start to end.
    /// </summary>
    public struct Edge
    {
        public Vector2 start;
        public Vector2 end;

        public Edge(Vector2 start, Vector2 end)
        {
            this.start = start;
            this.end = end;
        }

        public Vector2 Middle => (start + end) / 2f;
        public float Length => Vector2.Distance(start, end);
        public Edge Reversed => new Edge(end, start);
    }

    /// <summary>
    /// A convex outline, in image pixels (y downward). Every piece's outline is convex (it's the convex hull of the piece),
    /// and an affine transform keeps it convex, so placed pieces can use the fast convex-only maths here.
    /// </summary>
    public class ConvexPolygon
    {
        public readonly Vector2[] points;

        private float cachedArea = -1f;

        public ConvexPolygon(Vector2[] points)
        {
            this.points = points;
        }

        public ConvexPolygon Transformed(Affine2D transform)
        {
            return new ConvexPolygon(transform.ApplyAll(points));
        }

        public float Area
        {
            get
            {
                if (cachedArea >= 0f) return cachedArea;
                float doubledArea = 0f;
                for (int index = 0; index < points.Length; index++)
                {
                    Vector2 current = points[index];
                    Vector2 next = points[(index + 1) % points.Length];
                    doubledArea += current.x * next.y - next.x * current.y;
                }
                cachedArea = Mathf.Abs(doubledArea) / 2f;
                return cachedArea;
            }
        }

        public Vector2 Centroid
        {
            get
            {
                float doubledArea = 0f;
                float sumX = 0f;
                float sumY = 0f;
                for (int index = 0; index < points.Length; index++)
                {
                    Vector2 current = points[index];
                    Vector2 next = points[(index + 1) % points.Length];
                    float cross = current.x * next.y - next.x * current.y;
                    doubledArea += cross;
                    sumX += (current.x + next.x) * cross;
                    sumY += (current.y + next.y) * cross;
                }
                if (Mathf.Abs(doubledArea) < 1e-6f) return points.Length > 0 ? points[0] : Vector2.zero;
                return new Vector2(sumX / (3f * doubledArea), sumY / (3f * doubledArea));
            }
        }

        public void GetBounds(out float minimumX, out float minimumY, out float maximumX, out float maximumY)
        {
            minimumX = minimumY = float.MaxValue;
            maximumX = maximumY = float.MinValue;
            foreach (Vector2 point in points)
            {
                minimumX = Mathf.Min(minimumX, point.x);
                minimumY = Mathf.Min(minimumY, point.y);
                maximumX = Mathf.Max(maximumX, point.x);
                maximumY = Mathf.Max(maximumY, point.y);
            }
        }

        /// <summary>
        /// The longer side of the outline's upright bounding box.
        /// </summary>
        public float LongestSide
        {
            get
            {
                GetBounds(out float minimumX, out float minimumY, out float maximumX, out float maximumY);
                return Mathf.Max(maximumX - minimumX, maximumY - minimumY);
            }
        }

        /// <summary>
        /// How thin the outline is at its narrowest: the smallest gap between two parallel lines that squeeze it.
        /// Squashed slivers read as clutter, so pieces thinner than a limit are refused.
        /// </summary>
        public float Thickness
        {
            get
            {
                float thinnest = float.MaxValue;
                for (int index = 0; index < points.Length; index++)
                {
                    Vector2 start = points[index];
                    Vector2 end = points[(index + 1) % points.Length];
                    Vector2 edge = end - start;
                    float edgeLength = edge.magnitude;
                    if (edgeLength < 1e-6f) continue;
                    Vector2 normal = new Vector2(-edge.y, edge.x) / edgeLength;
                    float widest = 0f;
                    foreach (Vector2 point in points) widest = Mathf.Max(widest, Mathf.Abs(Vector2.Dot(point - start, normal)));
                    thinnest = Mathf.Min(thinnest, widest);
                }
                return thinnest == float.MaxValue ? 0f : thinnest;
            }
        }

        /// <summary>
        /// The rows of pixels whose centres are inside the outline, clipped to the canvas.
        /// </summary>
        public List<PixelSpan> Rasterize(int canvasWidth, int canvasHeight)
        {
            var spans = new List<PixelSpan>();
            GetBounds(out _, out float minimumY, out _, out float maximumY);
            int firstRow = Mathf.Max(0, Mathf.CeilToInt(minimumY - 0.5f));
            int lastRow = Mathf.Min(canvasHeight - 1, Mathf.FloorToInt(maximumY - 0.5f));
            for (int row = firstRow; row <= lastRow; row++)
            {
                float rowCenter = row + 0.5f;
                float leftmost = float.MaxValue;
                float rightmost = float.MinValue;
                for (int index = 0; index < points.Length; index++)
                {
                    Vector2 start = points[index];
                    Vector2 end = points[(index + 1) % points.Length];
                    if ((start.y > rowCenter) == (end.y > rowCenter)) continue;   // this edge doesn't cross the row
                    float crossingX = start.x + (rowCenter - start.y) / (end.y - start.y) * (end.x - start.x);
                    leftmost = Mathf.Min(leftmost, crossingX);
                    rightmost = Mathf.Max(rightmost, crossingX);
                }
                if (leftmost > rightmost) continue;
                int firstColumn = Mathf.Max(0, Mathf.CeilToInt(leftmost - 0.5f));
                int lastColumn = Mathf.Min(canvasWidth - 1, Mathf.FloorToInt(rightmost - 0.5f));
                if (firstColumn <= lastColumn) spans.Add(new PixelSpan { row = row, firstColumn = firstColumn, lastColumn = lastColumn });
            }
            return spans;
        }

        /// <summary>
        /// How far apart two outlines are; 0 if they touch or overlap.
        /// </summary>
        public float DistanceTo(ConvexPolygon other)
        {
            if (Overlaps(other)) return 0f;
            float nearest = float.MaxValue;
            foreach (Vector2 point in points) nearest = Mathf.Min(nearest, other.DistanceToBoundary(point));
            foreach (Vector2 point in other.points) nearest = Mathf.Min(nearest, DistanceToBoundary(point));
            return nearest;
        }

        /// <summary>
        /// Separating-axis test: two convex outlines overlap unless some edge direction of either one splits them apart.
        /// </summary>
        public bool Overlaps(ConvexPolygon other)
        {
            return !HasSeparatingEdge(this, other) && !HasSeparatingEdge(other, this);
        }

        private static bool HasSeparatingEdge(ConvexPolygon edgesFrom, ConvexPolygon other)
        {
            Vector2[] edgePoints = edgesFrom.points;
            for (int index = 0; index < edgePoints.Length; index++)
            {
                Vector2 edge = edgePoints[(index + 1) % edgePoints.Length] - edgePoints[index];
                Vector2 axis = new Vector2(-edge.y, edge.x);
                Project(edgePoints, axis, out float firstMinimum, out float firstMaximum);
                Project(other.points, axis, out float secondMinimum, out float secondMaximum);
                if (firstMaximum < secondMinimum || secondMaximum < firstMinimum) return true;
            }
            return false;
        }

        private static void Project(Vector2[] projected, Vector2 axis, out float minimum, out float maximum)
        {
            minimum = float.MaxValue;
            maximum = float.MinValue;
            foreach (Vector2 point in projected)
            {
                float position = Vector2.Dot(point, axis);
                minimum = Mathf.Min(minimum, position);
                maximum = Mathf.Max(maximum, position);
            }
        }

        public float DistanceToBoundary(Vector2 point)
        {
            float nearest = float.MaxValue;
            for (int index = 0; index < points.Length; index++) nearest = Mathf.Min(nearest, DistanceToSegment(point, points[index], points[(index + 1) % points.Length]));
            return nearest;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float share = lengthSquared < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * share);
        }

        /// <summary>
        /// Points every "step" pixels around the outline, for measuring how much of it touches something.
        /// </summary>
        public List<Vector2> SamplePerimeter(float step)
        {
            var samples = new List<Vector2>();
            for (int index = 0; index < points.Length; index++)
            {
                Vector2 start = points[index];
                Vector2 end = points[(index + 1) % points.Length];
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / step));
                for (int sample = 0; sample < count; sample++) samples.Add(start + (end - start) * (sample / (float)count));
            }
            return samples;
        }

        public List<Edge> Edges()
        {
            var edges = new List<Edge>();
            for (int index = 0; index < points.Length; index++) edges.Add(new Edge(points[index], points[(index + 1) % points.Length]));
            return edges;
        }

        /// <summary>
        /// The convex hull of a set of points (Andrew's monotone chain).
        /// </summary>
        public static ConvexPolygon Hull(List<Vector2> cloud)
        {
            var sorted = new List<Vector2>(cloud);
            sorted.Sort((first, second) => first.x != second.x ? first.x.CompareTo(second.x) : first.y.CompareTo(second.y));
            if (sorted.Count < 3) return new ConvexPolygon(sorted.ToArray());

            var hull = new List<Vector2>();
            foreach (Vector2 point in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count + 1;
            for (int index = sorted.Count - 2; index >= 0; index--)
            {
                Vector2 point = sorted[index];
                while (hull.Count >= lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            hull.RemoveAt(hull.Count - 1);
            return new ConvexPolygon(hull.ToArray());
        }

        private static float Cross(Vector2 origin, Vector2 first, Vector2 second)
        {
            return (first.x - origin.x) * (second.y - origin.y) - (first.y - origin.y) * (second.x - origin.x);
        }

        /// <summary>
        /// Douglas-Peucker on a closed ring: keeps only the corners that stick out more than "tolerance" pixels,
        /// so a crystal is described by its handful of real edges.
        /// </summary>
        public static List<Vector2> SimplifyRing(IList<Vector2> ring, float tolerance)
        {
            if (ring.Count <= 3) return new List<Vector2>(ring);
            // split the ring at its two farthest-apart points and simplify each half as an open line
            int farthestIndex = 0;
            float farthestDistance = -1f;
            for (int index = 1; index < ring.Count; index++)
            {
                float distance = Vector2.Distance(ring[0], ring[index]);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthestIndex = index;
                }
            }
            var firstHalf = new List<Vector2>();
            for (int index = 0; index <= farthestIndex; index++) firstHalf.Add(ring[index]);
            var secondHalf = new List<Vector2>();
            for (int index = farthestIndex; index < ring.Count; index++) secondHalf.Add(ring[index]);
            secondHalf.Add(ring[0]);

            List<Vector2> simplifiedFirst = SimplifyLine(firstHalf, tolerance);
            List<Vector2> simplifiedSecond = SimplifyLine(secondHalf, tolerance);
            simplifiedFirst.RemoveAt(simplifiedFirst.Count - 1);
            simplifiedSecond.RemoveAt(simplifiedSecond.Count - 1);
            simplifiedFirst.AddRange(simplifiedSecond);
            return simplifiedFirst;
        }

        public static List<Vector2> SimplifyLine(List<Vector2> line, float tolerance)
        {
            var keep = new bool[line.Count];
            keep[0] = true;
            keep[line.Count - 1] = true;
            var pending = new Stack<(int first, int last)>();
            pending.Push((0, line.Count - 1));
            while (pending.Count > 0)
            {
                (int first, int last) = pending.Pop();
                float furthest = 0f;
                int furthestIndex = -1;
                for (int index = first + 1; index < last; index++)
                {
                    float distance = DistanceToSegment(line[index], line[first], line[last]);
                    if (distance > furthest)
                    {
                        furthest = distance;
                        furthestIndex = index;
                    }
                }
                if (furthestIndex < 0 || furthest <= tolerance) continue;
                keep[furthestIndex] = true;
                pending.Push((first, furthestIndex));
                pending.Push((furthestIndex, last));
            }
            var simplified = new List<Vector2>();
            for (int index = 0; index < line.Count; index++) if (keep[index]) simplified.Add(line[index]);
            return simplified;
        }
    }
}
