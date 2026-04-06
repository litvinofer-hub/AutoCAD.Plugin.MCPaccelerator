using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

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

        private readonly List<SubLevel> _subLevels;
        public IReadOnlyList<SubLevel> SubLevels => _subLevels.AsReadOnly();

        public Level(Guid buildingId, double elevation)
        {
            Id = Guid.NewGuid();
            BuildingId = buildingId;
            Elevation = elevation;
            _subLevels = new List<SubLevel>();
        }

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
