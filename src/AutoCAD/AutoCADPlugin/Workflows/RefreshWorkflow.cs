using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Selection;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_REFRESH command.
    ///
    /// For every <see cref="FloorPlanWorkingArea"/> in the session:
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
    /// </list>
    /// </summary>
    public class RefreshWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
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

                    // --- 4. Remove old domain walls for this story ---
                    RemoveStoryWalls(building, story.Id);
                    area.ClearDomainMap();

                    // --- 5. Filter → classify → convert ---
                    var raw = new RawSelection();
                    foreach (var id in area.SelectedObjectIds)
                        raw.ObjectIds.Add(id);

                    var closedPolylines = FloorPlanSelection.FilterClosedPolylines(raw);
                    if (closedPolylines.Count == 0)
                    {
                        _editor.WriteMessage($"\n  No closed polylines found.");
                        areasRefreshed++;
                        continue;
                    }

                    var classified = FloorPlanSelection.Classify(closedPolylines);
                    if (classified.Total == 0)
                    {
                        _editor.WriteMessage($"\n  No wall/window/door polylines after classification.");
                        areasRefreshed++;
                        continue;
                    }

                    var converted = FloorPlanConverter.Convert(
                        classified.Walls, classified.Windows, classified.Doors,
                        building.Units.LengthEpsilon);

                    var result = FloorPlanConverter.Apply(building, story, converted);

                    // --- 6. Re-map domain IDs ---
                    MapDomainElementIds(building, story, raw, area);

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

            _editor.WriteMessage($"\n\nRefreshed {areasRefreshed} working area(s).");
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

        private static void RemoveStoryWalls(Building building, Guid storyId)
        {
            var wallsToRemove = building.Walls
                .Where(w => w.StoryId == storyId)
                .ToList();

            foreach (var wall in wallsToRemove)
                building.RemoveWall(wall);
        }

        private static void MapDomainElementIds(
            Building building, Story story,
            RawSelection raw, FloorPlanWorkingArea workingArea)
        {
            var storyWalls = building.Walls
                .Where(w => w.StoryId == story.Id)
                .ToList();

            foreach (var wall in storyWalls)
            {
                workingArea.MapDomainElement(wall.Id, raw.ObjectIds);

                foreach (var opening in wall.Openings)
                    workingArea.MapDomainElement(opening.Id, raw.ObjectIds);
            }
        }

        /// <summary>
        /// Returns true if any entities exist on the MCP_3D_* layers,
        /// meaning the 3D view is currently active.
        /// </summary>
        private static bool Is3DViewActive()
        {
            var layers3D = new[] { "MCP_3D_Walls", "MCP_3D_Windows", "MCP_3D_Doors" };
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
