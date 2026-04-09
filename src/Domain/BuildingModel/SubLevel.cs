using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class SubLevel(Guid levelId, double offset)
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid LevelId { get; private set; } = levelId;
        /// <summary>
        /// Offset relative to the parent Level's Elevation.
        /// </summary>
        public double Offset { get; private set; } = offset;

        public override bool Equals(object obj)
        {
            if (obj is SubLevel other)
            {
                return GeometrySettings.AreEqual(Offset, other.Offset);
            }

            return false;
        }

        public override int GetHashCode()
        {
            double roundFactor = 1.0 / GeometrySettings.Tolerance;
            return Math.Round(Offset * roundFactor).GetHashCode();
        }
    }
}
