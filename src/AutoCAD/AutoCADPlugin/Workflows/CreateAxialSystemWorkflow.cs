using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CREATE_AXIAL_SYSTEM command.
    ///
    /// The axial system belongs to the <see cref="Building"/>, not a single
    /// <see cref="Story"/> — the same grid is shared across every story, since
    /// in real life the stories stack vertically and sit on the same grid. On
    /// the 2D canvas, each story is offset; each story's
    /// <see cref="Story.CanvasOrigin"/> records where building-space (0,0) sits
    /// on its own floor plan.
    ///
    /// For the <b>first direction</b> on a building:
    /// 1. The user picks the grid A-1 anchor point on the source story — this
    ///    defines the building origin and sets the source story's CanvasOrigin.
    /// 2. The source story's walls are re-ingested so their stored coordinates
    ///    move from canvas-space into building-space.
    /// 3. A new <see cref="AxialSystem"/> is created on the Building.
    ///
    /// For <b>subsequent directions</b> (same building, new direction):
    /// - No anchor pick — the building origin is already set.
    /// - The existing <see cref="AxialSystem"/> gains one more
    ///   <see cref="AxialSystemDirection"/>.
    ///
    /// Axis-line geometry is analyzed from walls in building space, stored in
    /// <see cref="AxialLine.Line"/> in building space, and drawn on the canvas
    /// at <c>story.BuildingToCanvas(...)</c>.
    /// </summary>
    public class CreateAxialSystemWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";
        private const short AxisColorIndex = 2; // yellow

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // 1. Pick building and source story
            var context = BuildingContextPrompt.PickBuildingAndStory("axial system");
            if (context == null) return;
            var (building, story) = context.Value;

            // 2. Pick direction
            var direction = PromptDirection();
            if (direction == null) return;
            Vec2 dir = Vec2Math.Normalize(direction.Value);

            // 3. Reject duplicate direction
            if (building.AxialSystem != null &&
                building.AxialSystem.FindDirection(dir) != null)
            {
                _editor.WriteMessage(
                    "\nAn axial direction parallel to this vector already exists in this building.");
                return;
            }

            // 4. Pick symbol type
            var symbolType = PromptSymbolType();
            if (symbolType == null) return;

            // 5. Look up working area (needed early so we know we can draw/refresh)
            var workingAreas = BuildingSession.GetWorkingAreas(building);
            var area = workingAreas?.FindByStory(story.Id);
            if (area == null)
            {
                _editor.WriteMessage(
                    "\nNo floor plan working area registered for this story. " +
                    "Run OL_CREATE_FLOOR_PLAN_AREA first.");
                return;
            }

            // 6. First direction: prompt for grid A-1 anchor, set CanvasOrigin,
            //    re-ingest so existing walls are in building space.
            bool isFirstDirection = building.AxialSystem == null;
            if (isFirstDirection)
            {
                var anchor = PromptGridA1Anchor();
                if (anchor == null) return;

                story.SetCanvasOrigin(anchor.Value);
                _editor.WriteMessage(
                    $"\nGrid A-1 anchor set at canvas ({anchor.Value.X:0.##}, {anchor.Value.Y:0.##}). " +
                    "Re-ingesting walls in building space...");

                StoryReingestion.Reingest(building, story, area);
            }

            // 7. Gather walls for this story (now in building space)
            var storyWalls = building.Walls.Where(w => w.StoryId == story.Id).ToList();
            if (storyWalls.Count == 0)
            {
                _editor.WriteMessage("\nNo walls found in this story.");
                return;
            }

            // 8. Filter walls parallel to chosen direction
            var parallelWalls = FindParallelWalls(storyWalls, dir);
            if (parallelWalls.Count == 0)
            {
                _editor.WriteMessage("\nNo walls found parallel to the chosen direction.");
                return;
            }

            // 9. Unique perpendicular positions in BUILDING space, sorted
            var perpDir = Vec2Math.Perpendicular(dir);
            var positions = ComputeUniquePositions(parallelWalls, perpDir, building.Units);

            // 10. Compute extent along direction for line length (building space)
            var (minAlong, maxAlong) = ComputeExtentAlongDirection(storyWalls, dir);
            double span = maxAlong - minAlong;
            double margin = span * 0.15;
            if (margin < building.Units.LengthEpsilon) margin = 1.0;

            double bubbleRadius = isFirstDirection
                ? ComputeBubbleRadius(building.Units)
                : building.AxialSystem.BubbleRadius;
            double lineStart = minAlong - margin - bubbleRadius * 2;
            double lineEnd = maxAlong + margin + bubbleRadius * 2;

            // 11. Build domain AxialLines (coordinates in BUILDING space)
            var domainSymbolType = symbolType.Value switch
            {
                SymbolType.Numbers   => AxisSymbolType.Numbers,
                SymbolType.LowerCase => AxisSymbolType.LowerCase,
                SymbolType.UpperCase => AxisSymbolType.UpperCase,
                _                    => AxisSymbolType.Numbers
            };

            var axialLines = new List<AxialLine>();
            for (int i = 0; i < positions.Count; i++)
            {
                double perpPos = positions[i];
                string symbol = GetSymbol(i, symbolType.Value);

                var startPt = new Point(
                    perpPos * perpDir.X + lineStart * dir.X,
                    perpPos * perpDir.Y + lineStart * dir.Y, 0);
                var endPt = new Point(
                    perpPos * perpDir.X + lineEnd * dir.X,
                    perpPos * perpDir.Y + lineEnd * dir.Y, 0);

                axialLines.Add(new AxialLine(symbol, new LineSegment(startPt, endPt)));
            }

            var axialDirection = new AxialSystemDirection(dir, domainSymbolType, axialLines);

            // 12. Ensure the building has an AxialSystem; add the new direction
            if (isFirstDirection)
                building.SetAxialSystem(new AxialSystem(building.Id, bubbleRadius));
            building.AxialSystem.AddDirection(axialDirection);

            // 13. Draw on canvas at story's BuildingToCanvas(...) + collect ids per line
            var idsPerLine = DrawAxes(axialLines, dir, bubbleRadius, story);

            // 14. Wire drawn entities into this story's working area
            foreach (var (axialLine, ids) in idsPerLine)
            {
                area.SelectedObjectIds.AddRange(ids);
                area.MapDomainElement(axialLine.Id, ids);
            }
            WorkingAreaFrameHelper.RedrawFrame(area);

            // 15. If other stories are already registered with the axial system,
            //     draw the new direction onto their FPWAs too.
            foreach (var otherStory in building.Stories)
            {
                if (ReferenceEquals(otherStory, story)) continue;
                if (!otherStory.HasCanvasOrigin) continue;

                var otherArea = workingAreas.FindByStory(otherStory.Id);
                if (otherArea == null) continue;

                var otherIds = DrawAxes(axialLines, dir, bubbleRadius, otherStory);
                foreach (var (axialLine, ids) in otherIds)
                {
                    otherArea.SelectedObjectIds.AddRange(ids);
                    otherArea.MapDomainElement(axialLine.Id, ids);
                }
                WorkingAreaFrameHelper.RedrawFrame(otherArea);
            }

            _editor.WriteMessage(
                $"\nCreated {axialLines.Count} axial axis/axes on layer '{LayerAxes}'.");
        }

        // -------------------------------------------------------------------
        // Prompts
        // -------------------------------------------------------------------

        private Vec2? PromptGridA1Anchor()
        {
            var options = new PromptPointOptions(
                "\nPick the grid A-1 intersection point (will become building origin 0,0): ")
            { AllowNone = false };

            var result = _editor.GetPoint(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return new Vec2(result.Value.X, result.Value.Y);
        }

        private Vec2? PromptDirection()
        {
            var options = new PromptKeywordOptions(
                "\nSelect axis direction [X/Y/Other]: ");
            options.Keywords.Add("X");
            options.Keywords.Add("Y");
            options.Keywords.Add("Other");
            options.AllowNone = false;

            var result = _editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            switch (result.StringResult)
            {
                case "X": return new Vec2(1, 0);
                case "Y": return new Vec2(0, 1);
                case "Other": return PromptCustomDirection();
                default: return null;
            }
        }

        private Vec2? PromptCustomDirection()
        {
            var xOpts = new PromptDoubleOptions("\nEnter direction X component: ")
            { AllowNone = false };
            var xResult = _editor.GetDouble(xOpts);
            if (xResult.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            var yOpts = new PromptDoubleOptions("\nEnter direction Y component: ")
            { AllowNone = false };
            var yResult = _editor.GetDouble(yOpts);
            if (yResult.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            var v = new Vec2(xResult.Value, yResult.Value);
            if (Vec2Math.Length(v) < GeometrySettings.Tolerance)
            {
                _editor.WriteMessage("\nDirection vector cannot be zero.");
                return null;
            }

            return v;
        }

        private enum SymbolType { Numbers, LowerCase, UpperCase }

        private SymbolType? PromptSymbolType()
        {
            var options = new PromptKeywordOptions(
                "\nSelect axis symbols [Numbers/Lowercase/Uppercase]: ");
            options.Keywords.Add("Numbers");
            options.Keywords.Add("Lowercase");
            options.Keywords.Add("Uppercase");
            options.AllowNone = false;

            var result = _editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            switch (result.StringResult)
            {
                case "Numbers":   return SymbolType.Numbers;
                case "Lowercase": return SymbolType.LowerCase;
                case "Uppercase": return SymbolType.UpperCase;
                default: return null;
            }
        }

        // -------------------------------------------------------------------
        // Wall analysis (BUILDING space)
        // -------------------------------------------------------------------

        private static List<Wall> FindParallelWalls(List<Wall> walls, Vec2 direction)
        {
            var result = new List<Wall>();
            foreach (var wall in walls)
            {
                var wallDir = Vec2Math.Normalize(new Vec2(
                    wall.BotLine.EndPoint.X - wall.BotLine.StartPoint.X,
                    wall.BotLine.EndPoint.Y - wall.BotLine.StartPoint.Y));

                double cross = wallDir.X * direction.Y - wallDir.Y * direction.X;
                if (GeometrySettings.AreEqual(cross, 0))
                    result.Add(wall);
            }
            return result;
        }

        private static List<double> ComputeUniquePositions(
            List<Wall> walls, Vec2 perpDir, UnitSystem units)
        {
            var positions = new List<double>();
            foreach (var wall in walls)
            {
                var mid = Vec2Math.Mid(
                    new Vec2(wall.BotLine.StartPoint.X, wall.BotLine.StartPoint.Y),
                    new Vec2(wall.BotLine.EndPoint.X, wall.BotLine.EndPoint.Y));
                double pos = Vec2Math.Dot(mid, perpDir);

                bool exists = positions.Any(
                    p => Math.Abs(p - pos) < units.LengthEpsilon);
                if (!exists)
                    positions.Add(pos);
            }

            positions.Sort();
            return positions;
        }

        private static (double min, double max) ComputeExtentAlongDirection(
            List<Wall> walls, Vec2 dir)
        {
            double min = double.MaxValue;
            double max = double.MinValue;

            foreach (var wall in walls)
            {
                double p1 = Vec2Math.Dot(
                    new Vec2(wall.BotLine.StartPoint.X, wall.BotLine.StartPoint.Y), dir);
                double p2 = Vec2Math.Dot(
                    new Vec2(wall.BotLine.EndPoint.X, wall.BotLine.EndPoint.Y), dir);

                min = Math.Min(min, Math.Min(p1, p2));
                max = Math.Max(max, Math.Max(p1, p2));
            }

            return (min, max);
        }

        // -------------------------------------------------------------------
        // Drawing — converts each AxialLine's building-space endpoints to
        // canvas space using the given story's CanvasOrigin.
        // -------------------------------------------------------------------

        /// <summary>
        /// Draws axis lines with bubbles for <paramref name="story"/>.
        /// Input <see cref="AxialLine"/>s are in <b>building</b> space;
        /// this method converts to canvas space via <c>story.BuildingToCanvas</c>.
        /// </summary>
        private List<(AxialLine axialLine, List<ObjectId> ids)> DrawAxes(
            List<AxialLine> axialLines, Vec2 dir, double bubbleRadius, Story story)
        {
            var result = new List<(AxialLine, List<ObjectId>)>();
            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(tx, db, LayerAxes, AxisColorIndex);
                var linetypeId = LoadCenterLinetype(tx, db);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var axialLine in axialLines)
                {
                    var lineIds = new List<ObjectId>();

                    var (sx, sy) = story.BuildingToCanvas(
                        axialLine.Line.StartPoint.X, axialLine.Line.StartPoint.Y);
                    var (ex, ey) = story.BuildingToCanvas(
                        axialLine.Line.EndPoint.X, axialLine.Line.EndPoint.Y);

                    var startPt = new Point3d(sx, sy, 0);
                    var endPt = new Point3d(ex, ey, 0);

                    var bubbleStartPt = new Point3d(
                        startPt.X - dir.X * bubbleRadius,
                        startPt.Y - dir.Y * bubbleRadius, 0);
                    var bubbleEndPt = new Point3d(
                        endPt.X + dir.X * bubbleRadius,
                        endPt.Y + dir.Y * bubbleRadius, 0);

                    // Axis line
                    var line = new Line(startPt, endPt);
                    line.Layer = LayerAxes;
                    if (linetypeId != ObjectId.Null)
                        line.LinetypeId = linetypeId;
                    modelSpace.AppendEntity(line);
                    tx.AddNewlyCreatedDBObject(line, true);
                    lineIds.Add(line.ObjectId);

                    // Bubbles at both ends
                    lineIds.AddRange(
                        DrawBubble(tx, modelSpace, bubbleStartPt, bubbleRadius, axialLine.Symbol));
                    lineIds.AddRange(
                        DrawBubble(tx, modelSpace, bubbleEndPt, bubbleRadius, axialLine.Symbol));

                    result.Add((axialLine, lineIds));
                }

                tx.Commit();
            }

            return result;
        }

        /// <summary>
        /// Draws a circle with a centered label and returns the ObjectIds.
        /// </summary>
        private static List<ObjectId> DrawBubble(Transaction tx, BlockTableRecord modelSpace,
            Point3d center, double radius, string symbol)
        {
            var ids = new List<ObjectId>();

            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.Layer = LayerAxes;
            modelSpace.AppendEntity(circle);
            tx.AddNewlyCreatedDBObject(circle, true);
            ids.Add(circle.ObjectId);

            var text = new DBText
            {
                TextString = symbol,
                Height = radius * 1.2,
                Layer = LayerAxes,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = center
            };
            modelSpace.AppendEntity(text);
            tx.AddNewlyCreatedDBObject(text, true);
            ids.Add(text.ObjectId);

            return ids;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static string GetSymbol(int index, SymbolType type)
        {
            switch (type)
            {
                case SymbolType.Numbers:   return (index + 1).ToString();
                case SymbolType.LowerCase: return ((char)('a' + index)).ToString();
                case SymbolType.UpperCase: return ((char)('A' + index)).ToString();
                default:                   return (index + 1).ToString();
            }
        }

        private static double ComputeBubbleRadius(UnitSystem units)
        {
            return units.Unit == LengthUnit.Inches ? 12.0 : 0.3;
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

        private static void EnsureLayer(Transaction tx, Database db,
            string name, short colorIndex)
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
    }
}
