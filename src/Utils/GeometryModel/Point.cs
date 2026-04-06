using System;
using System.Collections.Generic;

namespace MCPAccelerator.Utils.GeometryModel
{
    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object obj)
        {
            if (obj is Point other)
            {
                return Math.Abs(X - other.X) < GeometrySettings.Tolerance
                    && Math.Abs(Y - other.Y) < GeometrySettings.Tolerance
                    && Math.Abs(Z - other.Z) < GeometrySettings.Tolerance;
            }

            return false;
        }

        /// <summary>
        /// Returns the 2D distance (X, Y only) between this point and another point.
        /// </summary>
        public double Distance2D(Point other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override int GetHashCode()
        {
            double roundFactor = 1.0 / GeometrySettings.Tolerance;
            int hx = (Math.Round(X * roundFactor)).GetHashCode();
            int hy = (Math.Round(Y * roundFactor)).GetHashCode();
            int hz = (Math.Round(Z * roundFactor)).GetHashCode();

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + hx;
                hash = hash * 31 + hy;
                hash = hash * 31 + hz;
                return hash;
            }
        }

        /// <summary>
        /// Checks whether the list contains any two distinct points with equal coordinates.
        /// Uses a HashSet for O(n) performance.
        /// </summary>
        public static bool HasDuplicates(List<Point> points)
        {
            var seen = new HashSet<Point>();

            foreach (var point in points)
            {
                if (!seen.Add(point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
