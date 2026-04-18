using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Remembers every OL_PRINT_GRAPHS invocation so that OL_REFRESH can
    /// redraw the level plan graphs automatically after the underlying model
    /// has been re-ingested.
    ///
    /// For each building we record:
    /// <list type="bullet">
    /// <item>The user-picked center point for every level whose graph was printed.</item>
    /// <item>The AutoCAD ObjectIds of the entities drawn in the last pass
    /// (edge lines, node circles, coordinate labels), so the next redraw
    /// can erase the previous pass before drawing the new one.</item>
    /// </list>
    ///
    /// Session-scoped (cleared on OL_RESET_SESSION and on per-building delete).
    /// </summary>
    public static class PrintGraphsRegistry
    {
        public sealed class Entry
        {
            /// <summary>LevelId → the center point the user picked for that level's graph.</summary>
            public Dictionary<Guid, Point3d> LevelCenters { get; } = new();

            /// <summary>ObjectIds of the entities drawn in the most recent print/reprint.</summary>
            public List<ObjectId> DrawnEntityIds { get; } = new();
        }

        private static readonly Dictionary<Guid, Entry> _byBuildingId = new();

        public static Entry GetOrCreate(Building building)
        {
            if (!_byBuildingId.TryGetValue(building.Id, out var entry))
            {
                entry = new Entry();
                _byBuildingId[building.Id] = entry;
            }
            return entry;
        }

        public static Entry TryGet(Building building)
        {
            _byBuildingId.TryGetValue(building.Id, out var entry);
            return entry;
        }

        public static IReadOnlyDictionary<Guid, Entry> All => _byBuildingId;

        public static void Remove(Building building) => _byBuildingId.Remove(building.Id);

        public static void Remove(Guid buildingId) => _byBuildingId.Remove(buildingId);

        public static void Clear() => _byBuildingId.Clear();
    }
}
