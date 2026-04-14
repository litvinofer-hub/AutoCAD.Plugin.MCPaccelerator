using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CLEAR_AXIAL_SYSTEM command.
    ///
    /// Prompts the user to pick a building and story, then:
    /// 1. Erases axial system entities from the canvas (MCP_Axial_System layer,
    ///    scoped to ObjectIds tracked in the <see cref="FloorPlanWorkingArea"/>).
    /// 2. Removes those ObjectIds from the working area's SelectedObjectIds.
    /// 3. Resizes the working-area bounding-box frame.
    /// 4. Clears the <see cref="Domain.BuildingModel.AxialSystem"/> from the Story.
    /// </summary>
    public class ClearAxialSystemWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var context = BuildingContextPrompt.PickBuildingAndStory("clear axial system");
            if (context == null) return;
            var (building, story) = context.Value;

            var workingAreas = BuildingSession.GetWorkingAreas(building);
            var area = workingAreas?.FindByStory(story.Id);

            // 1. Erase axial entities from canvas and collect their ObjectIds
            int erased = 0;

            if (area != null)
            {
                // Only erase axial entities that are tracked in this working area
                var axialIds = FindAxialObjectIds(area);
                erased = EraseEntities(axialIds);

                // 2. Remove those ObjectIds from the working area
                foreach (var id in axialIds)
                    area.SelectedObjectIds.Remove(id);

                // 3. Resize frame (if there are still elements left)
                if (area.SelectedObjectIds.Count > 0)
                    WorkingAreaFrameHelper.RedrawFrame(area);
            }
            else
            {
                // Fallback: erase all entities on the axial layer
                erased = EraseAllOnAxialLayer();
            }

            // 4. Clear domain AxialSystems from story
            story.ClearAxialSystems();

            if (erased == 0)
            {
                _editor.WriteMessage("\nNo axial system entities to remove.");
                return;
            }

            _editor.WriteMessage($"\nRemoved {erased} axial system entity(ies) from '{story.Name}'.");
        }

        /// <summary>
        /// Finds ObjectIds in the working area that are on the axial system layer.
        /// </summary>
        private static System.Collections.Generic.List<ObjectId> FindAxialObjectIds(
            FloorPlanWorkingArea area)
        {
            var result = new System.Collections.Generic.List<ObjectId>();
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

        private static int EraseEntities(System.Collections.Generic.List<ObjectId> objectIds)
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
        /// Fallback when no working area exists: erases every entity on
        /// the MCP_Axial_System layer.
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
