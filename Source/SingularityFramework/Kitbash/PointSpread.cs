using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// How a cloud of points is spread out: its middle, the direction it spreads the most (major) and the one across
    /// that (minor), and how much it spreads along each (variance). Used to fit a piece to a gap's shape, and to find
    /// which way a crystal points.
    /// </summary>
    public struct PointSpread
    {
        public Vector2 center;
        public Vector2 major;
        public Vector2 minor;
        public float majorVariance;
        public float minorVariance;

        public float MajorAngle => Mathf.Atan2(major.y, major.x);

        /// <summary>
        /// About the full length of the cloud along its major direction (for an even spread, 4.2 standard deviations
        /// covers it end to end).
        /// </summary>
        public float LongExtent => 4.2f * Mathf.Sqrt(Mathf.Max(majorVariance, 1e-6f));

        public float ShortExtent => 4.2f * Mathf.Sqrt(Mathf.Max(minorVariance, 1e-6f));

        public static PointSpread Of(IList<Vector2> points)
        {
            var accumulator = new Accumulator();
            foreach (Vector2 point in points) accumulator.Add(point.x, point.y);
            return accumulator.Result();
        }

        /// <summary>
        /// Adds points one at a time (for counting pixels without making a list of them).
        /// </summary>
        public class Accumulator
        {
            private double count, sumX, sumY, sumXX, sumXY, sumYY;

            public int Count => (int)count;

            public void Add(float x, float y)
            {
                count++;
                sumX += x;
                sumY += y;
                sumXX += x * (double)x;
                sumXY += x * (double)y;
                sumYY += y * (double)y;
            }

            public PointSpread Result()
            {
                if (count < 1) return new PointSpread { major = Vector2.right, minor = new Vector2(0f, 1f) };
                double meanX = sumX / count;
                double meanY = sumY / count;
                // sample covariance (divided by count - 1), as numpy's cov does
                double divisor = count > 1 ? count - 1 : 1;
                float spreadXX = (float)((sumXX - count * meanX * meanX) / divisor);
                float spreadXY = (float)((sumXY - count * meanX * meanY) / divisor);
                float spreadYY = (float)((sumYY - count * meanY * meanY) / divisor);
                if (count <= 2)
                {
                    spreadXX = spreadYY = 1f;
                    spreadXY = 0f;
                }

                // eigenvalues and the main eigenvector of the 2x2 spread matrix
                float halfTrace = (spreadXX + spreadYY) / 2f;
                float offset = Mathf.Sqrt(Mathf.Max(0f, (spreadXX - spreadYY) * (spreadXX - spreadYY) / 4f + spreadXY * spreadXY));
                float angle = 0.5f * Mathf.Atan2(2f * spreadXY, spreadXX - spreadYY);
                Vector2 major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                return new PointSpread
                {
                    center = new Vector2((float)meanX, (float)meanY),
                    major = major,
                    minor = new Vector2(-major.y, major.x),
                    majorVariance = halfTrace + offset,
                    minorVariance = Mathf.Max(0f, halfTrace - offset)
                };
            }
        }
    }
}
