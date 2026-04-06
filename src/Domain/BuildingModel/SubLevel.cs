using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class SubLevel
    {
        public Guid Id { get; set; }
        public Guid LevelId { get; set; }
        /// <summary>
        /// Offset relative to the parent Level's Elevation.
        /// </summary>
        public double Offset { get; set; }

        public SubLevel(Guid levelId, double offset)
        {
            Id = Guid.NewGuid();
            LevelId = levelId;
            Offset = offset;
        }

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
