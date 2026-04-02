using System;
using System.Collections.Generic;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Level
    {
        public Guid Id { get; set; }
        public Guid BuildingId { get; set; }
        /// <summary>
        /// Global Z coordinate of the level.
        /// </summary>
        public double Elevation { get; set; }
        public List<SubLevel> SubLevels { get; set; }

        public Level(Guid buildingId, double elevation)
        {
            Id = Guid.NewGuid();
            BuildingId = buildingId;
            Elevation = elevation;
            SubLevels = new List<SubLevel>();
        }
    }
}
