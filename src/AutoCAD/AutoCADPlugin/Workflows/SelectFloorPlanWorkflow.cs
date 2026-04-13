using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Selection;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_SELECT_FLOOR_PLAN command:
    ///
    /// 1. Pick building + story.
    /// 2. User selects any entities (no filtering — every entity type accepted).
    /// 3. Save ALL selected ObjectIds into a <see cref="FloorPlanWorkingArea"/>,
    ///    draw a bounding-box frame + label around every selected entity, and
    ///    register the working area in <see cref="BuildingSession"/>.
    /// 4. Filter the saved selection down to closed polylines only.
    /// 5. Classify closed polylines into walls / windows / doors by layer name.
    /// 6. Convert classified polylines to domain Building elements.
    /// 7. Map each new domain element ID back to the source AutoCAD ObjectIds.
    /// 8. Report results.
    /// </summary>
    public class SelectFloorPlanWorkflow
    {
        private const string LayerFloorFrame = "MCP_Floor_Frame";
        private const short FrameColorIndex = 3; // green

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // --- 1. Pick building + story ---
            var context = BuildingContextPrompt.PickBuildingAndStory("floor plan elements");
            if (context == null) return;

            var building = context.Value.building;
            var story = context.Value.story;

            // --- 2. Raw selection — any entity type, no filtering ---
            var raw = FloorPlanSelection.Select();
            if (raw == null) return;

            if (raw.ObjectIds.Count == 0)
            {
                _editor.WriteMessage("\nNo elements found in selection.");
                return;
            }

            _editor.WriteMessage($"\nSelected {raw.ObjectIds.Count} element(s).");

            // --- 3. Save to FloorPlanWorkingArea + draw bbox + register in session ---
            var (frameId, labelId) = DrawBoundingBoxFrame(raw.ObjectIds, building.Name, story.Name);

            var workingArea = new FloorPlanWorkingArea(
                building.Id, story.Id,
                building.Name, story.Name,
                raw.ObjectIds,
                frameId, labelId);

            BuildingSession.GetWorkingAreas(building).Add(workingArea);

            // --- 4. Filter to closed polylines ---
            var closedPolylines = FloorPlanSelection.FilterClosedPolylines(raw);

            if (closedPolylines.Count == 0)
            {
                _editor.WriteMessage("\nNo closed polylines found in selection.");
                return;
            }

            // --- 5. Classify into walls / windows / doors ---
            var classified = FloorPlanSelection.Classify(closedPolylines);

            _editor.WriteMessage(
                $"\nClassified: {classified.Walls.Count} wall(s), " +
                $"{classified.Windows.Count} window(s), " +
                $"{classified.Doors.Count} door(s)");

            if (classified.Total == 0)
            {
                _editor.WriteMessage("\nNo wall/window/door polylines found after classification.");
                return;
            }

            // --- 6. Convert to domain Building elements ---
            var converted = FloorPlanConverter.Convert(
                classified.Walls, classified.Windows, classified.Doors,
                building.Units.LengthEpsilon);

            var result = FloorPlanConverter.Apply(building, story, converted);

            // --- 7. Map domain element IDs to source AutoCAD ObjectIds ---
            MapDomainElementIds(building, story, raw, workingArea);

            // --- 8. Report ---
            ReportResult(building, story, result);
        }

        // -------------------------------------------------------------------
        // Bounding-box frame
        // -------------------------------------------------------------------

        /// <summary>
        /// Draws a 2D axis-aligned bounding-box rectangle around all selected
        /// entities (any type) and a label at the bottom-left corner, both on
        /// the <c>MCP_Floor_Frame</c> layer. Uses each entity's
        /// <see cref="Entity.GeometricExtents"/> for a type-agnostic bbox.
        /// Returns both ObjectIds.
        /// </summary>
        private (ObjectId frameId, ObjectId labelId) DrawBoundingBoxFrame(
            List<ObjectId> objectIds, string buildingName, string storyName)
        {
            var (minX, minY, maxX, maxY) = ComputeBoundingBox(objectIds);

            // Small margin so the frame doesn't touch the outermost element.
            double margin = (maxX - minX + maxY - minY) * 0.02;
            minX -= margin;
            minY -= margin;
            maxX += margin;
            maxY += margin;

            string label = $"{buildingName} - {storyName}";
            ObjectId frameId, labelId;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(tx, db, LayerFloorFrame, FrameColorIndex);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // --- rectangle ---
                var rect = new AcadPolyline();
                rect.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                rect.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                rect.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                rect.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                rect.Closed = true;
                rect.Layer = LayerFloorFrame;
                modelSpace.AppendEntity(rect);
                tx.AddNewlyCreatedDBObject(rect, true);
                frameId = rect.ObjectId;

                // --- label text at bottom-left ---
                double textHeight = (maxY - minY) * 0.03;
                if (textHeight < 1.0) textHeight = 1.0;

                var text = new DBText
                {
                    TextString = label,
                    Height = textHeight,
                    Layer = LayerFloorFrame,
                    Position = new Point3d(minX, minY - textHeight * 1.5, 0)
                };
                modelSpace.AppendEntity(text);
                tx.AddNewlyCreatedDBObject(text, true);
                labelId = text.ObjectId;

                tx.Commit();
            }

            return (frameId, labelId);
        }

        // -------------------------------------------------------------------
        // ID mapping
        // -------------------------------------------------------------------

        /// <summary>
        /// After domain elements are created, walks the building's walls for
        /// this story and maps each wall / opening ID back to the full set
        /// of source AutoCAD ObjectIds from the raw selection.
        /// </summary>
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

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Computes the 2D axis-aligned bounding box of any set of entities
        /// using <see cref="Entity.GeometricExtents"/>. Works for every
        /// entity type (polylines, lines, circles, blocks, text, etc.).
        /// </summary>
        private static (double minX, double minY, double maxX, double maxY) ComputeBoundingBox(
            List<ObjectId> objectIds)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in objectIds)
                {
                    var entity = tx.GetObject(objectId, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;

                    Extents3d extents;
                    try { extents = entity.GeometricExtents; }
                    catch { continue; } // entity has no geometry (e.g. empty block)

                    if (extents.MinPoint.X < minX) minX = extents.MinPoint.X;
                    if (extents.MaxPoint.X > maxX) maxX = extents.MaxPoint.X;
                    if (extents.MinPoint.Y < minY) minY = extents.MinPoint.Y;
                    if (extents.MaxPoint.Y > maxY) maxY = extents.MaxPoint.Y;
                }

                tx.Commit();
            }

            return (minX, minY, maxX, maxY);
        }

        private static void EnsureLayer(Transaction tx, Database db, string name, short colorIndex)
        {
            var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(name)) return;

            layerTable.UpgradeOpen();
            var record = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            layerTable.Add(record);
            tx.AddNewlyCreatedDBObject(record, true);
        }

        private void ReportResult(Building building, Story story, FloorPlanResult result)
        {
            _editor.WriteMessage($"\n\nAdded to '{story.Name}' in '{building.Name}':");
            _editor.WriteMessage($"\n  Walls:   {result.WallsCreated}");
            _editor.WriteMessage($"\n  Windows: {result.WindowsCreated}");
            _editor.WriteMessage($"\n  Doors:   {result.DoorsCreated}");
            if (result.OpeningsSkipped > 0)
                _editor.WriteMessage($"\n  Skipped: {result.OpeningsSkipped} opening(s) (validation failed)");
        }
    }
}
