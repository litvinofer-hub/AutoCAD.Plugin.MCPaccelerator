using System;
using System.Collections.Generic;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class RoomTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        private Polygon CreateValidPolygon(double z)
        {
            var p1 = new Point(0, 0, z);
            var p2 = new Point(5, 0, z);
            var p3 = new Point(5, 5, z);
            return new Polygon(new List<Point> { p1, p2, p3 });
        }

        [Fact]
        public void Constructor_PolygonZMatchesBotLevel_Succeeds()
        {
            var bot = new Level(_buildingId, 5.0);
            var top = new Level(_buildingId, 8.0);
            var polygon = CreateValidPolygon(5.0);

            var room = new Room(_buildingId, polygon, bot, top);

            Assert.NotNull(room);
        }

        [Fact]
        public void Constructor_PolygonZDoesNotMatchBotLevel_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var polygon = CreateValidPolygon(0);

            Assert.Throws<ArgumentException>(() => new Room(_buildingId, polygon, bot, top));
        }

        [Fact]
        public void Constructor_PolygonZWithinToleranceOfBotLevel_Succeeds()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var polygon = CreateValidPolygon(3.0 + 1e-7);

            var room = new Room(_buildingId, polygon, bot, top);

            Assert.NotNull(room);
        }
    }
}
