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
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_SHOW_3D command. For the selected building, creates
    /// AutoCAD <see cref="Solid3d"/> extrusions for every wall segment, window,
    /// and door, placed at their real-world X/Y/Z coordinates. Each element type
    /// gets its own layer and color. After drawing, the viewport switches to a
    /// south-west isometric view so the user immediately sees the 3D result.
    /// </summary>
    public class Show3DViewWorkflow
    {
        private const string Layer3DWalls   = "MCP_3D_Walls";
        private const string Layer3DWindows = "MCP_3D_Windows";
        private const string Layer3DDoors   = "MCP_3D_Doors";

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("show in 3D");
            if (building == null) return;

            if (building.Walls.Count == 0)
            {
                _editor.WriteMessage($"\nBuilding '{building.Name}' has no walls to display.");
                return;
            }

            var totals = new Totals();
            DrawBuilding3D(building, totals);

            _editor.WriteMessage(
                $"\n3D view of '{building.Name}': {totals.SubWalls} wall solid(s), " +
                $"{totals.Sills} sill(s), {totals.Lintels} lintel(s), " +
                $"{totals.Windows} window(s), {totals.Doors} door(s).");

            SwitchToIsometricView();
        }

        // -------------------------------------------------------------------
        // Drawing
        // -------------------------------------------------------------------

        private sealed class Totals
        {
            public int SubWalls;
            public int Sills;
            public int Lintels;
            public int Windows;
            public int Doors;
        }

        private void DrawBuilding3D(Building building, Totals totals)
        {
            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                EnsureLayer(tx, db, Layer3DWalls,   colorIndex: 7);  // white
                EnsureLayer(tx, db, Layer3DWindows, colorIndex: 4);  // cyan
                EnsureLayer(tx, db, Layer3DDoors,   colorIndex: 1);  // red

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var wall in building.Walls)
                {
                    double botZ = wall.BotLevel.Elevation;
                    double topZ = botZ + wall.Height;

                    // Solid wall segments (horizontal strips between openings) — full height
                    foreach (var subWall in wall.SubWalls())
                    {
                        ExtrudeRect(tx, modelSpace, subWall, botZ, wall.Height, Layer3DWalls);
                        totals.SubWalls++;
                    }

                    // Per opening: sill (below), panel (door/window), lintel (above)
                    foreach (var opening in wall.Openings)
                    {
                        var rect = opening.Line.ToRect(wall.Thickness);
                        double openingBotZ = opening.Line.StartPoint.Z;
                        double openingTopZ = openingBotZ + opening.Height;

                        // Sill: solid wall below the opening
                        if (openingBotZ > botZ)
                        {
                            ExtrudeRect(tx, modelSpace, rect, botZ, openingBotZ - botZ, Layer3DWalls);
                            totals.Sills++;
                        }

                        // Lintel: solid wall above the opening
                        if (openingTopZ < topZ)
                        {
                            ExtrudeRect(tx, modelSpace, rect, openingTopZ, topZ - openingTopZ, Layer3DWalls);
                            totals.Lintels++;
                        }

                        // Panel: the door/window itself
                        if (opening is Door)
                        {
                            ExtrudeRect(tx, modelSpace, rect, openingBotZ, opening.Height, Layer3DDoors);
                            totals.Doors++;
                        }
                        else
                        {
                            ExtrudeRect(tx, modelSpace, rect, openingBotZ, opening.Height, Layer3DWindows);
                            totals.Windows++;
                        }
                    }
                }

                tx.Commit();
            }
        }

        // -------------------------------------------------------------------
        // 3D helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Creates a <see cref="Solid3d"/> box by building a closed polyline from
        /// the rect's 4 corners at <paramref name="baseZ"/>, converting it to a
        /// <see cref="Region"/>, and extruding it upward by <paramref name="height"/>.
        /// </summary>
        private static void ExtrudeRect(Transaction tx, BlockTableRecord modelSpace,
            Rect rect, double baseZ, double height, string layer)
        {
            // Build a closed polyline at the base elevation
            using (var profile = new AcadPolyline())
            {
                for (int i = 0; i < 4; i++)
                {
                    var p = rect.Points[i];
                    profile.AddVertexAt(i, new Point2d(p.X, p.Y), 0, 0, 0);
                }
                profile.Closed = true;
                profile.Elevation = baseZ;

                // Create a region from the profile
                var curves = new DBObjectCollection { profile };
                var regions = Region.CreateFromCurves(curves);
                if (regions.Count == 0) return;

                var region = (Region)regions[0];

                // Extrude the region into a solid
                var solid = new Solid3d();
                solid.Extrude(region, height, taperAngle: 0);
                solid.Layer = layer;

                modelSpace.AppendEntity(solid);
                tx.AddNewlyCreatedDBObject(solid, true);

                region.Dispose();
                // Dispose any extra regions (shouldn't happen for a simple rect)
                for (int i = 1; i < regions.Count; i++)
                    regions[i].Dispose();
            }
        }

        /// <summary>
        /// Switches the current viewport to a south-west isometric view with a
        /// shaded visual style so solids appear opaque instead of wireframe.
        /// </summary>
        private void SwitchToIsometricView()
        {
            _editor.Command("_-VIEW", "_SWISO");
            _editor.Command("_ZOOM", "_E");
            // Shaded visual style — otherwise the default 2D Wireframe makes
            // solids look transparent (only edges are drawn).
            _editor.Command("_-VISUALSTYLES", "_Current", "_Shaded");
        }

        // -------------------------------------------------------------------
        // Layer helper (same pattern as PrintBuildingWorkflow)
        // -------------------------------------------------------------------

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
    }
}
