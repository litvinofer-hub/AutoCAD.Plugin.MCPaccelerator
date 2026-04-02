using System;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class StoryTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        [Fact]
        public void AddIntermediateLevel_BetweenBotAndTop_AddsToList()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 6.0);
            var story = new Story(_buildingId, bot, top);

            var mid = new Level(_buildingId, 3.0);
            story.AddIntermediateLevel(mid);

            Assert.Single(story.IntermediateLevels);
            Assert.Same(mid, story.IntermediateLevels[0]);
        }

        [Fact]
        public void AddIntermediateLevel_AtOrBelowBot_ReplacesBotLevel()
        {
            var bot = new Level(_buildingId, 1.0);
            var top = new Level(_buildingId, 6.0);
            var story = new Story(_buildingId, bot, top);

            var newBot = new Level(_buildingId, 0.5);
            story.AddIntermediateLevel(newBot);

            Assert.Same(newBot, story.BotLevel);
            Assert.Empty(story.IntermediateLevels);
        }

        [Fact]
        public void AddIntermediateLevel_AtOrAboveTop_ReplacesTopLevel()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 3.0);
            var story = new Story(_buildingId, bot, top);

            var newTop = new Level(_buildingId, 5.0);
            story.AddIntermediateLevel(newTop);

            Assert.Same(newTop, story.TopLevel);
            Assert.Empty(story.IntermediateLevels);
        }

        [Fact]
        public void AddIntermediateLevel_EqualToBot_ReplacesBotLevel()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 6.0);
            var story = new Story(_buildingId, bot, top);

            var newBot = new Level(_buildingId, 0);
            story.AddIntermediateLevel(newBot);

            Assert.Same(newBot, story.BotLevel);
        }

        [Fact]
        public void AddIntermediateLevel_WithinTolerance_ReplacesBotLevel()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 6.0);
            var story = new Story(_buildingId, bot, top);

            var nearBot = new Level(_buildingId, 1e-7);
            story.AddIntermediateLevel(nearBot);

            Assert.Same(nearBot, story.BotLevel);
        }

        [Fact]
        public void IntermediateLevels_ReturnsSortedByElevation()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 10.0);
            var story = new Story(_buildingId, bot, top);

            var mid3 = new Level(_buildingId, 7.0);
            var mid1 = new Level(_buildingId, 2.0);
            var mid2 = new Level(_buildingId, 5.0);

            story.AddIntermediateLevel(mid3);
            story.AddIntermediateLevel(mid1);
            story.AddIntermediateLevel(mid2);

            Assert.Equal(3, story.IntermediateLevels.Count);
            Assert.Same(mid1, story.IntermediateLevels[0]);
            Assert.Same(mid2, story.IntermediateLevels[1]);
            Assert.Same(mid3, story.IntermediateLevels[2]);
        }

        [Fact]
        public void AllLevels_ReturnsBotIntermediateTop_InOrder()
        {
            var bot = new Level(_buildingId, 0);
            var top = new Level(_buildingId, 10.0);
            var story = new Story(_buildingId, bot, top);

            var mid = new Level(_buildingId, 5.0);
            story.AddIntermediateLevel(mid);

            var all = story.AllLevels;

            Assert.Equal(3, all.Count);
            Assert.Same(bot, all[0]);
            Assert.Same(mid, all[1]);
            Assert.Same(top, all[2]);
        }
    }
}
