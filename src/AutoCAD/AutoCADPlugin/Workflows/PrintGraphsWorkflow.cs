using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using AcadPoint3d = Autodesk.AutoCAD.Geometry.Point3d;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_PRINT_GRAPHS command. For each <see cref="Level"/>
    /// of the selected building (in elevation order) whose
    /// <see cref="Level.Graph"/> is non-empty, the workflow:
    /// <list type="number">
    /// <item>Asks the user to click the point that should become the center
    /// of the printed graph for that level.</item>
    /// <item>Draws every graph edge as a 2D line, every graph node as a
    /// small circle, and a coordinate label "(x, y, z)" (rounded to integers)
    /// next to each node. Positions are shifted so the graph's bounding-box
    /// center lands on the picked point.</item>
    /// </list>
    /// Each primitive type goes on its own layer: edges on
    /// <see cref="McpLayers.GraphEdges"/>, nodes on
    /// <see cref="McpLayers.GraphNodes"/>, labels on
    /// <see cref="McpLayers.GraphLabels"/>.
    ///
    /// The picked centers and the ObjectIds of the drawn entities are stored
    /// in <see cref="PrintGraphsRegistry"/> so OL_REFRESH can redraw the
    /// graphs after the underlying model changes. See <see cref="ReprintAll"/>.
    /// </summary>
    public class PrintGraphsWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("print graphs");
            if (building == null) return;

            var entry = PrintGraphsRegistry.GetOrCreate(building);
            EraseEntities(entry.DrawnEntityIds);
            entry.DrawnEntityIds.Clear();
            entry.LevelCenters.Clear();

            var sortedLevels = building.Levels
                .OrderBy(l => l.Elevation)
                .ToList();

            int drawnLevels = 0;
            var totals = new Totals();

            for (int i = 0; i < sortedLevels.Count; i++)
            {
                var level = sortedLevels[i];
                if (level.Graph.Edges.Count == 0) continue;

                var (cx, cy) = ComputeBoundingBoxCenter(level.Graph);
                var picked = PromptLevelCenter(i + 1, level.Elevation);
                if (picked == null) return; // user cancelled

                entry.LevelCenters[level.Id] = picked.Value;

                double dx = picked.Value.X - cx;
                double dy = picked.Value.Y - cy;

                DrawLevelGraph(level.Graph, dx, dy, totals, entry.DrawnEntityIds);
                drawnLevels++;
            }

            _editor.WriteMessage(
                $"\nPrinted graphs for '{building.Name}': {drawnLevels} level(s), " +
                $"{totals.Edges} edge(s), {totals.Nodes} node(s).");
        }

        // -------------------------------------------------------------------
        // Reprint (called by OL_REFRESH)
        // -------------------------------------------------------------------

        /// <summary>
        /// Redraws every building recorded in <see cref="PrintGraphsRegistry"/>,
        /// reusing the level centers picked originally. Each building's
        /// previously-drawn entities are erased first. No prompts — silent replay.
        /// Buildings whose Id no longer resolves in <see cref="BuildingSession"/>
        /// are dropped from the registry.
        /// </summary>
        public static void ReprintAll()
        {
            var buildingIds = PrintGraphsRegistry.All.Keys.ToList();
            if (buildingIds.Count == 0) return;

            var editor = AcadContext.Editor;
            int repainted = 0;

            foreach (var id in buildingIds)
            {
                var building = BuildingSession.Buildings.FirstOrDefault(b => b.Id == id);
                var entry = PrintGraphsRegistry.All[id];

                if (building == null)
                {
                    EraseEntities(entry.DrawnEntityIds);
                    entry.DrawnEntityIds.Clear();
                    PrintGraphsRegistry.Remove(id);
                    continue;
                }

                Reprint(building, entry);
                repainted++;
            }

            if (repainted > 0)
                editor.WriteMessage($"\n  Reprinted graphs for {repainted} building(s).");
        }

        private static void Reprint(Building building, PrintGraphsRegistry.Entry entry)
        {
            EraseEntities(entry.DrawnEntityIds);
            entry.DrawnEntityIds.Clear();

            if (entry.LevelCenters.Count == 0) return;

            var totals = new Totals();

            foreach (var level in building.Levels.OrderBy(l => l.Elevation))
            {
                if (!entry.LevelCenters.TryGetValue(level.Id, out var center)) continue;
                if (level.Graph.Edges.Count == 0) continue;

                var (cx, cy) = ComputeBoundingBoxCenter(level.Graph);
                double dx = center.X - cx;
                double dy = center.Y - cy;

                DrawLevelGraph(level.Graph, dx, dy, totals, entry.DrawnEntityIds);
            }
        }

        // -------------------------------------------------------------------
        // Drawing
        // -------------------------------------------------------------------

        private sealed class Totals
        {
            public int Edges;
            public int Nodes;
        }

        private static void DrawLevelGraph(
            LevelPlanGraph graph, double dx, double dy, Totals totals, List<ObjectId> drawnIds)
        {
            var doc = AcadContext.Document;
            var db = doc.Database;

            // Scale node circle + text to the graph's bounding-box size so
            // they're visible regardless of the building's unit system.
            double scale = ComputeDisplayScale(graph);
            double circleRadius = scale * 0.01;
            double textHeight   = scale * 0.02;
            double textOffset   = circleRadius * 2.0;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                McpLayers.Ensure(tx, db, McpLayers.GraphEdges,  McpLayers.GreenColorIndex);
                McpLayers.Ensure(tx, db, McpLayers.GraphNodes,  McpLayers.YellowColorIndex);
                McpLayers.Ensure(tx, db, McpLayers.GraphLabels, McpLayers.WhiteColorIndex);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Edges as simple 2D lines.
                foreach (var edge in graph.Edges.Values)
                {
                    var p1 = edge.Node1.Point;
                    var p2 = edge.Node2.Point;
                    var line = new Line(
                        new AcadPoint3d(p1.X + dx, p1.Y + dy, 0),
                        new AcadPoint3d(p2.X + dx, p2.Y + dy, 0))
                    {
                        Layer = McpLayers.GraphEdges
                    };
                    drawnIds.Add(modelSpace.AppendEntity(line));
                    tx.AddNewlyCreatedDBObject(line, true);
                    totals.Edges++;
                }

                // Nodes as small circles + coordinate labels.
                foreach (var node in graph.Nodes.Values)
                {
                    var p = node.Point;
                    var center = new AcadPoint3d(p.X + dx, p.Y + dy, 0);

                    var circle = new Circle(center, Vector3d.ZAxis, circleRadius)
                    {
                        Layer = McpLayers.GraphNodes
                    };
                    drawnIds.Add(modelSpace.AppendEntity(circle));
                    tx.AddNewlyCreatedDBObject(circle, true);

                    int rx = (int)Math.Round(p.X);
                    int ry = (int)Math.Round(p.Y);
                    int rz = (int)Math.Round(p.Z);
                    var label = new DBText
                    {
                        TextString = $"({rx}, {ry}, {rz})",
                        Height = textHeight,
                        Position = new AcadPoint3d(center.X + textOffset, center.Y + textOffset, 0),
                        Layer = McpLayers.GraphLabels
                    };
                    drawnIds.Add(modelSpace.AppendEntity(label));
                    tx.AddNewlyCreatedDBObject(label, true);

                    totals.Nodes++;
                }

                tx.Commit();
            }
        }

        private static void EraseEntities(IReadOnlyList<ObjectId> ids)
        {
            if (ids.Count == 0) return;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                    entity?.Erase();
                }
                tx.Commit();
            }
        }

        // -------------------------------------------------------------------
        // Prompts
        // -------------------------------------------------------------------

        private Autodesk.AutoCAD.Geometry.Point3d? PromptLevelCenter(int ordinalIndex, double elevation)
        {
            var options = new PromptPointOptions(
                $"\nPick center for the graph at level {ordinalIndex} (elevation {elevation}): ");

            var result = _editor.GetPoint(options);
            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return result.Value;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static (double x, double y) ComputeBoundingBoxCenter(LevelPlanGraph graph)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var node in graph.Nodes.Values)
            {
                var p = node.Point;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            if (minX > maxX) return (0, 0); // empty — caller should skip
            return ((minX + maxX) / 2, (minY + maxY) / 2);
        }

        /// <summary>
        /// Returns a reference length derived from the graph's bounding box
        /// used to scale node circles and labels. Falls back to 1 when the
        /// graph is tiny / degenerate.
        /// </summary>
        private static double ComputeDisplayScale(LevelPlanGraph graph)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var node in graph.Nodes.Values)
            {
                var p = node.Point;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            if (minX > maxX) return 1.0;

            double scale = Math.Max(maxX - minX, maxY - minY);
            return scale > 1e-6 ? scale : 1.0;
        }
    }
}
