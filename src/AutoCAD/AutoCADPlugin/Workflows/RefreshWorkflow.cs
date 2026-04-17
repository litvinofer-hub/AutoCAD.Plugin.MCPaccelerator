using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_REFRESH command.
    ///
    /// <list type="number">
    /// <item>Drains <see cref="PendingCanvasCleanup"/>: erases the frames,
    /// labels and printed polylines left behind by buildings that were
    /// removed via OL_DELETE_BUILDING since the last refresh.</item>
    /// </list>
    ///
    /// Then, for every <see cref="FloorPlanWorkingArea"/> in the session:
    /// <list type="number">
    /// <item>Uses the current boundary frame to find all entities that are
    /// partly or fully inside (crossing selection).</item>
    /// <item>Resets <see cref="FloorPlanWorkingArea.SelectedObjectIds"/> with
    /// the entities found inside.</item>
    /// <item>Redraws the boundary frame to fit the new set of elements.</item>
    /// <item>Filters → classifies → converts to domain Building elements
    /// (removes old walls first).</item>
    /// <item>Re-maps domain element IDs to source AutoCAD ObjectIds.</item>
    /// <item>If a 3D view is active (entities on MCP_3D_* layers exist),
    /// runs Clear3D + Show3D to refresh it.</item>
    /// <item>Replays every OL_PRINT_BUILDING recorded in
    /// <see cref="PrintBuildingRegistry"/> so printed floor plans stay in sync
    /// with the re-ingested model.</item>
    /// </list>
    /// </summary>
    public class RefreshWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // --- 0. Drain canvas cleanup from prior OL_DELETE_BUILDING calls ---
            // Done BEFORE the empty-session early return so a refresh after the
            // very last building was deleted still wipes its leftover entities.
            DrainPendingCleanup();

            var entries = BuildingSession.Entries;
            if (entries.Count == 0)
            {
                _editor.WriteMessage("\nNo buildings in session. Nothing to refresh.");
                return;
            }

            int areasRefreshed = 0;

            foreach (var (workingAreas, building) in entries)
            {
                foreach (var area in workingAreas.Areas)
                {
                    var story = building.Stories.FirstOrDefault(s => s.Id == area.StoryId);
                    if (story == null) continue;

                    _editor.WriteMessage($"\nRefreshing '{area.BuildingName} - {area.StoryName}'...");

                    // --- 1. Find all entities inside the current boundary ---
                    var insideIds = FindEntitiesInsideBoundary(area);

                    // --- 2. Reset SelectedObjectIds ---
                    area.SelectedObjectIds = insideIds;

                    // --- 3. Redraw the boundary frame ---
                    if (insideIds.Count > 0)
                        WorkingAreaFrameHelper.RedrawFrame(area);

                    // --- 4. Re-ingest this story's floor plan ---
                    var result = StoryReingestion.Reingest(building, story, area);

                    _editor.WriteMessage(
                        $"\n  Rebuilt: {result.WallsCreated} wall(s), " +
                        $"{result.WindowsCreated} window(s), " +
                        $"{result.DoorsCreated} door(s)");

                    areasRefreshed++;
                }

                // --- 7. Refresh 3D view if active ---
                if (Is3DViewActive())
                {
                    _editor.WriteMessage("\n  Refreshing 3D view...");
                    new Clear3DViewWorkflow().Run();
                    new Show3DViewWorkflow().Run();
                }
            }

            // --- 8. Replay every remembered OL_PRINT_BUILDING ---
            PrintBuildingWorkflow.ReprintAll();

            _editor.WriteMessage($"\n\nRefreshed {areasRefreshed} working area(s).");
        }

        // -------------------------------------------------------------------
        // Pending canvas cleanup (staged by OL_DELETE_BUILDING)
        // -------------------------------------------------------------------

        /// <summary>
        /// Erases every still-valid entity in <see cref="PendingCanvasCleanup"/>
        /// and clears the queue. Called at the very start of a refresh so
        /// frames, labels and printed polylines from deleted buildings disappear
        /// before anything else happens.
        /// </summary>
        private void DrainPendingCleanup()
        {
            int pending = PendingCanvasCleanup.Count;
            if (pending == 0) return;

            var doc = AcadContext.Document;
            var db = doc.Database;

            int erased = 0;
            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in PendingCanvasCleanup.Ids)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (entity == null) continue;
                    entity.Erase();
                    erased++;
                }
                tx.Commit();
            }

            PendingCanvasCleanup.Clear();
            _editor.WriteMessage(
                $"\nCleaned up {erased} canvas entity(ies) from deleted building(s).");
        }

        // -------------------------------------------------------------------
        // Boundary scanning
        // -------------------------------------------------------------------

        /// <summary>
        /// Uses the working area's frame polyline as a crossing window to find
        /// all entities that are partly or fully inside the boundary.
        /// Excludes the frame and label themselves.
        /// </summary>
        private List<ObjectId> FindEntitiesInsideBoundary(FloorPlanWorkingArea area)
        {
            var (minX, minY, maxX, maxY) = WorkingAreaFrameHelper.GetFrameBounds(area.FrameId);

            var corner1 = new Point3d(minX, minY, 0);
            var corner2 = new Point3d(maxX, maxY, 0);

            var selectionResult = _editor.SelectCrossingWindow(corner1, corner2);
            if (selectionResult.Status != PromptStatus.OK)
                return new List<ObjectId>();

            var result = new List<ObjectId>();
            var excludeIds = new HashSet<ObjectId> { area.FrameId, area.LabelId };

            foreach (var objectId in selectionResult.Value.GetObjectIds())
            {
                // Exclude the frame and label of this working area
                if (excludeIds.Contains(objectId)) continue;

                // Exclude frame/label of OTHER working areas
                if (IsWorkingAreaFrameOrLabel(objectId)) continue;

                result.Add(objectId);
            }

            return result;
        }

        /// <summary>
        /// Checks if an ObjectId belongs to any working area's frame or label
        /// (we don't want to include those as floor plan elements).
        /// </summary>
        private static bool IsWorkingAreaFrameOrLabel(ObjectId id)
        {
            foreach (var (workingAreas, _) in BuildingSession.Entries)
            {
                foreach (var area in workingAreas.Areas)
                {
                    if (id == area.FrameId || id == area.LabelId)
                        return true;
                }
            }
            return false;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns true if any entities exist on the MCP_3D_* layers,
        /// meaning the 3D view is currently active.
        /// </summary>
        private static bool Is3DViewActive()
        {
            var layers3D = McpLayers.All3D;
            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tx.GetObject(database.LayerTableId, OpenMode.ForRead);

                // If none of the 3D layers even exist, no 3D view is active
                bool anyLayerExists = false;
                foreach (var layer in layers3D)
                {
                    if (layerTable.Has(layer)) { anyLayerExists = true; break; }
                }
                if (!anyLayerExists) { tx.Commit(); return false; }

                var blockTable = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (var entityId in modelSpace)
                {
                    var entity = (Entity)tx.GetObject(entityId, OpenMode.ForRead);
                    foreach (var layer in layers3D)
                    {
                        if (entity.Layer == layer)
                        {
                            tx.Commit();
                            return true;
                        }
                    }
                }

                tx.Commit();
            }

            return false;
        }
    }
}
