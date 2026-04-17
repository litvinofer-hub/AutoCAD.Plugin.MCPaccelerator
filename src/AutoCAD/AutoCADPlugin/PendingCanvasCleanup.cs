using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Queue of AutoCAD ObjectIds that belonged to buildings removed from
    /// <see cref="BuildingSession"/> (via OL_DELETE_BUILDING) but whose
    /// on-canvas entities — working-area frames and labels, printed floor
    /// plan polylines — have not been erased yet.
    ///
    /// OL_REFRESH drains this queue at the start of every run, erasing
    /// every still-valid entity, so the canvas lines up with the session
    /// after the next refresh.
    /// </summary>
    public static class PendingCanvasCleanup
    {
        private static readonly List<ObjectId> _ids = new();

        public static IReadOnlyList<ObjectId> Ids => _ids;

        public static int Count => _ids.Count;

        public static void Add(ObjectId id)
        {
            if (id.IsValid) _ids.Add(id);
        }

        public static void AddRange(IEnumerable<ObjectId> ids)
        {
            foreach (var id in ids) Add(id);
        }

        public static void Clear() => _ids.Clear();
    }
}
