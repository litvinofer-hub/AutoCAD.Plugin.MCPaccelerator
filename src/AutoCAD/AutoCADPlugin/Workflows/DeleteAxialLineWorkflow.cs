using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_DELETE_AXIAL_LINE command.
    ///
    /// Flow:
    /// 1. Prompt the user to pick an entity on the MCP_Axial_System layer.
    /// 2. Look up which AxialLine owns that entity via
    ///    <see cref="FloorPlanWorkingArea.DomainToAcadMap"/>.
    /// 3. Erase all AutoCAD entities for that axis line
    ///    (line + 2 bubbles × (circle + text) = 5 entities).
    /// 4. Re-label remaining axes on canvas so symbols stay sequential
    ///    (e.g. A,B,C,D → delete C → A,B,D becomes A,B,C).
    /// 5. Remove ObjectIds from <see cref="FloorPlanWorkingArea"/> and redraw frame.
    /// 6. Update domain: remove the <see cref="AxialLine"/> from its
    ///    <see cref="AxialSystemDirection"/> (which also re-labels domain symbols).
    ///    If the direction has no lines left, remove it.
    ///    If the system has no directions left, clear it from the story.
    /// </summary>
    public class DeleteAxialLineWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // 1. Prompt user to pick an entity on the axial layer
            var pickedId = PromptPickAxialEntity();
            if (pickedId == null) return;

            // 2. Find the owner context via DomainToAcadMap
            var match = FindOwner(pickedId.Value);
            if (match == null)
            {
                _editor.WriteMessage("\nSelected entity is not part of a tracked axial system.");
                return;
            }

            var (building, story, area, axialSystem, direction, axisIndex) = match.Value;
            var axialLine = direction.AxialLines[axisIndex];
            string deletedSymbol = axialLine.Symbol;

            // 3. Get all ObjectIds for this AxialLine from the map
            List<ObjectId> idsToErase;
            if (!area.DomainToAcadMap.TryGetValue(axialLine.Id, out idsToErase))
            {
                _editor.WriteMessage("\nCould not find AutoCAD entities for this axis line.");
                return;
            }

            // 4. Erase from canvas
            EraseEntities(idsToErase);

            // 5. Remove from working area
            foreach (var id in idsToErase)
                area.SelectedObjectIds.Remove(id);

            // 6. Re-label remaining axes on canvas for indices >= removed
            RelabelAxesOnCanvas(area, direction, axisIndex);

            // 7. Redraw frame
            if (area.SelectedObjectIds.Count > 0)
                WorkingAreaFrameHelper.RedrawFrame(area);

            // 8. Update domain model (also re-labels domain symbols)
            direction.RemoveAxialLine(axisIndex);

            if (direction.AxialLines.Count == 0)
            {
                axialSystem.RemoveDirection(direction);

                if (axialSystem.Directions.Count == 0)
                {
                    story.ClearAxialSystem();
                    _editor.WriteMessage(
                        $"\nDeleted axis '{deletedSymbol}' — axial system removed (was the last line).");
                }
                else
                {
                    _editor.WriteMessage(
                        $"\nDeleted axis '{deletedSymbol}' — direction removed (was the last line in this direction).");
                }
            }
            else
            {
                _editor.WriteMessage(
                    $"\nDeleted axis '{deletedSymbol}'. " +
                    $"Remaining {direction.AxialLines.Count} axis/axes re-labeled.");
            }
        }

        // -------------------------------------------------------------------
        // Prompts
        // -------------------------------------------------------------------

        private ObjectId? PromptPickAxialEntity()
        {
            var options = new PromptEntityOptions(
                "\nSelect an axial line or bubble to delete: ");
            options.SetRejectMessage("\nEntity is not on the axial system layer.");
            options.AllowNone = false;

            var result = _editor.GetEntity(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            // Verify the entity is on the axial layer
            var db = AcadContext.Document.Database;
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var entity = tx.GetObject(result.ObjectId, OpenMode.ForRead) as Entity;
                tx.Commit();

                if (entity == null || entity.Layer != LayerAxes)
                {
                    _editor.WriteMessage("\nSelected entity is not on the axial system layer.");
                    return null;
                }
            }

            return result.ObjectId;
        }

        // -------------------------------------------------------------------
        // Ownership lookup via DomainToAcadMap
        // -------------------------------------------------------------------

        /// <summary>
        /// Searches all session entries to find the building, story, working area,
        /// axial system, direction, and axis index that own the given entity,
        /// using the <see cref="FloorPlanWorkingArea.DomainToAcadMap"/>.
        /// </summary>
        private static (Building building, Story story, FloorPlanWorkingArea area,
                 AxialSystem axialSystem, AxialSystemDirection direction, int axisIndex)?
            FindOwner(ObjectId entityId)
        {
            foreach (var entry in BuildingSession.Entries)
            {
                var building = entry.Building;

                foreach (var area in entry.WorkingAreas.Areas)
                {
                    // Find which domain Guid this ObjectId belongs to
                    Guid? matchedGuid = null;
                    foreach (var kvp in area.DomainToAcadMap)
                    {
                        if (kvp.Value.Contains(entityId))
                        {
                            matchedGuid = kvp.Key;
                            break;
                        }
                    }

                    if (matchedGuid == null) continue;

                    // Find the story and its axial system
                    var story = building.Stories.FirstOrDefault(s => s.Id == area.StoryId);
                    if (story?.AxialSystem == null) continue;

                    var axialSystem = story.AxialSystem;

                    // Find which direction and index this AxialLine is
                    foreach (var direction in axialSystem.Directions)
                    {
                        for (int i = 0; i < direction.AxialLines.Count; i++)
                        {
                            if (direction.AxialLines[i].Id == matchedGuid.Value)
                                return (building, story, area, axialSystem, direction, i);
                        }
                    }
                }
            }

            return null;
        }

        // -------------------------------------------------------------------
        // Re-labeling on canvas
        // -------------------------------------------------------------------

        /// <summary>
        /// Updates the DBText labels on the canvas for all axes in the direction
        /// starting from <paramref name="fromIndex"/>, so they match their new
        /// position in the symbol sequence after one line is removed.
        /// </summary>
        private static void RelabelAxesOnCanvas(FloorPlanWorkingArea area,
            AxialSystemDirection direction, int fromIndex)
        {
            // Re-label axes at fromIndex+1, fromIndex+2, ... (they will shift down)
            // At this point the domain hasn't been updated yet, so AxialLines still
            // has the original count and the line at fromIndex is still present.
            if (fromIndex + 1 >= direction.AxialLines.Count) return;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                for (int i = fromIndex + 1; i < direction.AxialLines.Count; i++)
                {
                    // After removal, index i becomes i-1
                    string newSymbol = direction.GetSymbol(i - 1);
                    var lineId = direction.AxialLines[i].Id;

                    if (!area.DomainToAcadMap.TryGetValue(lineId, out var objectIds))
                        continue;

                    foreach (var id in objectIds)
                    {
                        if (id.IsErased) continue;
                        var text = tx.GetObject(id, OpenMode.ForRead) as DBText;
                        if (text == null) continue;

                        text.UpgradeOpen();
                        text.TextString = newSymbol;
                    }
                }

                tx.Commit();
            }
        }

        // -------------------------------------------------------------------
        // Erase
        // -------------------------------------------------------------------

        private static void EraseEntities(List<ObjectId> objectIds)
        {
            if (objectIds.Count == 0) return;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in objectIds)
                {
                    if (id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                    entity?.Erase();
                }

                tx.Commit();
            }
        }
    }
}
