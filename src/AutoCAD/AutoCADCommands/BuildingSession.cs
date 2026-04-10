using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands
{
    /// <summary>
    /// Holds all Building instances for the current AutoCAD session.
    /// Lives as long as the DLL is loaded.
    /// </summary>
    public static class BuildingSession
    {
        private static readonly List<Building> _buildings = [];
        private static int _counter = 0;

        public static IReadOnlyList<Building> Buildings => _buildings.AsReadOnly();

        public static Building Add(string name = null, UnitSystem units = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _counter++;
                name = $"Building{_counter}";
            }

            var building = new Building(name, units);
            _buildings.Add(building);
            return building;
        }

        public static Building GetByName(string name)
        {
            return _buildings.FirstOrDefault(b => b.Name == name);
        }

        public static bool Remove(Building building)
        {
            return _buildings.Remove(building);
        }

        public static void Clear()
        {
            _buildings.Clear();
            _counter = 0;
        }
    }
}
