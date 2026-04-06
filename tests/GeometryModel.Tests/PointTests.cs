using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.GeometryModel
{
    public class PointTests
    {
        [Fact]
        public void Equals_SameCoordinates_ReturnsTrue()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0, 2.0, 3.0);

            Assert.True(point1.Equals(point2));
        }

        [Fact]
        public void Equals_WithinTolerance_ReturnsTrue()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0 + 1e-7, 2.0 - 1e-7, 3.0 + 1e-7);

            Assert.True(point1.Equals(point2));
        }

        [Fact]
        public void Equals_BeyondTolerance_ReturnsFalse()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0 + 1e-5, 2.0, 3.0);

            Assert.False(point1.Equals(point2));
        }

        [Fact]
        public void Equals_DifferentCoordinates_ReturnsFalse()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(4.0, 5.0, 6.0);

            Assert.False(point1.Equals(point2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var point = new Point(1.0, 2.0, 3.0);

            Assert.False(point.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var point = new Point(1.0, 2.0, 3.0);

            Assert.False(point.Equals("not a point"));
        }

        [Fact]
        public void GetHashCode_EqualPoints_SameHashCode()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0, 2.0, 3.0);

            Assert.Equal(point1.GetHashCode(), point2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_PointsWithinTolerance_SameHashCode()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0 + 1e-7, 2.0 - 1e-7, 3.0 + 1e-7);

            Assert.Equal(point1.GetHashCode(), point2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_PointsBeyondTolerance_DifferentHashCode()
        {
            var point1 = new Point(1.0, 2.0, 3.0);
            var point2 = new Point(1.0 + 1e-5, 2.0, 3.0);

            Assert.NotEqual(point1.GetHashCode(), point2.GetHashCode());
        }
        [Fact]
        public void HasDuplicates_EmptyList_ReturnsFalse()
        {
            Assert.False(Point.HasDuplicates(new List<Point>()));
        }

        [Fact]
        public void HasDuplicates_SinglePoint_ReturnsFalse()
        {
            var points = new List<Point> { new Point(1, 2, 3) };

            Assert.False(Point.HasDuplicates(points));
        }

        [Fact]
        public void HasDuplicates_AllUnique_ReturnsFalse()
        {
            var points = new List<Point>
            {
                new Point(0, 0, 0),
                new Point(1, 0, 0),
                new Point(0, 1, 0),
                new Point(0, 0, 1)
            };

            Assert.False(Point.HasDuplicates(points));
        }

        [Fact]
        public void HasDuplicates_WithExactDuplicate_ReturnsTrue()
        {
            var points = new List<Point>
            {
                new Point(0, 0, 0),
                new Point(1, 2, 3),
                new Point(0, 0, 0)
            };

            Assert.True(Point.HasDuplicates(points));
        }

        [Fact]
        public void HasDuplicates_WithinTolerance_ReturnsTrue()
        {
            var points = new List<Point>
            {
                new Point(1, 2, 3),
                new Point(1 + 1e-7, 2, 3)
            };

            Assert.True(Point.HasDuplicates(points));
        }

        [Fact]
        public void HasDuplicates_OutsideTolerance_ReturnsFalse()
        {
            var points = new List<Point>
            {
                new Point(1, 2, 3),
                new Point(1 + 1e-4, 2, 3)
            };

            Assert.False(Point.HasDuplicates(points));
        }
    }
}
