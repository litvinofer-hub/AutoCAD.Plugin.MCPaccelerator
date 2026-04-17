using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The command creates the whole axial system in one shot — there is no
    /// "add one more direction" flow. If a system already exists on the
    /// building, the user must run OL_CLEAR_AXIAL_SYSTEM first.
    ///
    /// Flow:
    /// 1. User supplies the number of axial directions (N ≥ 2).
    /// 2. For each direction i = 1..N, user picks direction (X/Y/Other) and
    ///    symbol type (Numbers/Lowercase/Uppercase). A direction parallel to
    ///    any already picked one is rejected up front.
    /// 3. Wall analysis runs in <b>canvas space</b> (walls aren't reingested
    ///    yet) to produce each direction's axis-line positions.
    /// 4. The building origin is <b>derived</b> — it's the intersection of the
    ///    first axis line of direction 1 with the first axis line of direction
    ///    2. With UpperCase × Numbers this intersection is the conventional
    ///    "A-1" point; with other symbol choices it's the equivalent first-line
    ///    intersection.
    /// 5. <see cref="Story.CanvasOrigin"/> is set to that derived anchor, the
    ///    source story's walls are re-ingested into building space, and every
    ///    axis line is translated from canvas space into building space
    ///    (subtract the anchor).
    /// 6. A new <see cref="AxialSystem"/> is created on the Building with all
    ///    N directions, drawn on the source FPWA, and propagated to any other
    ///    story that is already registered.
    ///
    /// Axis-line geometry is stored in <see cref="AxialLine.Line"/> in building
    /// space, and drawn on the canvas at <c>story.BuildingToCanvas(...)</c>.
    /// </summary>
    public class CreateAxialSystemWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // 1. Pick building and source story
            var context = BuildingContextPrompt.PickBuildingAndStory("axial system");
            if (context == null) return;
            var (building, story) = context.Value;

            // 2. Refuse if an axial system already exists — "create once or clear all".
            if (building.AxialSystem != null)
            {
                _editor.WriteMessage(
                    "\nBuilding already has an axial system. " +
                    "Run OL_CLEAR_AXIAL_SYSTEM to delete it first.");
                return;
            }

            // 3. Look up working area (needed early so we know we can draw/refresh)
            var workingAreas = BuildingSession.GetWorkingAreas(building);
            var area = workingAreas?.FindByStory(story.Id);
            if (area == null)
            {
                _editor.WriteMessage(
                    "\nNo floor plan working area registered for this story. " +
                    "Run OL_CREATE_FLOOR_PLAN_AREA first.");
                return;
            }

            // 4. How many directions?
            int? countResult = PromptDirectionCount();
            if (countResult == null) return;
            int directionCount = countResult.Value;

            // 5. Pick (direction, symbol type) for each. Reject a direction
            //    parallel to any already-picked one.
            var specs = new List<(Vec2 dir, SymbolType symbolType)>();
            while (specs.Count < directionCount)
            {
                _editor.WriteMessage($"\n--- Direction {specs.Count + 1} of {directionCount} ---");

                var directionRes = PromptDirection();
                if (directionRes == null) return;
                var dir = Vec2Math.Normalize(directionRes.Value);

                if (IsParallelToAny(dir, specs))
                {
                    _editor.WriteMessage(
                        "\nThis direction is parallel to a previously picked direction. " +
                        "Pick a non-parallel direction.");
                    continue;
                }

                var sym = PromptSymbolType();
                if (sym == null) return;

                specs.Add((dir, sym.Value));
            }

            // 6. Gather this story's walls — still in CANVAS space (no reingest yet).
            var storyWalls = building.Walls.Where(w => w.StoryId == story.Id).ToList();
            if (storyWalls.Count == 0)
            {
                _editor.WriteMessage("\nNo walls found in this story.");
                return;
            }

            // 7. For each direction, analyze walls (in canvas space) and build
            //    its axis lines (also in canvas space for now).
            double bubbleRadius = ComputeBubbleRadius(building.Units);
            var perDirection = new List<(Vec2 dir, AxisSymbolType symbol, List<AxialLine> canvasLines)>();

            foreach (var (dir, symbolType) in specs)
            {
                var parallelWalls = FindParallelWalls(storyWalls, dir);
                if (parallelWalls.Count == 0)
                {
                    _editor.WriteMessage(
                        $"\nDirection ({dir.X:0.##}, {dir.Y:0.##}) has no parallel walls — cannot place axes.");
                    return;
                }

                var perpDir = Vec2Math.Perpendicular(dir);
                var positions = ComputeUniquePositions(parallelWalls, perpDir, building.Units);
                var (minAlong, maxAlong) = ComputeExtentAlongDirection(storyWalls, dir);
                double span = maxAlong - minAlong;
                double margin = span * 0.15;
                if (margin < building.Units.LengthEpsilon) margin = 1.0;

                double lineStart = minAlong - margin - bubbleRadius * 2;
                double lineEnd   = maxAlong + margin + bubbleRadius * 2;

                var domainSymbolType = symbolType switch
                {
                    SymbolType.Numbers   => AxisSymbolType.Numbers,
                    SymbolType.LowerCase => AxisSymbolType.LowerCase,
                    SymbolType.UpperCase => AxisSymbolType.UpperCase,
                    _                    => AxisSymbolType.Numbers
                };

                var canvasLines = new List<AxialLine>();
                for (int i = 0; i < positions.Count; i++)
                {
                    double perpPos = positions[i];
                    string symbol = GetSymbol(i, symbolType);

                    var startPt = new Point(
                        perpPos * perpDir.X + lineStart * dir.X,
                        perpPos * perpDir.Y + lineStart * dir.Y, 0);
                    var endPt = new Point(
                        perpPos * perpDir.X + lineEnd * dir.X,
                        perpPos * perpDir.Y + lineEnd * dir.Y, 0);

                    canvasLines.Add(new AxialLine(symbol, new LineSegment(startPt, endPt)));
                }

                perDirection.Add((dir, domainSymbolType, canvasLines));
            }

            // 8. Derive the canvas anchor from the intersection of the first
            //    axis lines of directions 1 and 2. With UpperCase × Numbers
            //    this is the "A-1" point.
            var anchorResult = IntersectFirstLines(perDirection[0], perDirection[1]);
            if (anchorResult == null)
            {
                // Guarded against earlier by the parallel-direction rejection,
                // but keep a defensive message in case of degenerate geometry.
                _editor.WriteMessage(
                    "\nFailed to compute origin from the first axis lines (directions are parallel).");
                return;
            }
            Vec2 anchor = anchorResult.Value;

            _editor.WriteMessage(
                $"\nBuilding origin set at canvas ({anchor.X:0.##}, {anchor.Y:0.##}). " +
                "Re-ingesting walls in building space...");

            // 9. Commit: set canvas origin, reingest walls, translate every
            //    collected axis line from canvas space into building space.
            story.SetCanvasOrigin(anchor);
            StoryReingestion.Reingest(building, story, area);

            for (int i = 0; i < perDirection.Count; i++)
            {
                var (dir, symbol, canvasLines) = perDirection[i];
                var buildingLines = TranslateLinesToBuildingSpace(canvasLines, anchor);
                perDirection[i] = (dir, symbol, buildingLines);
            }

            // 10. Build and install the AxialSystem.
            var axialSystem = new AxialSystem(building.Id, bubbleRadius);
            foreach (var (dir, symbol, lines) in perDirection)
                axialSystem.AddDirection(new AxialSystemDirection(dir, symbol, lines));
            building.SetAxialSystem(axialSystem);

            // 11. Draw all axes on the source FPWA and record mappings.
            int totalLines = 0;
            foreach (var (dir, _, lines) in perDirection)
            {
                var idsPerLine = DrawAxes(lines, dir, bubbleRadius, story);
                foreach (var (axialLine, ids) in idsPerLine)
                {
                    area.SelectedObjectIds.AddRange(ids);
                    area.MapDomainElement(axialLine.Id, ids);
                }
                totalLines += lines.Count;
            }
            WorkingAreaFrameHelper.RedrawFrame(area);

            // 12. Propagate to other already-registered stories (defensive —
            //     with the "clear first" rule this is normally unreachable).
            foreach (var otherStory in building.Stories)
            {
                if (ReferenceEquals(otherStory, story)) continue;
                if (!otherStory.HasCanvasOrigin) continue;

                var otherArea = workingAreas.FindByStory(otherStory.Id);
                if (otherArea == null) continue;

                foreach (var (dir, _, lines) in perDirection)
                {
                    var otherIds = DrawAxes(lines, dir, bubbleRadius, otherStory);
                    foreach (var (axialLine, ids) in otherIds)
                    {
                        otherArea.SelectedObjectIds.AddRange(ids);
                        otherArea.MapDomainElement(axialLine.Id, ids);
                    }
                }
                WorkingAreaFrameHelper.RedrawFrame(otherArea);
            }

            _editor.WriteMessage(
                $"\nCreated axial system with {perDirection.Count} direction(s) and " +
                $"{totalLines} axis line(s) on layer '{McpLayers.Axes}'.");
        }

        // -------------------------------------------------------------------
        // Prompts
        // -------------------------------------------------------------------

        private int? PromptDirectionCount()
        {
            var options = new PromptIntegerOptions(
                "\nHow many axial directions do you want? (minimum 2): ")
            {
                AllowNone = false,
                AllowNegative = false,
                AllowZero = false,
                LowerLimit = 2
            };

            var result = _editor.GetInteger(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return result.Value;
        }

        /// <summary>
        /// True if <paramref name="dir"/> is parallel (same or opposite) to any
        /// direction already collected in <paramref name="specs"/>.
        /// </summary>
        private static bool IsParallelToAny(Vec2 dir,
            List<(Vec2 dir, SymbolType symbolType)> specs)
        {
            foreach (var (existing, _) in specs)
            {
                double cross = existing.X * dir.Y - existing.Y * dir.X;
                if (GeometrySettings.AreEqual(cross, 0))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Intersects the first axis line of <paramref name="d1"/> with the
        /// first axis line of <paramref name="d2"/>, treating both as infinite
        /// lines through their stored endpoints. Returns null if the lines are
        /// parallel.
        /// </summary>
        private static Vec2? IntersectFirstLines(
            (Vec2 dir, AxisSymbolType symbol, List<AxialLine> canvasLines) d1,
            (Vec2 dir, AxisSymbolType symbol, List<AxialLine> canvasLines) d2)
        {
            if (d1.canvasLines.Count == 0 || d2.canvasLines.Count == 0)
                return null;

            var p1 = new Vec2(
                d1.canvasLines[0].Line.StartPoint.X,
                d1.canvasLines[0].Line.StartPoint.Y);
            var p2 = new Vec2(
                d2.canvasLines[0].Line.StartPoint.X,
                d2.canvasLines[0].Line.StartPoint.Y);

            double cross = d1.dir.X * d2.dir.Y - d1.dir.Y * d2.dir.X;
            if (GeometrySettings.AreEqual(cross, 0))
                return null;

            // Solve p1 + t * d1.dir = p2 + s * d2.dir for t.
            var dp = Vec2Math.Subtract(p2, p1);
            double t = (dp.X * d2.dir.Y - dp.Y * d2.dir.X) / cross;

            return new Vec2(p1.X + t * d1.dir.X, p1.Y + t * d1.dir.Y);
        }

        /// <summary>
        /// Returns a new list of <see cref="AxialLine"/>s whose endpoints have
        /// <paramref name="anchor"/> subtracted — converting canvas-space
        /// coordinates into building-space coordinates.
        /// </summary>
        private static List<AxialLine> TranslateLinesToBuildingSpace(
            List<AxialLine> canvasLines, Vec2 anchor)
        {
            var result = new List<AxialLine>(canvasLines.Count);
            foreach (var line in canvasLines)
            {
                var s = line.Line.StartPoint;
                var e = line.Line.EndPoint;
                var newStart = new Point(s.X - anchor.X, s.Y - anchor.Y, s.Z);
                var newEnd   = new Point(e.X - anchor.X, e.Y - anchor.Y, e.Z);
                result.Add(new AxialLine(line.Symbol, new LineSegment(newStart, newEnd)));
            }
            return result;
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
                McpLayers.Ensure(tx, db, McpLayers.Axes, McpLayers.YellowColorIndex);
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
                    line.Layer = McpLayers.Axes;
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
            circle.Layer = McpLayers.Axes;
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

    }
}
