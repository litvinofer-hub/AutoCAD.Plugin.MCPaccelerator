using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_PRINT_BUILDING command. For each story of the selected
    /// building (in elevation order) the workflow:
    /// <list type="number">
    /// <item>Asks the user to click the point that should become the center of
    /// the printed floor plan for that story.</item>
    /// <item>Immediately draws that story's walls (minus their openings, via
    /// <see cref="Wall.SubWalls"/>), windows, and doors as closed 2D polylines,
    /// shifted so the story's bounding-box center lands on the picked point.</item>
    /// </list>
    /// Walls, windows, and doors each go on their own layer (created on demand).
    /// Only X/Y are used — the drawing is pure 2D, Z is dropped.
    ///
    /// The picked centers and the ObjectIds of the drawn entities are stored in
    /// <see cref="PrintBuildingRegistry"/> so that OL_REFRESH can redraw the
    /// floor plan automatically after the underlying model changes. See
    /// <see cref="ReprintAll"/>.
    /// </summary>
    public class PrintBuildingWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("print");
            if (building == null) return;

            // Starting a fresh print pass: erase any previously printed geometry
            // for this building and reset the remembered centers.
            var entry = PrintBuildingRegistry.GetOrCreate(building);
            EraseEntities(entry.DrawnEntityIds);
            entry.DrawnEntityIds.Clear();
            entry.StoryCenters.Clear();

            // Walls know their story directly (Building.AddWall enforces StoryId).
            // A simple GroupBy is all we need — no elevation lookups at the print site.
            var wallsByStory = building.Walls
                .GroupBy(w => w.StoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sortedStories = building.Stories
                .OrderBy(s => s.BotLevel.Elevation)
                .ToList();

            var totals = new Totals();

            // One story at a time: prompt → draw → move on. The user sees the
            // previous story already on the canvas before picking the next center.
            for (int i = 0; i < sortedStories.Count; i++)
            {
                var story = sortedStories[i];
                if (!wallsByStory.TryGetValue(story.Id, out var storyWalls) || storyWalls.Count == 0)
                    continue;

                var (cx, cy) = ComputeBoundingBoxCenter(storyWalls);
                var picked = PromptStoryCenter(i + 1, story.Name);
                if (picked == null) return; // user cancelled

                entry.StoryCenters[story.Id] = picked.Value;

                double dx = picked.Value.X - cx;
                double dy = picked.Value.Y - cy;

                DrawStory(storyWalls, dx, dy, totals, entry.DrawnEntityIds);
            }

            _editor.WriteMessage(
                $"\nPrinted '{building.Name}': {totals.SubWalls} sub-wall(s), " +
                $"{totals.Windows} window(s), {totals.Doors} door(s).");
        }

        // -------------------------------------------------------------------
        // Reprint (called by OL_REFRESH)
        // -------------------------------------------------------------------

        /// <summary>
        /// Redraws every building recorded in <see cref="PrintBuildingRegistry"/>,
        /// reusing the story centers picked originally. Each building is erased
        /// first (its previously-drawn entities) before being drawn again. No
        /// prompts are shown — this is silent replay.
        ///
        /// Buildings whose Id no longer resolves in <see cref="BuildingSession"/>
        /// are dropped from the registry.
        /// </summary>
        public static void ReprintAll()
        {
            // Snapshot keys so we can remove stale entries during iteration.
            var buildingIds = PrintBuildingRegistry.All.Keys.ToList();
            if (buildingIds.Count == 0) return;

            var editor = AcadContext.Editor;
            int repainted = 0;

            foreach (var id in buildingIds)
            {
                var building = BuildingSession.Buildings.FirstOrDefault(b => b.Id == id);
                var entry = PrintBuildingRegistry.All[id];

                if (building == null)
                {
                    // Building gone from the session — clean up any leftover geometry
                    // and drop the orphan entry.
                    EraseEntities(entry.DrawnEntityIds);
                    entry.DrawnEntityIds.Clear();
                    PrintBuildingRegistry.Remove(id);
                    continue;
                }

                Reprint(building, entry);
                repainted++;
            }

            if (repainted > 0)
                editor.WriteMessage($"\n  Reprinted {repainted} building(s).");
        }

        private static void Reprint(Building building, PrintBuildingRegistry.Entry entry)
        {
            // Erase last pass before drawing the new one — otherwise old geometry
            // lingers underneath updated shapes.
            EraseEntities(entry.DrawnEntityIds);
            entry.DrawnEntityIds.Clear();

            if (entry.StoryCenters.Count == 0) return;

            var wallsByStory = building.Walls
                .GroupBy(w => w.StoryId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sortedStories = building.Stories
                .OrderBy(s => s.BotLevel.Elevation)
                .ToList();

            var totals = new Totals();

            foreach (var story in sortedStories)
            {
                if (!entry.StoryCenters.TryGetValue(story.Id, out var center)) continue;
                if (!wallsByStory.TryGetValue(story.Id, out var storyWalls) || storyWalls.Count == 0) continue;

                var (cx, cy) = ComputeBoundingBoxCenter(storyWalls);
                double dx = center.X - cx;
                double dy = center.Y - cy;

                DrawStory(storyWalls, dx, dy, totals, entry.DrawnEntityIds);
            }
        }

        // -------------------------------------------------------------------
        // Drawing
        // -------------------------------------------------------------------

        private sealed class Totals
        {
            public int SubWalls;
            public int Windows;
            public int Doors;
        }

        private static void DrawStory(
            List<Wall> walls, double dx, double dy, Totals totals, List<ObjectId> drawnIds)
        {
            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                McpLayers.Ensure(tx, db, McpLayers.Walls,   McpLayers.WhiteColorIndex);
                McpLayers.Ensure(tx, db, McpLayers.Windows, McpLayers.CyanColorIndex);
                McpLayers.Ensure(tx, db, McpLayers.Doors,   McpLayers.RedColorIndex);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var wall in walls)
                {
                    // Walls are drawn as the pieces BETWEEN their openings.
                    foreach (var subWall in wall.SubWalls())
                    {
                        DrawRect(tx, modelSpace, subWall, McpLayers.Walls, dx, dy, drawnIds);
                        totals.SubWalls++;
                    }

                    // Openings as their own closed polylines.
                    foreach (var opening in wall.Openings)
                    {
                        var rect = opening.Line.ToRect(wall.Thickness);
                        if (opening is Door)
                        {
                            DrawRect(tx, modelSpace, rect, McpLayers.Doors, dx, dy, drawnIds);
                            totals.Doors++;
                        }
                        else
                        {
                            DrawRect(tx, modelSpace, rect, McpLayers.Windows, dx, dy, drawnIds);
                            totals.Windows++;
                        }
                    }
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// Erases every still-valid entity in <paramref name="ids"/> from the
        /// drawing. Missing or already-erased ids are skipped silently.
        /// </summary>
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

        /// <summary>
        /// Asks the user to click the point that should become the center of
        /// the printed story. Returns null on cancel.
        /// </summary>
        private Point3d? PromptStoryCenter(int ordinalIndex, string storyName)
        {
            var options = new PromptPointOptions(
                $"\nPick center for the {Ordinal.For(ordinalIndex)} story '{storyName}': ");

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

        /// <summary>
        /// XY bounding-box center of all wall endpoints — the "natural" reference
        /// point that the user-picked center will replace.
        /// </summary>
        private static (double x, double y) ComputeBoundingBoxCenter(List<Wall> walls)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var wall in walls)
            {
                Update(wall.BotLine.StartPoint);
                Update(wall.BotLine.EndPoint);
            }

            return ((minX + maxX) / 2, (minY + maxY) / 2);

            void Update(Point p)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        /// <summary>
        /// Draws a closed 2D polyline from the first 4 corners of <paramref name="rect"/>,
        /// shifted by (<paramref name="dx"/>, <paramref name="dy"/>). Z is dropped — the
        /// polyline sits at elevation 0 in pure 2D. Appends the new entity's ObjectId
        /// to <paramref name="drawnIds"/> so it can be erased on the next reprint.
        /// </summary>
        private static void DrawRect(Transaction tx, BlockTableRecord modelSpace,
            Rect rect, string layer, double dx, double dy, List<ObjectId> drawnIds)
        {
            var polyline = new AcadPolyline();
            for (int i = 0; i < 4; i++)
            {
                var p = rect.Points[i];
                polyline.AddVertexAt(i, new Point2d(p.X + dx, p.Y + dy),
                    bulge: 0, startWidth: 0, endWidth: 0);
            }
            polyline.Closed = true;
            polyline.Layer = layer;

            var id = modelSpace.AppendEntity(polyline);
            tx.AddNewlyCreatedDBObject(polyline, true);
            drawnIds.Add(id);
        }
    }
}
