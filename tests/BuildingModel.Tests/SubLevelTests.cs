using System;
using MCPAccelerator.Domain.BuildingModel;
using Xunit;

namespace MCPAccelerator.Tests.BuildingModel
{
    public class SubLevelTests
    {
        [Fact]
        public void Equals_SameOffset_ReturnsTrue()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 0.5);

            Assert.True(sub1.Equals(sub2));
        }

        [Fact]
        public void Equals_WithinTolerance_ReturnsTrue()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 0.5 + 1e-7);

            Assert.True(sub1.Equals(sub2));
        }

        [Fact]
        public void Equals_BeyondTolerance_ReturnsFalse()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 0.5 + 1e-4);

            Assert.False(sub1.Equals(sub2));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var sub = new SubLevel(Guid.NewGuid(), 0.5);

            Assert.False(sub.Equals(null));
        }

        [Fact]
        public void GetHashCode_SameOffset_SameHashCode()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 0.5);

            Assert.Equal(sub1.GetHashCode(), sub2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_WithinTolerance_SameHashCode()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 0.5 + 1e-7);

            Assert.Equal(sub1.GetHashCode(), sub2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentOffset_DifferentHashCode()
        {
            var sub1 = new SubLevel(Guid.NewGuid(), 0.5);
            var sub2 = new SubLevel(Guid.NewGuid(), 1.5);

            Assert.NotEqual(sub1.GetHashCode(), sub2.GetHashCode());
        }
    }
}
