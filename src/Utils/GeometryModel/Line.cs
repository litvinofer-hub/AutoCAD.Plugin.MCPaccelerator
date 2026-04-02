namespace MCPAccelerator.Utils.GeometryModel
{
    public class Line
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }

        public Line(Point startPoint, Point endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public override bool Equals(object obj)
        {
            if (obj is Line other)
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
    }
}
