using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.Utils.GeometryModel
{
    public class Polygon : Polyline
    {
        public Polygon(List<Point> points) : base(points)
        {
            ValidateMinimumPoints();
            Close();
        }

        private void ValidateMinimumPoints()
        {
            int distinctCount = Points.Distinct().Count();

            if (distinctCount < 3)
            {
                throw new ArgumentException("A polygon must contain at least 3 different points.");
            }
        }

        private void Close()
        {
            if (Points.Count > 2)
            {
                var first = Points[0];
                var last = Points[Points.Count - 1];

                if (first.Equals(last) && !ReferenceEquals(first, last))
                {
                    throw new ArgumentException("Last point is equal to the first point but is not the same reference. Use the same Point object to close the polygon.");
                }
                else if (!first.Equals(last))
                {
                    Points.Add(first);
                }
            }
        }
    }
}
