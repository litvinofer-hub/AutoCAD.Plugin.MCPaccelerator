using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Holds all (FloorPlanWorkingAreas, Building) pairs for the current AutoCAD
    /// session. Each building has exactly one <see cref="FloorPlanWorkingAreas"/>
    /// container that collects the working areas for every story.
    /// Lives as long as the DLL is loaded.
    /// </summary>
    public static class BuildingSession
    {
        private static readonly List<(FloorPlanWorkingAreas WorkingAreas, Building Building)> _entries = [];
        private static int _counter = 0;

        /// <summary>All entries as (WorkingAreas, Building) tuples.</summary>
        public static IReadOnlyList<(FloorPlanWorkingAreas WorkingAreas, Building Building)> Entries
            => _entries.AsReadOnly();

        /// <summary>Convenience view: all buildings (preserves existing call-sites).</summary>
        public static IReadOnlyList<Building> Buildings
            => _entries.Select(e => e.Building).ToList().AsReadOnly();

        /// <summary>
        /// Creates a new Building and its companion FloorPlanWorkingAreas container,
        /// adds both to the session, and returns the Building.
        /// </summary>
        public static Building Add(string name = null, UnitSystem units = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _counter++;
                name = $"Building{_counter}";
            }

            var building = new Building(name, units);
            var workingAreas = new FloorPlanWorkingAreas();
            _entries.Add((workingAreas, building));
            return building;
        }

        public static Building GetByName(string name)
        {
            return _entries.FirstOrDefault(e => e.Building.Name == name).Building;
        }

        /// <summary>
        /// Returns the FloorPlanWorkingAreas container paired with the given building,
        /// or null if the building is not in the session.
        /// </summary>
        public static FloorPlanWorkingAreas GetWorkingAreas(Building building)
        {
            foreach (var entry in _entries)
            {
                if (ReferenceEquals(entry.Building, building))
                    return entry.WorkingAreas;
            }
            return null;
        }

        public static bool Remove(Building building)
        {
            int index = _entries.FindIndex(e => ReferenceEquals(e.Building, building));
            if (index < 0) return false;
            _entries.RemoveAt(index);
            return true;
        }

        public static void Clear()
        {
            _entries.Clear();
            _counter = 0;
        }
    }
}
