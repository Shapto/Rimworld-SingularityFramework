using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// A yes/no grid the size of a canvas: where a shape is, where pieces may go, what's covered so far.
    /// Row 0 is the top of the image. Grow, shrink, fill holes, split into pieces and trace outlines.
    /// </summary>
    public class PixelMask
    {
        public readonly int width;
        public readonly int height;
        public readonly bool[] cells;

        public PixelMask(int width, int height)
        {
            this.width = width;
            this.height = height;
            cells = new bool[width * height];
        }

        public PixelMask(int width, int height, bool[] cells)
        {
            this.width = width;
            this.height = height;
            this.cells = cells;
        }

        public bool this[int column, int row]
        {
            get => column >= 0 && row >= 0 && column < width && row < height && cells[row * width + column];
            set => cells[row * width + column] = value;
        }

        public PixelMask Copy()
        {
            return new PixelMask(width, height, (bool[])cells.Clone());
        }

        public int Count()
        {
            int count = 0;
            foreach (bool cell in cells) if (cell) count++;
            return count;
        }

        public bool Any()
        {
            foreach (bool cell in cells) if (cell) return true;
            return false;
        }

        public bool TryGetRowRange(out int firstRow, out int lastRow)
        {
            firstRow = lastRow = -1;
            for (int index = 0; index < cells.Length; index++)
            {
                if (!cells[index]) continue;
                int row = index / width;
                if (firstRow < 0) firstRow = row;
                lastRow = row;
            }
            return firstRow >= 0;
        }

        public void Fill(List<PixelSpan> spans)
        {
            foreach (PixelSpan span in spans)
            {
                int rowStart = span.row * width;
                for (int column = span.firstColumn; column <= span.lastColumn; column++) cells[rowStart + column] = true;
            }
        }

        public int CountIn(List<PixelSpan> spans)
        {
            int count = 0;
            foreach (PixelSpan span in spans)
            {
                int rowStart = span.row * width;
                for (int column = span.firstColumn; column <= span.lastColumn; column++) if (cells[rowStart + column]) count++;
            }
            return count;
        }

        public static int CountSpans(List<PixelSpan> spans)
        {
            int count = 0;
            foreach (PixelSpan span in spans) count += span.lastColumn - span.firstColumn + 1;
            return count;
        }

        public PixelMask And(PixelMask other) => Combine(other, (first, second) => first && second);
        public PixelMask Or(PixelMask other) => Combine(other, (first, second) => first || second);
        public PixelMask AndNot(PixelMask other) => Combine(other, (first, second) => first && !second);

        public PixelMask Inverted()
        {
            var result = new PixelMask(width, height);
            for (int index = 0; index < cells.Length; index++) result.cells[index] = !cells[index];
            return result;
        }

        private PixelMask Combine(PixelMask other, Func<bool, bool, bool> rule)
        {
            var result = new PixelMask(width, height);
            for (int index = 0; index < cells.Length; index++) result.cells[index] = rule(cells[index], other.cells[index]);
            return result;
        }

        // ----- growing and shrinking -----

        /// <summary>
        /// Grows the shape by "steps" pixels in the four straight directions (a diamond), like scipy's binary_dilation.
        /// </summary>
        public PixelMask Dilated(int steps)
        {
            if (steps <= 0) return Copy();
            int[] distance = StepsTo(true);
            var result = new PixelMask(width, height);
            for (int index = 0; index < cells.Length; index++) result.cells[index] = distance[index] <= steps;
            return result;
        }

        /// <summary>
        /// Shrinks the shape by "steps" pixels; the canvas edge counts as empty.
        /// </summary>
        public PixelMask Eroded(int steps)
        {
            if (steps <= 0) return Copy();
            int[] distance = StepsTo(false);
            var result = new PixelMask(width, height);
            for (int index = 0; index < cells.Length; index++) result.cells[index] = distance[index] > steps;
            return result;
        }

        /// <summary>
        /// Removes anything thinner than about twice "steps", keeping the rest the same shape.
        /// </summary>
        public PixelMask Opened(int steps) => Eroded(steps).Dilated(steps);

        /// <summary>
        /// Closes gaps narrower than about twice "steps".
        /// </summary>
        public PixelMask Closed(int steps) => Dilated(steps).Eroded(steps);

        /// <summary>
        /// For every pixel, how many straight steps it is to the nearest pixel that is "solid" (true) or empty (false).
        /// For empty, the area outside the canvas counts as empty.
        /// </summary>
        private int[] StepsTo(bool solid)
        {
            const int Far = int.MaxValue / 4;
            var distance = new int[cells.Length];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int index = row * width + column;
                    if (cells[index] == solid)
                    {
                        distance[index] = 0;
                        continue;
                    }
                    int best = Far;
                    if (!solid && (row == 0 || column == 0)) best = 1;
                    if (row > 0) best = Math.Min(best, distance[index - width] + 1);
                    if (column > 0) best = Math.Min(best, distance[index - 1] + 1);
                    distance[index] = best;
                }
            }
            for (int row = height - 1; row >= 0; row--)
            {
                for (int column = width - 1; column >= 0; column--)
                {
                    int index = row * width + column;
                    if (distance[index] == 0) continue;
                    int best = distance[index];
                    if (!solid && (row == height - 1 || column == width - 1)) best = Math.Min(best, 1);
                    if (row < height - 1) best = Math.Min(best, distance[index + width] + 1);
                    if (column < width - 1) best = Math.Min(best, distance[index + 1] + 1);
                    distance[index] = best;
                }
            }
            return distance;
        }

        // ----- holes and pieces -----

        /// <summary>
        /// The shape with every hole filled in: empty areas not connected to the canvas edge.
        /// </summary>
        public PixelMask HolesFilled()
        {
            var outside = new bool[cells.Length];
            var queue = new Queue<int>();
            for (int column = 0; column < width; column++)
            {
                SpreadOutside(column, 0, outside, queue);
                SpreadOutside(column, height - 1, outside, queue);
            }
            for (int row = 0; row < height; row++)
            {
                SpreadOutside(0, row, outside, queue);
                SpreadOutside(width - 1, row, outside, queue);
            }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int column = index % width;
                int row = index / width;
                if (column > 0) SpreadOutside(column - 1, row, outside, queue);
                if (column < width - 1) SpreadOutside(column + 1, row, outside, queue);
                if (row > 0) SpreadOutside(column, row - 1, outside, queue);
                if (row < height - 1) SpreadOutside(column, row + 1, outside, queue);
            }
            var result = new PixelMask(width, height);
            for (int index = 0; index < cells.Length; index++) result.cells[index] = !outside[index];
            return result;
        }

        private void SpreadOutside(int column, int row, bool[] outside, Queue<int> queue)
        {
            int index = row * width + column;
            if (cells[index] || outside[index]) return;
            outside[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// Splits the shape into its separate pieces (touching side to side). Returns each pixel's piece number
        /// (0 = empty, pieces from 1) and how many pieces there are.
        /// </summary>
        public int[] Label(out int pieceCount)
        {
            var labels = new int[cells.Length];
            pieceCount = 0;
            var queue = new Queue<int>();
            for (int start = 0; start < cells.Length; start++)
            {
                if (!cells[start] || labels[start] != 0) continue;
                pieceCount++;
                labels[start] = pieceCount;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int column = index % width;
                    int row = index / width;
                    if (column > 0) JoinPiece(index - 1, pieceCount, labels, queue);
                    if (column < width - 1) JoinPiece(index + 1, pieceCount, labels, queue);
                    if (row > 0) JoinPiece(index - width, pieceCount, labels, queue);
                    if (row < height - 1) JoinPiece(index + width, pieceCount, labels, queue);
                }
            }
            return labels;
        }

        private void JoinPiece(int index, int piece, int[] labels, Queue<int> queue)
        {
            if (!cells[index] || labels[index] != 0) return;
            labels[index] = piece;
            queue.Enqueue(index);
        }

        /// <summary>
        /// For every pixel, the index of a nearby solid pixel (itself if it's solid; -1 if there are none at all).
        /// Used to fill seams and bare spots with the colour of the nearest crystal.
        /// </summary>
        public int[] NearestSolid()
        {
            var nearest = new int[cells.Length];
            var queue = new Queue<int>();
            for (int index = 0; index < cells.Length; index++)
            {
                nearest[index] = cells[index] ? index : -1;
                if (cells[index]) queue.Enqueue(index);
            }
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int column = index % width;
                int row = index / width;
                for (int rowStep = -1; rowStep <= 1; rowStep++)
                {
                    for (int columnStep = -1; columnStep <= 1; columnStep++)
                    {
                        int neighbourColumn = column + columnStep;
                        int neighbourRow = row + rowStep;
                        if (neighbourColumn < 0 || neighbourRow < 0 || neighbourColumn >= width || neighbourRow >= height) continue;
                        int neighbour = neighbourRow * width + neighbourColumn;
                        if (nearest[neighbour] != -1) continue;
                        nearest[neighbour] = nearest[index];
                        queue.Enqueue(neighbour);
                    }
                }
            }
            return nearest;
        }

        // ----- outlines -----

        /// <summary>
        /// Every outline in the shape, outer ones and those around holes, as closed rings of pixel corners.
        /// Built from the cracks between solid and empty pixels, each walked with the solid side on its right.
        /// </summary>
        public List<List<Vector2>> OutlineRings()
        {
            var cracks = new CrackGraph(width + 1);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    if (!cells[row * width + column]) continue;
                    int topLeft = row * (width + 1) + column;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + width + 1;
                    int bottomRight = bottomLeft + 1;
                    if (!this[column, row - 1]) cracks.Add(topLeft, topRight);
                    if (!this[column + 1, row]) cracks.Add(topRight, bottomRight);
                    if (!this[column, row + 1]) cracks.Add(bottomRight, bottomLeft);
                    if (!this[column - 1, row]) cracks.Add(bottomLeft, topLeft);
                }
            }
            return cracks.WalkRings();
        }

        /// <summary>
        /// The cracks between solid and empty pixels, each one pixel side long, joined end to end into rings.
        /// </summary>
        private class CrackGraph
        {
            private readonly int cornerWidth;
            private readonly List<int> starts = new List<int>();
            private readonly List<int> ends = new List<int>();
            private readonly Dictionary<int, List<int>> leaving = new Dictionary<int, List<int>>();

            public CrackGraph(int cornerWidth)
            {
                this.cornerWidth = cornerWidth;
            }

            public void Add(int start, int end)
            {
                if (!leaving.TryGetValue(start, out List<int> list))
                {
                    list = new List<int>();
                    leaving[start] = list;
                }
                list.Add(starts.Count);
                starts.Add(start);
                ends.Add(end);
            }

            public List<List<Vector2>> WalkRings()
            {
                var used = new bool[starts.Count];
                var rings = new List<List<Vector2>>();
                for (int firstCrack = 0; firstCrack < starts.Count; firstCrack++)
                {
                    if (used[firstCrack]) continue;
                    var ring = new List<Vector2>();
                    int crack = firstCrack;
                    while (crack >= 0 && !used[crack])
                    {
                        used[crack] = true;
                        ring.Add(Corner(starts[crack]));
                        crack = NextCrack(crack, used);
                    }
                    if (ring.Count >= 4) rings.Add(ring);
                }
                return rings;
            }

            private Vector2 Corner(int corner)
            {
                return new Vector2(corner % cornerWidth, corner / cornerWidth);
            }

            /// <summary>
            /// Where two pieces touch only at a corner; turn the tightest way, so each piece keeps its own ring.
            /// </summary>
            private int NextCrack(int crack, bool[] used)
            {
                if (!leaving.TryGetValue(ends[crack], out List<int> candidates)) return -1;
                Vector2 direction = Corner(ends[crack]) - Corner(starts[crack]);
                int best = -1;
                float bestTurn = float.MinValue;
                foreach (int candidate in candidates)
                {
                    if (used[candidate]) continue;
                    Vector2 candidateDirection = Corner(ends[candidate]) - Corner(starts[candidate]);
                    float turn = direction.x * candidateDirection.y - direction.y * candidateDirection.x;   // positive: turning right (y is down)
                    if (turn > bestTurn)
                    {
                        bestTurn = turn;
                        best = candidate;
                    }
                }
                return best;
            }
        }
    }
}
