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
    ///    If the system has no directions left, clear it from the building
    ///    and drop every story's CanvasOrigin.
    ///
    /// Also erases the axis line's drawn entities from every other
    /// <see cref="FloorPlanWorkingArea"/> in the building that was rendering
    /// the same shared axis (registered via its CanvasOrigin).
    /// </summary>
    public class DeleteAxialLineWorkflow
    {
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

            var (building, workingAreas, axialSystem, direction, axisIndex) = match.Value;
            var axialLine = direction.AxialLines[axisIndex];
            string deletedSymbol = axialLine.Symbol;

            // 3. Gather every (area, ids) pair for this axis line — the same
            //    domain AxialLine is rendered in every story's working area that
            //    has a CanvasOrigin.
            var perArea = new List<(FloorPlanWorkingArea area, List<ObjectId> ids)>();
            foreach (var a in workingAreas.Areas)
            {
                if (a.DomainToAcadMap.TryGetValue(axialLine.Id, out var ids) && ids.Count > 0)
                    perArea.Add((a, new List<ObjectId>(ids)));
            }

            if (perArea.Count == 0)
            {
                _editor.WriteMessage("\nCould not find AutoCAD entities for this axis line.");
                return;
            }

            // 4. Erase from canvas and clean up each working area
            foreach (var (a, ids) in perArea)
            {
                EraseEntities(ids);
                foreach (var id in ids)
                    a.SelectedObjectIds.Remove(id);

                RelabelAxesOnCanvas(a, direction, axisIndex);

                if (a.SelectedObjectIds.Count > 0)
                    WorkingAreaFrameHelper.RedrawFrame(a);
            }

            // 5. Update domain model (also re-labels domain symbols)
            direction.RemoveAxialLine(axisIndex);

            if (direction.AxialLines.Count == 0)
            {
                axialSystem.RemoveDirection(direction);

                if (axialSystem.Directions.Count == 0)
                {
                    building.ClearAxialSystem();
                    foreach (var s in building.Stories)
                        s.ClearCanvasOrigin();
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

                if (entity == null || entity.Layer != McpLayers.Axes)
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
        /// Searches all session entries to find the building, its working-area
        /// container, the shared axial system, the direction, and the axis
        /// index that own the given entity, using the
        /// <see cref="FloorPlanWorkingArea.DomainToAcadMap"/>.
        /// </summary>
        private static (Building building, FloorPlanWorkingAreas workingAreas,
                 AxialSystem axialSystem, AxialSystemDirection direction, int axisIndex)?
            FindOwner(ObjectId entityId)
        {
            foreach (var entry in BuildingSession.Entries)
            {
                var building = entry.Building;
                if (building.AxialSystem == null) continue;

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

                    // Find which direction and index this AxialLine is
                    foreach (var direction in building.AxialSystem.Directions)
                    {
                        for (int i = 0; i < direction.AxialLines.Count; i++)
                        {
                            if (direction.AxialLines[i].Id == matchedGuid.Value)
                                return (building, entry.WorkingAreas,
                                        building.AxialSystem, direction, i);
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
