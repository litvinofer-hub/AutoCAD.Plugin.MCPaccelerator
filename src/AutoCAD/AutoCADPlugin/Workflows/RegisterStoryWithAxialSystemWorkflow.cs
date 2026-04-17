using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_REGISTER_STORY_WITH_AXIAL_SYSTEM command.
    ///
    /// Copies the building-wide <see cref="AxialSystem"/> onto other stories'
    /// <see cref="FloorPlanWorkingArea"/>s. The system itself is shared — not
    /// duplicated. What this workflow does is set each target story's
    /// <see cref="Story.CanvasOrigin"/> so the same axial lines are rendered
    /// at the right 2D position inside each floor plan.
    ///
    /// Flow:
    /// 1. Verify the building has an <see cref="AxialSystem"/>.
    /// 2. User picks a reference point on a source story whose
    ///    <see cref="Story.HasCanvasOrigin"/> is true.
    /// 3. User picks the matching point on one or more target FPWAs
    ///    (the same physical location in the building — e.g. grid A-1 on both).
    ///    For each target:
    ///      target.CanvasOrigin = source.CanvasOrigin + (targetPick - sourcePick)
    ///    Target walls are re-ingested so their stored coordinates move
    ///    into building space, and the shared axial lines are drawn on the
    ///    target's FPWA.
    /// </summary>
    public class RegisterStoryWithAxialSystemWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var entries = BuildingSession.Entries;
            if (entries.Count == 0)
            {
                _editor.WriteMessage("\nNo buildings in session.");
                return;
            }

            // 1. Pick a building that has an AxialSystem
            Building building = null;
            FloorPlanWorkingAreas workingAreas = null;
            foreach (var (wa, b) in entries)
            {
                if (b.AxialSystem != null) { building = b; workingAreas = wa; break; }
            }
            if (building == null)
            {
                _editor.WriteMessage(
                    "\nNo building has an axial system yet. " +
                    "Run OL_CREATE_AXIAL_SYSTEM first.");
                return;
            }

            // 2. Pick the source point (must fall inside a FPWA whose story has CanvasOrigin)
            var sourcePickRes = _editor.GetPoint(new PromptPointOptions(
                "\nPick any recognizable point on the source floor plan " +
                "(e.g. a grid bubble or building corner). " +
                "You'll pick the same point on each target floor plan next: ")
            { AllowNone = false });
            if (sourcePickRes.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return;
            }

            var sourcePick = new Vec2(sourcePickRes.Value.X, sourcePickRes.Value.Y);
            var sourceMatch = FindAreaAndStoryContaining(
                building, workingAreas, sourcePick, requireCanvasOrigin: true);
            if (sourceMatch == null)
            {
                _editor.WriteMessage(
                    "\nSource point is not inside a floor plan that has an axial-system origin. " +
                    "Pick a point inside the floor plan where the axial system was created.");
                return;
            }
            var (sourceStory, _) = sourceMatch.Value;

            _editor.WriteMessage(
                $"\nSource story: '{sourceStory.Name}' " +
                $"(origin at canvas {sourceStory.CanvasOrigin.X:0.##}, {sourceStory.CanvasOrigin.Y:0.##}).");

            // 3. Loop: pick matching points on target FPWAs until the user cancels
            int registeredCount = 0;
            while (true)
            {
                var targetPickRes = _editor.GetPoint(new PromptPointOptions(
                    "\nPick matching point on target floor plan (Esc to finish): ")
                { AllowNone = true });
                if (targetPickRes.Status != PromptStatus.OK) break;

                var targetPick = new Vec2(targetPickRes.Value.X, targetPickRes.Value.Y);
                var targetMatch = FindAreaAndStoryContaining(
                    building, workingAreas, targetPick, requireCanvasOrigin: false);
                if (targetMatch == null)
                {
                    _editor.WriteMessage("\n  Point is not inside any floor plan working area. Skipped.");
                    continue;
                }

                var (targetStory, targetArea) = targetMatch.Value;
                if (ReferenceEquals(targetStory, sourceStory))
                {
                    _editor.WriteMessage("\n  That's the source floor plan — skipping.");
                    continue;
                }

                // 4. Compute and set the target story's CanvasOrigin
                var newOrigin = new Vec2(
                    sourceStory.CanvasOrigin.X + (targetPick.X - sourcePick.X),
                    sourceStory.CanvasOrigin.Y + (targetPick.Y - sourcePick.Y));
                targetStory.SetCanvasOrigin(newOrigin);

                // 5. Remove any stale axial entities from the target FPWA
                //    (shouldn't exist, but be defensive if the user re-registers).
                EraseExistingAxialEntities(targetArea);

                // 6. Re-ingest the target story's walls in building space
                StoryReingestion.Reingest(building, targetStory, targetArea);

                // 7. Draw the shared axial lines onto the target FPWA
                DrawSharedAxialSystem(building.AxialSystem, targetStory, targetArea);

                // 8. Update frame
                WorkingAreaFrameHelper.RedrawFrame(targetArea);

                _editor.WriteMessage(
                    $"\n  Registered '{targetStory.Name}' " +
                    $"(origin at canvas {newOrigin.X:0.##}, {newOrigin.Y:0.##}).");
                registeredCount++;
            }

            _editor.WriteMessage(
                $"\n\nRegistered {registeredCount} story(ies) with the axial system.");
        }

        // -------------------------------------------------------------------

        /// <summary>
        /// Finds the (story, area) pair whose frame contains the canvas point.
        /// Returns null if no FPWA contains the point.
        /// When <paramref name="requireCanvasOrigin"/> is true, only stories
        /// with <see cref="Story.HasCanvasOrigin"/> = true qualify.
        /// </summary>
        private static (Story story, FloorPlanWorkingArea area)? FindAreaAndStoryContaining(
            Building building, FloorPlanWorkingAreas workingAreas, Vec2 canvasPoint,
            bool requireCanvasOrigin)
        {
            foreach (var area in workingAreas.Areas)
            {
                var (minX, minY, maxX, maxY) = WorkingAreaFrameHelper.GetFrameBounds(area.FrameId);
                if (canvasPoint.X < minX || canvasPoint.X > maxX) continue;
                if (canvasPoint.Y < minY || canvasPoint.Y > maxY) continue;

                var story = building.Stories.FirstOrDefault(s => s.Id == area.StoryId);
                if (story == null) continue;
                if (requireCanvasOrigin && !story.HasCanvasOrigin) continue;

                return (story, area);
            }
            return null;
        }

        private static void EraseExistingAxialEntities(FloorPlanWorkingArea area)
        {
            var toErase = new List<ObjectId>();
            var doc = AcadContext.Document;
            var db = doc.Database;

            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in area.SelectedObjectIds)
                {
                    if (id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && entity.Layer == McpLayers.Axes)
                        toErase.Add(id);
                }
                tx.Commit();
            }

            if (toErase.Count == 0) return;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in toErase)
                {
                    if (id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                    entity?.Erase();
                }
                tx.Commit();
            }

            foreach (var id in toErase)
                area.SelectedObjectIds.Remove(id);
        }

        /// <summary>
        /// Draws every axial line of <paramref name="system"/> into
        /// <paramref name="area"/>, offset by <paramref name="story"/>'s
        /// CanvasOrigin. Mapping is registered so OL_DELETE_AXIAL_LINE can
        /// find these entities later.
        /// </summary>
        private static void DrawSharedAxialSystem(
            AxialSystem system, Story story, FloorPlanWorkingArea area)
        {
            var doc = AcadContext.Document;
            var db = doc.Database;
            double bubbleRadius = system.BubbleRadius;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                McpLayers.Ensure(tx, db, McpLayers.Axes, McpLayers.YellowColorIndex);
                var linetypeId = LoadCenterLinetype(tx, db);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var direction in system.Directions)
                {
                    foreach (var axialLine in direction.AxialLines)
                    {
                        var lineIds = new List<ObjectId>();

                        var (sx, sy) = story.BuildingToCanvas(
                            axialLine.Line.StartPoint.X, axialLine.Line.StartPoint.Y);
                        var (ex, ey) = story.BuildingToCanvas(
                            axialLine.Line.EndPoint.X, axialLine.Line.EndPoint.Y);

                        var startPt = new Point3d(sx, sy, 0);
                        var endPt = new Point3d(ex, ey, 0);

                        var bubbleStart = new Point3d(
                            startPt.X - direction.Direction.X * bubbleRadius,
                            startPt.Y - direction.Direction.Y * bubbleRadius, 0);
                        var bubbleEnd = new Point3d(
                            endPt.X + direction.Direction.X * bubbleRadius,
                            endPt.Y + direction.Direction.Y * bubbleRadius, 0);

                        var line = new Line(startPt, endPt) { Layer = McpLayers.Axes };
                        if (linetypeId != ObjectId.Null) line.LinetypeId = linetypeId;
                        modelSpace.AppendEntity(line);
                        tx.AddNewlyCreatedDBObject(line, true);
                        lineIds.Add(line.ObjectId);

                        lineIds.AddRange(DrawBubble(tx, modelSpace, bubbleStart, bubbleRadius, axialLine.Symbol));
                        lineIds.AddRange(DrawBubble(tx, modelSpace, bubbleEnd, bubbleRadius, axialLine.Symbol));

                        area.SelectedObjectIds.AddRange(lineIds);
                        area.MapDomainElement(axialLine.Id, lineIds);
                    }
                }

                tx.Commit();
            }
        }

        private static List<ObjectId> DrawBubble(Transaction tx, BlockTableRecord modelSpace,
            Point3d center, double radius, string symbol)
        {
            var ids = new List<ObjectId>();

            var circle = new Circle(center, Vector3d.ZAxis, radius) { Layer = McpLayers.Axes };
            modelSpace.AppendEntity(circle);
            tx.AddNewlyCreatedDBObject(circle, true);
            ids.Add(circle.ObjectId);

            var text = new DBText
            {
                TextString = symbol,
                Height = radius * 1.2,
                Layer = McpLayers.Axes,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = center
            };
            modelSpace.AppendEntity(text);
            tx.AddNewlyCreatedDBObject(text, true);
            ids.Add(text.ObjectId);

            return ids;
        }

        private static ObjectId LoadCenterLinetype(Transaction tx, Database db)
        {
            var linetypeTable = (LinetypeTable)tx.GetObject(
                db.LinetypeTableId, OpenMode.ForRead);

            if (linetypeTable.Has("CENTER"))
                return linetypeTable["CENTER"];

            try
            {
                db.LoadLineTypeFile("CENTER", "acad.lin");
                linetypeTable = (LinetypeTable)tx.GetObject(
                    db.LinetypeTableId, OpenMode.ForRead);
                if (linetypeTable.Has("CENTER"))
                    return linetypeTable["CENTER"];
            }
            catch { }

            return ObjectId.Null;
        }
    }
}
