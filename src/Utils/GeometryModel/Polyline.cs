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
        // These methods treat the polyline as a 2D shape on the XY plane and make
        // no assumptions about its shape. Rectangle-specific queries (long axis,
        // thickness, center line) live on <see cref="Rect"/>.
        // =====================================================================

        /// <summary>
        /// Centroid (average of all vertices) projected onto the XY plane.
        /// Virtual so subclasses can correct for closing-point duplication
        /// (see <see cref="Rect.Center2D"/>).
        /// </summary>
        public virtual Vec2 Center2D()
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
