using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.Utils.GeometryModel
{
    /// <summary>
    /// A closed quadrilateral assumed to be a rectangle, described by its 4 corners
    /// in order (clockwise or counter-clockwise). Inherits the closing and validation
    /// behavior of <see cref="Polygon"/>.
    ///
    /// All rectangle-specific geometry (long-axis direction, center line, length,
    /// thickness) is precomputed in the constructor so the values are cheap to read
    /// and self-consistent.
    ///
    /// Inputs are assumed to form a real rectangle (opposite sides equal, corners ~90°).
    /// No right-angle check is performed — callers are responsible for passing sane data.
    /// </summary>
    public class Rect : Polygon
    {
        /// <summary>Unit vector along the rectangle's long axis.</summary>
        public Vec2 Direction2D { get; }

        /// <summary>Length of the long side.</summary>
        public double Length2D { get; }

        /// <summary>Length of the short side (the rectangle's thickness).</summary>
        public double Thickness2D { get; }

        /// <summary>Start point of the center line (midpoint of one short edge).</summary>
        public Vec2 CenterLineStart2D { get; }

        /// <summary>End point of the center line (midpoint of the opposite short edge).</summary>
        public Vec2 CenterLineEnd2D { get; }

        private readonly Vec2 _center;

        public Rect(List<Point> points) : base(points)
        {
            if (Points.Take(4).Distinct().Count() < 4)
                throw new ArgumentException("A Rect must be constructed from 4 distinct corner points.");

            var p0 = new Vec2(Points[0].X, Points[0].Y);
            var p1 = new Vec2(Points[1].X, Points[1].Y);
            var p2 = new Vec2(Points[2].X, Points[2].Y);
            var p3 = new Vec2(Points[3].X, Points[3].Y);

            double side1 = Vec2Math.Distance(p0, p1);
            double side2 = Vec2Math.Distance(p1, p2);

            if (side1 >= side2)
            {
                Length2D = side1;
                Thickness2D = side2;
                Direction2D = Vec2Math.Normalize(Vec2Math.Subtract(p1, p0));
                CenterLineStart2D = Vec2Math.Mid(p0, p3);
                CenterLineEnd2D = Vec2Math.Mid(p1, p2);
            }
            else
            {
                Length2D = side2;
                Thickness2D = side1;
                Direction2D = Vec2Math.Normalize(Vec2Math.Subtract(p2, p1));
                CenterLineStart2D = Vec2Math.Mid(p0, p1);
                CenterLineEnd2D = Vec2Math.Mid(p3, p2);
            }

            _center = new Vec2(
                (p0.X + p1.X + p2.X + p3.X) / 4,
                (p0.Y + p1.Y + p2.Y + p3.Y) / 4);
        }

        /// <summary>
        /// Centroid of the 4 corners. Overrides <see cref="Polyline.Center2D"/> so
        /// the closing duplicate point added by <see cref="Polygon"/> doesn't bias
        /// the result toward the first corner.
        /// </summary>
        public override Vec2 Center2D() => _center;
    }
}
