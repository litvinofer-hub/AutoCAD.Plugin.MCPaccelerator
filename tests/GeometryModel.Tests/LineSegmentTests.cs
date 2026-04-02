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
    }
}
