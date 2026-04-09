using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Story(Guid buildingId, Level botLevel, Level topLevel, string name = "")
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid BuildingId { get; private set; } = buildingId;
        public string Name { get; private set; } = name;
        public Level BotLevel { get; private set; } = botLevel;
        public Level TopLevel { get; private set; } = topLevel;

        /// <summary>
        /// Intermediate levels between BotLevel and TopLevel, sorted by elevation.
        /// </summary>
        public List<Level> IntermediateLevels
        {
            get { return [.. _intermediateLevels.OrderBy(l => l.Elevation)]; }
        }
        private readonly List<Level> _intermediateLevels = new List<Level>();

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        /// <summary>
        /// Returns all levels in order: BotLevel, intermediate levels, TopLevel.
        /// </summary>
        public List<Level> AllLevels
        {
            get
            {
                var levels = new List<Level> { BotLevel };
                levels.AddRange(IntermediateLevels);
                levels.Add(TopLevel);
                return levels;
            }
        }

        /// <summary>
        /// Adds an intermediate level. If the level's elevation is at or below BotLevel,
        /// it replaces BotLevel. If at or above TopLevel, it replaces TopLevel.
        /// </summary>
        public void AddIntermediateLevel(Level level)
        {
            if (GeometrySettings.IsLessThanOrEqual(level.Elevation, BotLevel.Elevation))
            {
                BotLevel = level;
            }
            else if (GeometrySettings.IsGreaterThanOrEqual(level.Elevation, TopLevel.Elevation))
            {
                TopLevel = level;
            }
            else
            {
                _intermediateLevels.Add(level);
            }
        }
    }
}
