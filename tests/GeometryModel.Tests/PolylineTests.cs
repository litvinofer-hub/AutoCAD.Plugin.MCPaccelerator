using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.GeometryModel
{
    public class PolylineTests
    {
        [Fact]
        public void Equals_SamePointsSameOrder_ReturnsTrue()
        {
            var polyline1 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });
            var polyline2 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });

            Assert.True(polyline1.Equals(polyline2));
        }

        [Fact]
        public void Equals_SamePointsDifferentOrder_ReturnsFalse()
        {
            var polyline1 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });
            var polyline2 = new Polyline(new List<Point>
            {
                new Point(2, 2, 0), new Point(1, 1, 0), new Point(0, 0, 0)
            });

            Assert.False(polyline1.Equals(polyline2));
        }

        [Fact]
        public void Equals_DifferentPointCount_ReturnsFalse()
        {
            var polyline1 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0)
            });
            var polyline2 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });

            Assert.False(polyline1.Equals(polyline2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var polyline = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0)
            });

            Assert.False(polyline.Equals(null));
        }

        [Fact]
        public void GetHashCode_EqualPolylines_SameHashCode()
        {
            var polyline1 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });
            var polyline2 = new Polyline(new List<Point>
            {
                new Point(0, 0, 0), new Point(1, 1, 0), new Point(2, 2, 0)
            });

            Assert.Equal(polyline1.GetHashCode(), polyline2.GetHashCode()); 
        }
    }
}
