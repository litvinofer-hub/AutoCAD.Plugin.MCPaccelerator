using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Level(Guid buildingId, double elevation)
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid BuildingId { get; private set; } = buildingId;
        /// <summary>
        /// Global Z coordinate of the level.
        /// </summary>
        public double Elevation { get; private set; } = elevation;

        private readonly List<SubLevel> _subLevels = [];
        public IReadOnlyList<SubLevel> SubLevels => _subLevels.AsReadOnly();

        /// <summary>
        /// Orthogonal plan graph for this level. Its edges represent the top
        /// middle-line of walls/beams whose TopLevel equals this level — i.e.
        /// the graph describes what stands below this level.
        /// </summary>
        public LevelPlanGraph Graph { get; } = new LevelPlanGraph();

        /// <summary>
        /// Returns the existing SubLevel with matching offset, or creates and adds a new one.
        /// </summary>
        public SubLevel GetOrAddSubLevel(double offset)
        {
            var existing = _subLevels.FirstOrDefault(s => GeometrySettings.AreEqual(s.Offset, offset));
            if (existing != null)
                return existing;

            var newSubLevel = new SubLevel(this.Id, offset);
            _subLevels.Add(newSubLevel);
            return newSubLevel;
        }

        public bool RemoveSubLevel(SubLevel subLevel)
        {
            return _subLevels.Remove(subLevel);
        }

        public override bool Equals(object obj)
        {
            if (obj is Level other)
            {
                return GeometrySettings.AreEqual(Elevation, other.Elevation);
            }

            return false;
        }

        public override int GetHashCode()
        {
            double roundFactor = 1.0 / GeometrySettings.Tolerance;
            return Math.Round(Elevation * roundFactor).GetHashCode();
        }
    }
}
