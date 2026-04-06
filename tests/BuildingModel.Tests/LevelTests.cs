using System;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class LevelTests
    {
        private readonly Guid _buildingId = Guid.NewGuid();

        // --- Equals / GetHashCode ---

        [Fact]
        public void Equals_SameElevation_ReturnsTrue()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 3.0);

            Assert.True(level1.Equals(level2));
        }

        [Fact]
        public void Equals_WithinTolerance_ReturnsTrue()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 3.0 + 1e-7);

            Assert.True(level1.Equals(level2));
        }

        [Fact]
        public void Equals_BeyondTolerance_ReturnsFalse()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 3.0 + 1e-4);

            Assert.False(level1.Equals(level2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var level = new Level(_buildingId, 3.0);

            Assert.False(level.Equals(null));
        }

        [Fact]
        public void GetHashCode_SameElevation_SameHashCode()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 3.0);

            Assert.Equal(level1.GetHashCode(), level2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_WithinTolerance_SameHashCode()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 3.0 + 1e-7);

            Assert.Equal(level1.GetHashCode(), level2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentElevation_DifferentHashCode()
        {
            var level1 = new Level(_buildingId, 3.0);
            var level2 = new Level(_buildingId, 6.0);

            Assert.NotEqual(level1.GetHashCode(), level2.GetHashCode());
        }

        // --- GetOrAddSubLevel ---

        [Fact]
        public void GetOrAddSubLevel_NewOffset_CreatesSubLevel()
        {
            var level = new Level(_buildingId, 3.0);

            var subLevel = level.GetOrAddSubLevel(0.5);

            Assert.Equal(0.5, subLevel.Offset);
            Assert.Single(level.SubLevels);
        }

        [Fact]
        public void GetOrAddSubLevel_SameOffset_ReturnsSameInstance()
        {
            var level = new Level(_buildingId, 3.0);

            var first = level.GetOrAddSubLevel(0.5);
            var second = level.GetOrAddSubLevel(0.5);

            Assert.Same(first, second);
            Assert.Single(level.SubLevels);
        }

        [Fact]
        public void GetOrAddSubLevel_WithinTolerance_ReturnsSameInstance()
        {
            var level = new Level(_buildingId, 3.0);

            var first = level.GetOrAddSubLevel(0.5);
            var second = level.GetOrAddSubLevel(0.5 + 1e-7);

            Assert.Same(first, second);
            Assert.Single(level.SubLevels);
        }

        [Fact]
        public void GetOrAddSubLevel_DifferentOffset_ReturnsDifferentInstance()
        {
            var level = new Level(_buildingId, 3.0);

            var first = level.GetOrAddSubLevel(0.5);
            var second = level.GetOrAddSubLevel(1.0);

            Assert.NotSame(first, second);
            Assert.Equal(2, level.SubLevels.Count);
        }

        // --- RemoveSubLevel ---

        [Fact]
        public void RemoveSubLevel_ExistingSubLevel_RemovesAndReturnsTrue()
        {
            var level = new Level(_buildingId, 3.0);
            var subLevel = level.GetOrAddSubLevel(0.5);

            var removed = level.RemoveSubLevel(subLevel);

            Assert.True(removed);
            Assert.Empty(level.SubLevels);
        }

        [Fact]
        public void RemoveSubLevel_NonExistingSubLevel_ReturnsFalse()
        {
            var level = new Level(_buildingId, 3.0);
            var otherSubLevel = new SubLevel(Guid.NewGuid(), 0.5);

            var removed = level.RemoveSubLevel(otherSubLevel);

            Assert.False(removed);
        }
    }
}
