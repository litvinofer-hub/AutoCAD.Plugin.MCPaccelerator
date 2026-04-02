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

        
    }
}
