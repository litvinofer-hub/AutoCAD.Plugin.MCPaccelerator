using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.GeometryModel
{
    public class PolygonTests
    {
        [Fact]
        public void Constructor_AutoClose_LastPointIsSameReference()
        {
            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(1, 1)
            });

            var first = polygon.Points[0];
            var last = polygon.Points[polygon.Points.Count - 1];

            Assert.Same(first, last);
        }

        [Fact]
        public void Constructor_AlreadyClosedSameReference_DoesNotAddDuplicate()
        {
            var firstPoint = new Point(0, 0);
            var polygon = new Polygon(new List<Point>
            {
                firstPoint, new Point(1, 0), new Point(1, 1), firstPoint
            });

            Assert.Equal(4, polygon.Points.Count);
        }

        [Fact]
        public void Constructor_AlreadyClosedDifferentReference_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 0)
            }));
        }

        [Fact]
        public void Constructor_LessThan3DistinctPoints_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0)
            }));
        }

        [Fact]
        public void Constructor_3PointsBut2Distinct_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(0, 0)
            }));
        }

        [Fact]
        public void Constructor_DuplicatePointsWithinTolerance_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(0, 1e-7)
            }));
        }

        [Fact]
        public void Constructor_3DistinctPoints_CreatesSuccessfully()
        {
            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(0, 1)
            });

            Assert.Equal(4, polygon.Points.Count);
        }

        [Fact]
        public void Constructor_EmptyList_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new Polygon(new List<Point>()));
        }
    }
}
