using System;

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
    }
}
