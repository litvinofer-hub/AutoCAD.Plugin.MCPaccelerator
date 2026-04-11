namespace MCPAccelerator.Utils.GeometryModel
{
    public class LineSegment
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        public LineSegment(Point startPoint, Point endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public override bool Equals(object obj)
        {
            if (obj is LineSegment other)
            {
                bool sameDirection = StartPoint.Equals(other.StartPoint) && EndPoint.Equals(other.EndPoint);
                bool reversed = StartPoint.Equals(other.EndPoint) && EndPoint.Equals(other.StartPoint);
                return sameDirection || reversed;
            }

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // Use addition so that order doesn't matter (A+B == B+A)
                return StartPoint.GetHashCode() + EndPoint.GetHashCode();
            }
        }

        /// <summary>
        /// Checks if a point lies on this line segment by verifying that
        /// distance(start, point) + distance(point, end) ≈ distance(start, end).
        /// Uses 2D distance (X, Y only).
        /// </summary>
        public bool IsPointOnSegment2D(Point point)
        {
            double dStartToPoint = StartPoint.Distance2D(point);
            double dPointToEnd = point.Distance2D(EndPoint);
            double dTotal = StartPoint.Distance2D(EndPoint);

            return GeometrySettings.AreEqual(dStartToPoint + dPointToEnd, dTotal);
        }

        /// <summary>
        /// Inflates this segment into a 2D rectangle of the given <paramref name="thickness"/>,
        /// centered on the segment. All four corners are returned at Z = 0 — this is a pure
        /// floor-plan (2D) helper; any Z on the source points is dropped.
        /// </summary>
        public Rect ToRect(double thickness)
        {
            var s = new Vec2(StartPoint.X, StartPoint.Y);
            var e = new Vec2(EndPoint.X, EndPoint.Y);
            var dir = Vec2Math.Normalize(Vec2Math.Subtract(e, s));
            var perp = Vec2Math.Perpendicular(dir);
            double half = thickness / 2.0;

            var p0 = new Point(s.X + perp.X * half, s.Y + perp.Y * half, 0);
            var p1 = new Point(e.X + perp.X * half, e.Y + perp.Y * half, 0);
            var p2 = new Point(e.X - perp.X * half, e.Y - perp.Y * half, 0);
            var p3 = new Point(s.X - perp.X * half, s.Y - perp.Y * half, 0);

            return new Rect([p0, p1, p2, p3]);
        }
    }
}
