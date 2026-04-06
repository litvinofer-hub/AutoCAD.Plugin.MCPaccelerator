using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class BuildingNoDuplicatePointsTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        // --- Building.GetPoints tests ---

        [Fact]
        public void GetPoints_EmptyBuilding_ReturnsEmpty()
        {
            var building = new Building();

            Assert.Empty(building.GetPoints());
        }

        [Fact]
        public void GetPoints_WithRoom_ReturnsPolygonPoints()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0, 0),
                new Point(5, 0, 0),
                new Point(5, 5, 0),
                new Point(0, 5, 0)
            });
            building.Rooms.Add(new Room(_buildingId, polygon, bot, top));

            var points = building.GetPoints().ToList();

            // 4 unique points (closing point excluded)
            Assert.Equal(4, points.Count);
        }

        [Fact]
        public void GetPoints_WithWall_ReturnsLineEndpoints()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(10, 0, 0));
            building.Walls.Add(new Wall(_buildingId, wallLine, 0.2, bot, top));

            var points = building.GetPoints().ToList();

            Assert.Equal(2, points.Count);
        }

        [Fact]
        public void GetPoints_WithWallAndOpening_ReturnsAllEndpoints()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(10, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);
            var openingLine = new LineSegment(new Point(1, 0, 0), new Point(2, 0, 0));
            wall.AddOpening(new Window(Guid.Empty, 2.0, openingLine));
            building.Walls.Add(wall);

            var points = building.GetPoints().ToList();

            // Wall: 2 + Opening: 2 = 4
            Assert.Equal(4, points.Count);
        }

        [Fact]
        public void GetPoints_WithRoomAndWall_ReturnsAllPoints()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);

            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0, 0),
                new Point(5, 0, 0),
                new Point(5, 5, 0)
            });
            building.Rooms.Add(new Room(_buildingId, polygon, bot, top));

            var wallLine = new LineSegment(new Point(10, 0, 0), new Point(20, 0, 0));
            building.Walls.Add(new Wall(_buildingId, wallLine, 0.2, bot, top));

            var points = building.GetPoints().ToList();

            // Polygon: 3 (closing excluded) + Wall: 2 = 5
            Assert.Equal(5, points.Count);
        }

        // --- Integration: GetPoints + HasDuplicates ---

        [Fact]
        public void Building_WithUniquePoints_HasNoDuplicates()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);

            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0, 0),
                new Point(10, 0, 0),
                new Point(10, 10, 0),
                new Point(0, 10, 0)
            });
            building.Rooms.Add(new Room(_buildingId, polygon, bot, top));

            var wallLine = new LineSegment(new Point(20, 0, 0), new Point(30, 0, 0));
            building.Walls.Add(new Wall(_buildingId, wallLine, 0.2, bot, top));

            Assert.False(Point.HasDuplicates(building.GetPoints().ToList()));
        }

        [Fact]
        public void Building_WithDuplicatePointsAcrossElements_DetectsDuplicates()
        {
            var building = new Building();
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);

            var polygon = new Polygon(new List<Point>
            {
                new Point(0, 0, 0),
                new Point(5, 0, 0),
                new Point(5, 5, 0),
                new Point(0, 5, 0)
            });
            building.Rooms.Add(new Room(_buildingId, polygon, bot, top));

            // Wall start point matches room polygon vertex (0,0,0)
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            building.Walls.Add(new Wall(_buildingId, wallLine, 0.2, bot, top));

            Assert.True(Point.HasDuplicates(building.GetPoints().ToList()));
        }
    }
}
