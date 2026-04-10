using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.Utils.GeometryModel
{
    public class Polyline(List<Point> points)
    {
        public List<Point> Points { get; set; } = points;

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

        // =====================================================================
        // 2D geometry queries (project to XY plane, ignore Z).
        //
        // These methods treat the polyline as a 2D shape on the XY plane.
        // Rectangle-specific queries assume Points[0..3] describe a 4-vertex
        // rectangle in order (either clockwise or counter-clockwise).
        // =====================================================================

        /// <summary>
        /// Centroid (average of all vertices) projected onto the XY plane.
        /// </summary>
        public Vec2 Center2D()
        {
            double sumX = 0, sumY = 0;
            foreach (var p in Points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            return new Vec2(sumX / Points.Count, sumY / Points.Count);
        }

        /// <summary>
        /// Unit vector along the polyline's long axis, assuming it is a rectangle
        /// defined by the first 4 points. Returns <c>null</c> if there are fewer than 4 points.
        /// </summary>
        public Vec2? LongAxisDirection2D()
        {
            if (Points.Count < 4) return null;

            var p0 = new Vec2(Points[0].X, Points[0].Y);
            var p1 = new Vec2(Points[1].X, Points[1].Y);
            var p2 = new Vec2(Points[2].X, Points[2].Y);

            double side1 = Vec2Math.Distance(p0, p1);
            double side2 = Vec2Math.Distance(p1, p2);

            var (start, end) = side1 >= side2 ? (p0, p1) : (p1, p2);
            return Vec2Math.Normalize(Vec2Math.Subtract(end, start));
        }

        /// <summary>
        /// Treats the polyline as a rectangle (first 4 vertices) and extracts its
        /// center line and thickness using the long-axis heuristic. Safe only when
        /// length &gt; thickness.
        /// </summary>
        public bool TryLongAxisRect2D(out Vec2 start, out Vec2 end, out double thickness)
        {
            start = end = Vec2.Zero;
            thickness = 0;
            if (Points.Count < 4) return false;

            var p0 = new Vec2(Points[0].X, Points[0].Y);
            var p1 = new Vec2(Points[1].X, Points[1].Y);
            var p2 = new Vec2(Points[2].X, Points[2].Y);
            var p3 = new Vec2(Points[3].X, Points[3].Y);

            double side1 = Vec2Math.Distance(p0, p1);
            double side2 = Vec2Math.Distance(p1, p2);

            if (side1 >= side2)
            {
                start = Vec2Math.Mid(p0, p3);
                end = Vec2Math.Mid(p1, p2);
                thickness = side2;
            }
            else
            {
                start = Vec2Math.Mid(p0, p1);
                end = Vec2Math.Mid(p3, p2);
                thickness = side1;
            }
            return true;
        }

        /// <summary>
        /// Signed scalar projection of the polyline's center onto <paramref name="direction"/>.
        /// Useful for sorting polylines along a shared axis.
        /// </summary>
        public double ProjectCenter2D(Vec2 direction)
            => Vec2Math.Dot(Center2D(), direction);

        /// <summary>
        /// Extent (max − min) of the polyline's vertices along an axis on the XY plane.
        /// </summary>
        public double Extent2D(Vec2 axis)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var p in Points)
            {
                double t = p.X * axis.X + p.Y * axis.Y;
                if (t < min) min = t;
                if (t > max) max = t;
            }
            return max - min;
        }

        /// <summary>
        /// Length of the shorter of the first two sides (the rectangular short side).
        /// Returns <see cref="double.MaxValue"/> if there are fewer than 4 points.
        /// </summary>
        public double MinSide2D()
        {
            if (Points.Count < 4) return double.MaxValue;
            var p0 = new Vec2(Points[0].X, Points[0].Y);
            var p1 = new Vec2(Points[1].X, Points[1].Y);
            var p2 = new Vec2(Points[2].X, Points[2].Y);
            double s1 = Vec2Math.Distance(p0, p1);
            double s2 = Vec2Math.Distance(p1, p2);
            return s1 < s2 ? s1 : s2;
        }

        /// <summary>
        /// Minimum 2D distance between any vertex of this polyline and any vertex
        /// of <paramref name="other"/>. Used to detect touching/adjacent polylines.
        /// </summary>
        public double MinVertexDistance2D(Polyline other)
        {
            double min = double.MaxValue;
            foreach (var a in Points)
            {
                foreach (var b in other.Points)
                {
                    double dx = a.X - b.X;
                    double dy = a.Y - b.Y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 < min) min = d2;
                }
            }
            return System.Math.Sqrt(min);
        }
    }
}
