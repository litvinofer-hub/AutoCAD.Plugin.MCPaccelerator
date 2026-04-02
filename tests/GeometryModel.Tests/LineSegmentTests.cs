using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.GeometryModel
{
    public class LineSegmentTests
    {
        [Fact]
        public void Equals_SameStartAndEndPoints_ReturnsTrue()
        {
            var line1 = new LineSegment(new Point(0, 0), new Point(1, 1));
            var line2 = new LineSegment(new Point(0, 0), new Point(1, 1));

            Assert.True(line1.Equals(line2));
        }

        [Fact]
        public void Equals_ReversedPoints_ReturnsTrue()
        {
            var line1 = new LineSegment(new Point(0, 0), new Point(1, 1));
            var line2 = new LineSegment(new Point(1, 1), new Point(0, 0));

            Assert.True(line1.Equals(line2));
        }

        [Fact]
        public void Equals_DifferentLines_ReturnsFalse()
        {
            var line1 = new LineSegment(new Point(0, 0), new Point(1, 1));
            var line2 = new LineSegment(new Point(0, 0), new Point(2, 2));

            Assert.False(line1.Equals(line2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var line = new LineSegment(new Point(0, 0), new Point(1, 1));

            Assert.False(line.Equals(null));
        }

        [Fact]
        public void GetHashCode_ReversedLines_SameHashCode()
        {
            var line1 = new LineSegment(new Point(0, 0), new Point(1, 1));
            var line2 = new LineSegment(new Point(1, 1), new Point(0, 0));

            Assert.Equal(line1.GetHashCode(), line2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_EqualLines_SameHashCode()
        {
            var line1 = new LineSegment(new Point(0, 0), new Point(1, 1));
            var line2 = new LineSegment(new Point(0, 0), new Point(1, 1));

            Assert.Equal(line1.GetHashCode(), line2.GetHashCode());
        }
        [Fact]
        public void IsPointOnSegment2D_PointOnSegment_ReturnsTrue()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));
            var point = new Point(5, 0);

            Assert.True(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_StartPoint_ReturnsTrue()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));

            Assert.True(segment.IsPointOnSegment2D(new Point(0, 0)));
        }

        [Fact]
        public void IsPointOnSegment2D_EndPoint_ReturnsTrue()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));

            Assert.True(segment.IsPointOnSegment2D(new Point(10, 0)));
        }

        [Fact]
        public void IsPointOnSegment2D_PointOffSegment_ReturnsFalse()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));
            var point = new Point(5, 1);

            Assert.False(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_PointBeyondEnd_ReturnsFalse()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));
            var point = new Point(11, 0);

            Assert.False(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_PointBeforeStart_ReturnsFalse()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));
            var point = new Point(-1, 0);

            Assert.False(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_DiagonalSegment_PointOnIt_ReturnsTrue()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 10));
            var point = new Point(5, 5);

            Assert.True(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_IgnoresZCoordinate()
        {
            var segment = new LineSegment(new Point(0, 0, 0), new Point(10, 0, 0));
            var point = new Point(5, 0, 99);

            Assert.True(segment.IsPointOnSegment2D(point));
        }

        [Fact]
        public void IsPointOnSegment2D_PointWithinTolerance_ReturnsTrue()
        {
            var segment = new LineSegment(new Point(0, 0), new Point(10, 0));
            var point = new Point(5, 1e-7);

            Assert.True(segment.IsPointOnSegment2D(point));
        }
    }
}
