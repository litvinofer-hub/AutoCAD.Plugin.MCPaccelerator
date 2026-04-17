using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Remembers every OL_PRINT_BUILDING invocation so that OL_REFRESH can
    /// redraw the printed floor plan automatically after the underlying model
    /// has been re-ingested.
    ///
    /// For each building we record:
    /// <list type="bullet">
    /// <item>The user-picked center point for every story that was printed.</item>
    /// <item>The AutoCAD ObjectIds of the entities drawn in the last pass, so
    /// the next redraw can erase the previous pass before drawing the new one.</item>
    /// </list>
    ///
    /// The registry is session-scoped (cleared on OL_RESET_SESSION and on
    /// per-building delete). If a building was never printed, it has no entry.
    /// </summary>
    public static class PrintBuildingRegistry
    {
        public sealed class Entry
        {
            /// <summary>StoryId → the center point the user picked for that story.</summary>
            public Dictionary<Guid, Point3d> StoryCenters { get; } = new();

            /// <summary>ObjectIds of the entities drawn in the most recent print/reprint.</summary>
            public List<ObjectId> DrawnEntityIds { get; } = new();
        }

        private static readonly Dictionary<Guid, Entry> _byBuildingId = new();

        /// <summary>
        /// Returns the entry for <paramref name="building"/>, creating an empty
        /// one if none exists yet.
        /// </summary>
        public static Entry GetOrCreate(Building building)
        {
            if (!_byBuildingId.TryGetValue(building.Id, out var entry))
            {
                entry = new Entry();
                _byBuildingId[building.Id] = entry;
            }
            return entry;
        }

        /// <summary>Returns the entry for <paramref name="building"/>, or null if absent.</summary>
        public static Entry TryGet(Building building)
        {
            _byBuildingId.TryGetValue(building.Id, out var entry);
            return entry;
        }

        /// <summary>All recorded entries, keyed by building Id.</summary>
        public static IReadOnlyDictionary<Guid, Entry> All => _byBuildingId;

        public static void Remove(Building building) => _byBuildingId.Remove(building.Id);

        public static void Remove(Guid buildingId) => _byBuildingId.Remove(buildingId);

        public static void Clear() => _byBuildingId.Clear();
    }
}
