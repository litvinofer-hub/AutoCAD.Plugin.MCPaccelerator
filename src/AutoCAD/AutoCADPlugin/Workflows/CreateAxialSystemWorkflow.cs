using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CREATE_AXIAL_SYSTEM command. For the selected building
    /// and story, finds all walls parallel to a user-chosen direction, deduplicates
    /// their perpendicular positions, and draws labeled axis lines (with bubbles)
    /// on the MCP_Axial_System layer.
    ///
    /// Prompts:
    /// 1. Pick building and story
    /// 2. Pick direction: X, Y, or Other (custom 2D vector)
    /// 3. Pick symbol type: Numbers (1,2,3), Lowercase (a,b,c), Uppercase (A,B,C)
    /// </summary>
    public class CreateAxialSystemWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";
        private const short AxisColorIndex = 2; // yellow

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // 1. Pick building and story
            var context = BuildingContextPrompt.PickBuildingAndStory("axial system");
            if (context == null) return;
            var (building, story) = context.Value;

            // 2. Pick direction
            var direction = PromptDirection();
            if (direction == null) return;
            Vec2 dir = Vec2Math.Normalize(direction.Value);

            // 3. Pick symbol type
            var symbolType = PromptSymbolType();
            if (symbolType == null) return;

            // 4. Gather walls for this story
            var storyWalls = building.Walls.Where(w => w.StoryId == story.Id).ToList();
            if (storyWalls.Count == 0)
            {
                _editor.WriteMessage("\nNo walls found in this story.");
                return;
            }

            // 5. Filter walls parallel to chosen direction
            var parallelWalls = FindParallelWalls(storyWalls, dir);
            if (parallelWalls.Count == 0)
            {
                _editor.WriteMessage("\nNo walls found parallel to the chosen direction.");
                return;
            }

            // 6. Unique perpendicular positions (deduplicated, sorted)
            var perpDir = Vec2Math.Perpendicular(dir);
            var positions = ComputeUniquePositions(parallelWalls, perpDir, building.Units);

            // 7. Compute extent along direction (across ALL story walls) for line length
            var (minAlong, maxAlong) = ComputeExtentAlongDirection(storyWalls, dir);
            double span = maxAlong - minAlong;
            double margin = span * 0.15;
            if (margin < building.Units.LengthEpsilon) margin = 1.0;

            double bubbleRadius = ComputeBubbleRadius(building.Units);
            double lineStart = minAlong - margin - bubbleRadius * 2;
            double lineEnd = maxAlong + margin + bubbleRadius * 2;

            // 8. Draw
            DrawAxes(positions, dir, perpDir, lineStart, lineEnd,
                symbolType.Value, bubbleRadius);

            _editor.WriteMessage(
                $"\nCreated {positions.Count} axial axis/axes on layer '{LayerAxes}'.");
        }

        // -------------------------------------------------------------------
        // Prompts
        // -------------------------------------------------------------------

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
        // Wall analysis
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns walls whose center-line direction is parallel to
        /// <paramref name="direction"/> (cross product ≈ 0).
        /// </summary>
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

        /// <summary>
        /// Projects each parallel wall's midpoint onto <paramref name="perpDir"/>,
        /// deduplicates within <see cref="UnitSystem.LengthEpsilon"/>, and returns
        /// the sorted list of unique perpendicular positions.
        /// </summary>
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

        /// <summary>
        /// Returns the min/max scalar projections of ALL wall endpoints onto
        /// <paramref name="dir"/>. Used to size the axis lines so they span the
        /// full building footprint.
        /// </summary>
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
        // Drawing
        // -------------------------------------------------------------------

        private void DrawAxes(List<double> positions, Vec2 dir, Vec2 perpDir,
            double lineStart, double lineEnd, SymbolType symbolType, double bubbleRadius)
        {
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

                for (int i = 0; i < positions.Count; i++)
                {
                    double perpPos = positions[i];
                    string symbol = GetSymbol(i, symbolType);

                    // Axis line endpoints:
                    //   point = perpPos * perpDir + t * dir
                    var startPt = new Point3d(
                        perpPos * perpDir.X + lineStart * dir.X,
                        perpPos * perpDir.Y + lineStart * dir.Y, 0);
                    var endPt = new Point3d(
                        perpPos * perpDir.X + lineEnd * dir.X,
                        perpPos * perpDir.Y + lineEnd * dir.Y, 0);

                    // Bubble centers sit just outside the line ends
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

                    // Bubbles at both ends
                    DrawBubble(tx, modelSpace, bubbleStartPt, bubbleRadius, symbol);
                    DrawBubble(tx, modelSpace, bubbleEndPt, bubbleRadius, symbol);
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// Draws a circle with a centered label — the standard architectural
        /// axis bubble.
        /// </summary>
        private static void DrawBubble(Transaction tx, BlockTableRecord modelSpace,
            Point3d center, double radius, string symbol)
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.Layer = LayerAxes;
            modelSpace.AppendEntity(circle);
            tx.AddNewlyCreatedDBObject(circle, true);

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
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private enum SymbolType { Numbers, LowerCase, UpperCase }

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

        /// <summary>
        /// Tries to load the CENTER linetype from acad.lin. Returns
        /// <see cref="ObjectId.Null"/> if unavailable.
        /// </summary>
        private static ObjectId LoadCenterLinetype(Transaction tx, Database db)
        {
            var linetypeTable = (LinetypeTable)tx.GetObject(
                db.LinetypeTableId, OpenMode.ForRead);

            if (linetypeTable.Has("CENTER"))
                return linetypeTable["CENTER"];

            try
            {
                db.LoadLineTypeFile("CENTER", "acad.lin");
                // Re-read after loading
                linetypeTable = (LinetypeTable)tx.GetObject(
                    db.LinetypeTableId, OpenMode.ForRead);
                if (linetypeTable.Has("CENTER"))
                    return linetypeTable["CENTER"];
            }
            catch
            {
                // CENTER linetype not available — fall back to continuous
            }

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
