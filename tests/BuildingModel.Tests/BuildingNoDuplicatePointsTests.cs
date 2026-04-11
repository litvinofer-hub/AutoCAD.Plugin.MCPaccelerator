using System.Linq;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class BuildingNoDuplicatePointsTests
    {
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
            building.AddStory(0, 3.0);
            building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0), (0.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            var points = building.GetPoints().ToList();

            Assert.Equal(4, points.Count);
        }

        [Fact]
        public void GetPoints_WithWall_ReturnsLineEndpoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            building.AddWall(0, 0, 10, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var points = building.GetPoints().ToList();

            Assert.Equal(2, points.Count);
        }

        [Fact]
        public void GetPoints_WithWallAndOpening_ReturnsAllEndpoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var wall = building.AddWall(0, 0, 10, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);
            building.AddWindow(wall, 1, 0, 2, 0, z: 0, height: 2.0);

            var points = building.GetPoints().ToList();

            // Wall: 2 + Window: 2 = 4
            Assert.Equal(4, points.Count);
        }

        [Fact]
        public void GetPoints_WithRoomAndWall_ReturnsAllPoints()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 0, topElevation: 3.0);
            building.AddWall(10, 0, 20, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var points = building.GetPoints().ToList();

            // Polygon: 3 + Wall: 2 = 5, but (0,0,0) is shared so wall reuses it
            // Actually wall points are (10,0,0) and (20,0,0) - all unique
            Assert.Equal(5, points.Count);
        }

        // --- Integration: GetPoints + HasDuplicates ---

        [Fact]
        public void Building_PointsCreatedThroughAdd_SharedReferencesAreReused()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            building.AddRoom(
                new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0) },
                botElevation: 0, topElevation: 3.0);

            // Wall shares corner (0,0) with room - Building reuses the same Point instance
            building.AddWall(0, 0, 0, 10, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            // All points are shared instances, so there are no value-equal-but-different-reference points
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

        [Fact]
        public void Building_SharedPointsAcrossElements_AreSameInstance()
        {
            var building = new Building();
            building.AddStory(0, 3.0);
            var room = building.AddRoom(
                new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 5.0) },
                botElevation: 0, topElevation: 3.0);

            // Wall starts at the same corner as room polygon
            var wall = building.AddWall(0, 0, 10, 0, botElevation: 0, topElevation: 3.0, thickness: 0.2);

            var roomFirstPoint = room.BotPolygon.Points[0];
            var wallStartPoint = wall.BotLine.StartPoint;

            Assert.Same(roomFirstPoint, wallStartPoint);
        }
    }
}
