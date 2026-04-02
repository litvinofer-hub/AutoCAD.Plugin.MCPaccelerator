using System;

namespace MCPAccelerator.Utils.GeometryModel
{
    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point(double x, double y, double z = 0)
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
    }
}
