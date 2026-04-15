using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CLEAR_AXIAL_SYSTEM command.
    ///
    /// Erases every building-level axial system and every per-story canvas
    /// origin in the current session — no user prompts required.
    ///
    /// For each building with an <see cref="Domain.BuildingModel.AxialSystem"/>:
    /// 1. For every story's <see cref="FloorPlanWorkingArea"/>:
    ///    - Erases axial-layer entities tracked in the working area.
    ///    - Removes their ObjectIds from SelectedObjectIds.
    ///    - Redraws the bounding-box frame.
    /// 2. Clears <see cref="Domain.BuildingModel.Building.AxialSystem"/>.
    /// 3. Clears each story's <see cref="Domain.BuildingModel.Story.CanvasOrigin"/>
    ///    (the origin loses its meaning once the shared system is gone).
    ///
    /// Also performs a fallback erase of any axial-layer entities not tracked
    /// in a working area (e.g. orphaned entities).
    /// </summary>
    public class ClearAxialSystemWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            int totalErased = 0;
            int buildingsCleared = 0;

            foreach (var (workingAreas, building) in BuildingSession.Entries)
            {
                if (building.AxialSystem == null) continue;

                foreach (var story in building.Stories)
                {
                    var area = workingAreas.FindByStory(story.Id);
                    if (area != null)
                    {
                        var axialIds = FindAxialObjectIds(area);
                        totalErased += EraseEntities(axialIds);

                        foreach (var id in axialIds)
                            area.SelectedObjectIds.Remove(id);

                        if (area.SelectedObjectIds.Count > 0)
                            WorkingAreaFrameHelper.RedrawFrame(area);
                    }

                    story.ClearCanvasOrigin();
                }

                building.ClearAxialSystem();
                buildingsCleared++;
            }

            // Fallback: erase any orphaned entities on the axial layer
            int orphaned = EraseAllOnAxialLayer();
            totalErased += orphaned;

            if (totalErased == 0 && buildingsCleared == 0)
            {
                _editor.WriteMessage("\nNo axial system entities to remove.");
                return;
            }

            _editor.WriteMessage(
                $"\nCleared axial system from {buildingsCleared} building(s). " +
                $"Removed {totalErased} entity(ies) total.");
        }

        /// <summary>
        /// Finds ObjectIds in the working area that are on the axial system layer.
        /// </summary>
        private static List<ObjectId> FindAxialObjectIds(FloorPlanWorkingArea area)
        {
            var result = new List<ObjectId>();
            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                foreach (var id in area.SelectedObjectIds)
                {
                    if (id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.Layer == LayerAxes)
                        result.Add(id);
                }

                tx.Commit();
            }

            return result;
        }

        private static int EraseEntities(List<ObjectId> objectIds)
        {
            if (objectIds.Count == 0) return 0;

            var doc = AcadContext.Document;
            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in objectIds)
                {
                    if (id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (entity != null)
                    {
                        entity.Erase();
                        count++;
                    }
                }

                tx.Commit();
            }

            return count;
        }

        /// <summary>
        /// Fallback: erases every entity on the MCP_Axial_System layer
        /// that might not be tracked in any working area.
        /// </summary>
        private static int EraseAllOnAxialLayer()
        {
            var doc = AcadContext.Document;
            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!layerTable.Has(LayerAxes))
                    return 0;

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (var entityId in modelSpace)
                {
                    var entity = (Entity)tx.GetObject(entityId, OpenMode.ForRead);
                    if (entity.Layer == LayerAxes)
                    {
                        entity.UpgradeOpen();
                        entity.Erase();
                        count++;
                    }
                }

                tx.Commit();
            }

            return count;
        }
    }
}
