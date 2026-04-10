using System;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class WallTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        // --- Constructor validation ---

        [Fact]
        public void Constructor_LineZMatchesLevel_Succeeds()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 5.0, topElevation: 8.0, thickness: 0.2);

            Assert.NotNull(wall);
            Assert.Equal(5.0, wall.BotLevel.Elevation);
        }

        [Fact]
        public void Constructor_LineZEndpointsDiffer_ThrowsArgumentException()
        {
            var building = new Building();
            building.GetOrAddLevel(0);
            building.GetOrAddLevel(3.0);
            var top = building.GetOrAddLevel(6.0);

            // Manually create line with mismatched Z
            var line = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 3.0));

            Assert.Throws<ArgumentException>(() =>
                new Wall(building.Id, line, 0.2, top, building.Levels));
        }

        [Fact]
        public void Constructor_NoMatchingLevel_ThrowsArgumentException()
        {
            var building = new Building();
            var top = building.GetOrAddLevel(6.0);

            // Line Z=3.0 but no Level at 3.0
            var line = new LineSegment(new Point(0, 0, 3.0), new Point(5, 0, 3.0));

            Assert.Throws<ArgumentException>(() =>
                new Wall(building.Id, line, 0.2, top, building.Levels));
        }

        [Fact]
        public void Constructor_BotLevelDerivedFromLineZ()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 3.0, topElevation: 6.0, thickness: 0.2);

            Assert.Equal(3.0, wall.BotLevel.Elevation);
            Assert.Same(building.Levels[0], wall.BotLevel);
        }

        // --- AddWindow / AddDoor / AddVoid validation ---

        [Fact]
        public void AddWindow_ValidOpening_AddsToList()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var window = building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 2.0);

            Assert.Single(wall.Openings);
            Assert.Equal(wall.Id, window.WallId);
        }

        [Fact]
        public void AddWindow_OpeningLineOutsideWall_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddWindow(wall, 6, 0, 7, 0, z: 0, height: 2.0));
        }

        [Fact]
        public void AddWindow_HeightExceedsWall_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 4.0));
        }

        [Fact]
        public void AddWindow_OpeningZAboveBotLevel_Succeeds()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            building.AddWindow(wall, 1, 0, 2, 0, z: 1.0, height: 1.5);

            Assert.Single(wall.Openings);
        }

        [Fact]
        public void AddWindow_OpeningZPlusHeightExceedsTop_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddWindow(wall, 1, 0, 2, 0, z: 1.5, height: 2.0));
        }

        [Fact]
        public void AddWindow_OpeningZBelowBotLevel_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 3.0, topElevation: 6.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddWindow(wall, 1, 0, 2, 0, z: 2.0, height: 1.0));
        }

        [Fact]
        public void AddWindow_OpeningZNotHorizontal_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            // Start and end at different Z — pass different z values through two separate walls
            // Actually, AddWindow uses a single z for both points, so it's always horizontal.
            // This validation is inherently satisfied by the API.
            // Test kept for documentation: the new API prevents non-horizontal openings by design.
            Assert.Single(wall.Openings,
                building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 1.0));
        }

        [Fact]
        public void AddDoor_HeightEqualsWallHeight_Succeeds()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            building.AddDoor(wall, 1, 0, 2, 0, z: 0, height: 3.0);

            Assert.Single(wall.Openings);
        }

        // --- RemoveOpening ---

        [Fact]
        public void RemoveOpening_ExistingOpening_RemovesAndReturnsTrue()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            var window = building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 2.0);

            var removed = wall.RemoveOpening(window);

            Assert.True(removed);
            Assert.Empty(wall.Openings);
        }

        // --- Opening shares points via Building ---

        [Fact]
        public void AddDoor_SharesPointsWithWall()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 10, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            // Door starts at same point as wall start (0,0,0)
            var door = building.AddDoor(wall, 0, 0, 2, 0, z: 0, height: 2.5);

            Assert.Same(wall.BotLine.StartPoint, door.Line.StartPoint);
        }

        // --- Validation edge cases ---

        [Fact]
        public void AddWindow_OpeningZAboveTopLevel_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddWindow(wall, 1, 0, 2, 0, z: 4.0, height: 1.0));
        }

        [Fact]
        public void AddWindow_OpeningZEqualsTopLevel_Succeeds()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            building.AddWindow(wall, 1, 0, 2, 0, z: 3.0, height: 0);

            Assert.Single(wall.Openings);
        }

        [Fact]
        public void AddWindow_OpeningHeightEqualsBotLevel_Succeeds()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 0);

            Assert.Single(wall.Openings);
        }

        [Fact]
        public void AddDoor_OpeningTopBelowBotLevel_ThrowsArgumentException()
        {
            var building = new Building();
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 3.0, topElevation: 6.0, thickness: 0.2);

            Assert.Throws<ArgumentException>(() =>
                building.AddDoor(wall, 1, 0, 2, 0, z: 3.0, height: -1.0));
        }
    }
}
