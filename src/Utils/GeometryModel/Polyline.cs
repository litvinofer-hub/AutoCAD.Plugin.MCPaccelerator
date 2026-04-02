using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.Utils.GeometryModel
{
    public class Polyline
    {
        public List<Point> Points { get; set; }

        public Polyline(List<Point> points)
        {
            Points = points;
        }

        public override bool Equals(object obj)
        {
            if (obj is Polyline other)
            {
                return Points.Count == other.Points.Count
                    && Points.SequenceEqual(other.Points);
            }

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var point in Points)
                {
                    hash = hash * 31 + point.GetHashCode();
                }
                return hash;
            }
        }
    }
}
