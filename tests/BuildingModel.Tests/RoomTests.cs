using System;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class RoomTests
    {
        // --- BotLevel derived from polygon Z ---

        [Fact]
        public void AddRoom_BotLevelDerivedFromPolygonZ()
        {
            var building = new Building();

            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 5.0, topElevation: 8.0);

            Assert.Equal(5.0, room.BotLevel.Elevation);
        }

        [Fact]
        public void AddRoom_BotLevelIsSameInstanceAsRegisteredLevel()
        {
            var building = new Building();
            var level = building.GetOrAddLevel(3.0);

            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 3.0, topElevation: 6.0);

            Assert.Same(level, room.BotLevel);
        }

        // --- Polygon Z validation ---

        [Fact]
        public void Constructor_PolygonZMixed_ThrowsArgumentException()
        {
            var building = new Building();
            building.GetOrAddLevel(0);
            building.GetOrAddLevel(3.0);

            // Manually create polygon with mixed Z values
            var polygon = new Polygon(new System.Collections.Generic.List<Point>
            {
                new Point(0, 0, 0),
                new Point(5, 0, 3.0),  // different Z
                new Point(5, 5, 0)
            });

            Assert.Throws<ArgumentException>(() =>
                new Room(building.Id, polygon, building.GetOrAddLevel(3.0), building.Levels));
        }

        [Fact]
        public void Constructor_PolygonZWithinTolerance_Succeeds()
        {
            var building = new Building();
            building.GetOrAddLevel(3.0);
            var top = building.GetOrAddLevel(6.0);

            var polygon = new Polygon(new System.Collections.Generic.List<Point>
            {
                new Point(0, 0, 3.0),
                new Point(5, 0, 3.0 + 1e-7),
                new Point(5, 5, 3.0 - 1e-7)
            });

            var room = new Room(building.Id, polygon, top, building.Levels);

            Assert.Equal(3.0, room.BotLevel.Elevation);
        }

        // --- No matching Level throws ---

        [Fact]
        public void Constructor_NoMatchingLevel_ThrowsArgumentException()
        {
            var building = new Building();
            var top = building.GetOrAddLevel(6.0);

            // Polygon Z = 3.0 but no Level at 3.0 exists
            var polygon = new Polygon(new System.Collections.Generic.List<Point>
            {
                new Point(0, 0, 3.0),
                new Point(5, 0, 3.0),
                new Point(5, 5, 3.0)
            });

            Assert.Throws<ArgumentException>(() =>
                new Room(building.Id, polygon, top, building.Levels));
        }

        // --- Height ---

        [Fact]
        public void Height_ReturnsTopMinusBot()
        {
            var building = new Building();

            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 2.0, topElevation: 5.0);

            Assert.Equal(3.0, room.Height);
        }
    }
}
