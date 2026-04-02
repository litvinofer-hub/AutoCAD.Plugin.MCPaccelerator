using System;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class WallTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        [Fact]
        public void Constructor_LineZMatchesBotLevel_Succeeds()
        {
            var bot = new Level(_buildingId, 5.0);
            var top = new Level(_buildingId, 8.0);
            var line = new LineSegment(new Point(0, 0, 5.0), new Point(5, 0, 5.0));

            var wall = new Wall(_buildingId, line, 0.2, bot, top);

            Assert.NotNull(wall);
        }

        [Fact]
        public void Constructor_LineZDoesNotMatchBotLevel_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var line = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));

            Assert.Throws<ArgumentException>(() => new Wall(_buildingId, line, 0.2, bot, top));
        }

        [Fact]
        public void Constructor_LineZWithinToleranceOfBotLevel_Succeeds()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var line = new LineSegment(new Point(0, 0, 3.0 + 1e-7), new Point(5, 0, 3.0 - 1e-7));

            var wall = new Wall(_buildingId, line, 0.2, bot, top);

            Assert.NotNull(wall);
        }

        [Fact]
        public void Constructor_StartPointZDoesNotMatch_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var line = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 3.0));

            Assert.Throws<ArgumentException>(() => new Wall(_buildingId, line, 0.2, bot, top));
        }

        [Fact]
        public void AddOpening_ValidOpening_AddsToList()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 0), new Point(2, 0, 0));
            var window = new Window(Guid.Empty, 2.0, openingLine);

            wall.AddOpening(window);

            Assert.Single(wall.Openings);
            Assert.Equal(wall.Id, window.WallId);
        }

        [Fact]
        public void AddOpening_OpeningLineOutsideWall_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(6, 0, 0), new Point(7, 0, 0));
            var window = new Window(Guid.Empty, 2.0, openingLine);

            Assert.Throws<ArgumentException>(() => wall.AddOpening(window));
        }

        [Fact]
        public void AddOpening_HeightExceedsWall_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 0), new Point(2, 0, 0));
            var window = new Window(Guid.Empty, 4.0, openingLine);

            Assert.Throws<ArgumentException>(() => wall.AddOpening(window));
        }

        [Fact]
        public void AddOpening_OpeningZAboveBotLevel_Succeeds()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 1.0), new Point(2, 0, 1.0));
            var window = new Window(Guid.Empty, 1.5, openingLine);

            wall.AddOpening(window);

            Assert.Single(wall.Openings);
        }

        [Fact]
        public void AddOpening_OpeningZPlusHeightExceedsTop_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 1.5), new Point(2, 0, 1.5));
            var window = new Window(Guid.Empty, 2.0, openingLine);

            Assert.Throws<ArgumentException>(() => wall.AddOpening(window));
        }

        [Fact]
        public void AddOpening_OpeningZBelowBotLevel_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 3.0);
            var top = new Level(_buildingId, 6.0);
            var wallLine = new LineSegment(new Point(0, 0, 3.0), new Point(5, 0, 3.0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 2.0), new Point(2, 0, 2.0));
            var window = new Window(Guid.Empty, 1.0, openingLine);

            Assert.Throws<ArgumentException>(() => wall.AddOpening(window));
        }

        [Fact]
        public void AddOpening_OpeningZNotHorizontal_ThrowsArgumentException()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 0.5), new Point(2, 0, 1.5));
            var window = new Window(Guid.Empty, 1.0, openingLine);

            Assert.Throws<ArgumentException>(() => wall.AddOpening(window));
        }

        [Fact]
        public void AddOpening_HeightEqualsWallHeight_Succeeds()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var wallLine = new LineSegment(new Point(0, 0, 0), new Point(5, 0, 0));
            var wall = new Wall(_buildingId, wallLine, 0.2, bot, top);

            var openingLine = new LineSegment(new Point(1, 0, 0), new Point(2, 0, 0));
            var door = new Door(Guid.Empty, 3.0, openingLine);

            wall.AddOpening(door);

            Assert.Single(wall.Openings);
        }
    }
}
