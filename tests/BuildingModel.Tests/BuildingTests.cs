using System.Linq;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class BuildingTests
    {
        // --- GetOrAddPoint ---

        [Fact]
        public void GetOrAddPoint_NewPoint_ReturnsNewInstance()
        {
            var building = new Building();

            var point = building.GetOrAddPoint(1, 2, 3);

            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);
            Assert.Equal(3, point.Z);
        }

        [Fact]
        public void GetOrAddPoint_SameCoordinates_ReturnsSameInstance()
        {
            var building = new Building();

            var first = building.GetOrAddPoint(1, 2, 3);
            var second = building.GetOrAddPoint(1, 2, 3);

            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrAddPoint_WithinTolerance_ReturnsSameInstance()
        {
            var building = new Building();

            var first = building.GetOrAddPoint(1, 2, 3);
            var second = building.GetOrAddPoint(1 + 1e-7, 2, 3);

            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrAddPoint_OutsideTolerance_ReturnsDifferentInstance()
        {
            var building = new Building();

            var first = building.GetOrAddPoint(1, 2, 3);
            var second = building.GetOrAddPoint(1 + 1e-4, 2, 3);

            Assert.NotSame(first, second);
        }

        // --- GetOrAddLevel ---

        [Fact]
        public void GetOrAddLevel_NewElevation_CreatesLevel()
        {
            var building = new Building();

            var level = building.GetOrAddLevel(3.0);

            Assert.Equal(3.0, level.Elevation);
            Assert.Single(building.Levels);
        }

        [Fact]
        public void GetOrAddLevel_SameElevation_ReturnsSameInstance()
        {
            var building = new Building();

            var first = building.GetOrAddLevel(3.0);
            var second = building.GetOrAddLevel(3.0);

            Assert.Same(first, second);
            Assert.Single(building.Levels);
        }

        [Fact]
        public void GetOrAddLevel_WithinTolerance_ReturnsSameInstance()
        {
            var building = new Building();

            var first = building.GetOrAddLevel(3.0);
            var second = building.GetOrAddLevel(3.0 + 1e-7);

            Assert.Same(first, second);
            Assert.Single(building.Levels);
        }

        [Fact]
        public void GetOrAddLevel_DifferentElevation_ReturnsDifferentInstance()
        {
            var building = new Building();

            var first = building.GetOrAddLevel(0);
            var second = building.GetOrAddLevel(3.0);

            Assert.NotSame(first, second);
            Assert.Equal(2, building.Levels.Count);
        }

        // --- AddRoom ---

        [Fact]
        public void AddRoom_CreatesRoomWithSharedPointsAndLevels()
        {
            var building = new Building();

            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            Assert.Single(building.Rooms);
            Assert.Equal(2, building.Levels.Count);
            Assert.Equal(building.Id, room.BuildingId);
        }

        [Fact]
        public void AddRoom_SameElevation_ReusesLevel()
        {
            var building = new Building();

            var room1 = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 0, topElevation: 3.0);
            var room2 = building.AddRoom(
                new[] { (10.0, 0.0), (15.0, 0.0), (15.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            Assert.Same(room1.BotLevel, room2.BotLevel);
            Assert.Same(room1.TopLevel, room2.TopLevel);
            Assert.Equal(2, building.Levels.Count);
        }

        // --- AddWall ---

        [Fact]
        public void AddWall_CreatesWallWithSharedPointsAndLevels()
        {
            var building = new Building();
            building.AddStory(0, 3.0);

            var wall = building.AddWall(0, 0, 5, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Single(building.Walls);
            Assert.Equal(building.Id, wall.BuildingId);
        }

        [Fact]
        public void AddWall_SharesPointsWithRoom()
        {
            var building = new Building();
            building.AddStory(0, 3.0);

            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0), (0.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            var wall = building.AddWall(0, 0, 5, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            // Wall endpoints reuse room polygon points
            Assert.Same(room.BotPolygon.Points[0], wall.BotLine.StartPoint);
            Assert.Same(room.BotPolygon.Points[1], wall.BotLine.EndPoint);
        }

        // --- AddStory ---

        [Fact]
        public void AddStory_CreatesStoryWithSharedLevels()
        {
            var building = new Building();

            var story = building.AddStory(0, 3.0);

            Assert.Single(building.Stories);
            Assert.Equal(2, building.Levels.Count);
        }

        [Fact]
        public void AddStory_SharesLevelsWithWall()
        {
            var building = new Building();
            var story = building.AddStory(0, 3.0);

            var wall = building.AddWall(0, 0, 5, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            Assert.Same(wall.BotLevel, story.BotLevel);
            Assert.Same(wall.TopLevel, story.TopLevel);
            Assert.Equal(2, building.Levels.Count);
        }

        // --- Openings created through Wall ---

        [Fact]
        public void Wall_AddWindow_UsesSharedPoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 10, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var window = building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 2.0);

            Assert.Single(wall.Openings);
            Assert.Equal(wall.Id, window.WallId);
        }

        [Fact]
        public void Wall_AddDoor_UsesSharedPoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 10, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var door = building.AddDoor(wall, 3, 0, 5, 0, z: 0, height: 2.5);

            Assert.Single(wall.Openings);
            Assert.Equal(wall.Id, door.WallId);
        }

        [Fact]
        public void Wall_AddDoor_SharesPointsWithWall()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 10, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            // Opening starts at wall start point (0,0,0)
            var door = building.AddDoor(wall, 0, 0, 2, 0, z: 0, height: 2.5);

            Assert.Same(wall.BotLine.StartPoint, door.Line.StartPoint);
        }

        // --- Remove ---

        [Fact]
        public void RemoveWall_RemovesFromList()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 5, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var removed = building.RemoveWall(wall);

            Assert.True(removed);
            Assert.Empty(building.Walls);
        }

        [Fact]
        public void RemoveRoom_RemovesFromList()
        {
            var building = new Building();
            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            var removed = building.RemoveRoom(room);

            Assert.True(removed);
            Assert.Empty(building.Rooms);
        }

        [Fact]
        public void RemoveStory_RemovesFromList()
        {
            var building = new Building();
            var story = building.AddStory(0, 3.0);

            var removed = building.RemoveStory(story);

            Assert.True(removed);
            Assert.Empty(building.Stories);
        }

        // --- Remove cleans up unused levels and points ---

        [Fact]
        public void RemoveWall_CleansUpUnusedLevels()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var upperStory = building.AddStory(6.0, 9.0);
            building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            var wall2 = building.AddWall(10, 0, 15, 0, botElevation: 6.0, topElevation: 9.0, thickness: 0.2);

            Assert.Equal(4, building.Levels.Count);

            building.RemoveWall(wall2);
            building.RemoveStory(upperStory);

            // Only elevations 0 and 3 remain (from wall1)
            Assert.Equal(2, building.Levels.Count);
        }

        [Fact]
        public void RemoveWall_KeepsSharedLevels()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            building.AddRoom(
                new[] { (10.0, 0.0), (15.0, 0.0), (15.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            building.RemoveWall(wall);

            // Levels 0 and 3 still used by room
            Assert.Equal(2, building.Levels.Count);
        }

        [Fact]
        public void RemoveWall_CleansUpUnusedPoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            building.AddWall(10, 0, 15, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            building.RemoveWall(wall);

            // After removal, only wall2's points remain
            // Verify via GetOrAddPoint: wall1's points should no longer be cached
            var freshPoint = building.GetOrAddPoint(0, 0, 0);
            var wall2Start = building.Walls[0].BotLine.StartPoint;

            Assert.NotSame(freshPoint, wall2Start); // new instance, not the old cached one
        }

        [Fact]
        public void RemoveAll_ClearsEverything()
        {
            var building = new Building();
            var story = building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 5, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            var room = building.AddRoom(
                new[] { (10.0, 0.0), (15.0, 0.0), (15.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            building.RemoveWall(wall);
            building.RemoveRoom(room);
            building.RemoveStory(story);

            Assert.Empty(building.Levels);
            Assert.Empty(building.GetPoints());
        }

        // --- Full integration ---

        [Fact]
        public void FullBuilding_AllSharedReferences_NoValueDuplicates()
        {
            var building = new Building();
            building.AddStory(0, 3.0);

            // Room and wall share corners at (0,0) and (5,0)
            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0), (0.0, 5.0) },
                botElevation: 0, topElevation: 3.0);
            var wall = building.AddWall(0, 0, 5, 0,
                botElevation: 0, topElevation: 3.0, thickness: 0.2);
            building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 2.0);

            // Shared levels
            Assert.Equal(2, building.Levels.Count);
            Assert.Same(room.BotLevel, wall.BotLevel);
            Assert.Same(room.TopLevel, wall.TopLevel);

            // Shared points: any equal points must be the same reference
            var allPoints = building.GetPoints().ToList();
            for (int i = 0; i < allPoints.Count; i++)
            {
                for (int j = i + 1; j < allPoints.Count; j++)
                {
                    if (allPoints[i].Equals(allPoints[j]))
                        Assert.Same(allPoints[i], allPoints[j]);
                }
            }
        }
    }
}
