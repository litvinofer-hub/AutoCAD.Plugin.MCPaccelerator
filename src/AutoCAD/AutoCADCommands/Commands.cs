using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MCPAccelerator.AutoCAD.AutoCADCommands.Converter;
using System;
using System.Collections.Generic;
using System.Linq;

// NETLOAD
// C:\Users\Ofer\Desktop\MCPaccelerator\src\AutoCAD\AutoCADCommands\bin\Debug\net10.0\MCPAccelerator.AutoCAD.AutoCADCommands.dll
// AutoCAD Text Window (F2)

namespace MCPAccelerator.AutoCAD.AutoCADCommands
{
    public class Commands
    {
        [CommandMethod("OL_GET_WALLS")]
        public static void GetWalls()
        {
            var polylines = GetClosedPolylinesFromLayers("wall");
            PrintPolylines(polylines, "wall");
        }

        [CommandMethod("OL_GET_WINDOWS")]
        public static void GetWindows()
        {
            var polylines = GetClosedPolylinesFromLayers("window");
            PrintPolylines(polylines, "window");
        }

        [CommandMethod("OL_GET_DOORS")]
        public static void GetDoors()
        {
            var polylines = GetClosedPolylinesFromLayers("door");
            PrintPolylines(polylines, "door");
        }

        [CommandMethod("OL_BUILD")]
        public static void Build()
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            var wallPolylines = GetClosedPolylinesFromLayers("wall");
            var windowPolylines = GetClosedPolylinesFromLayers("window");
            var doorPolylines = GetClosedPolylinesFromLayers("door");

            var converter = new BuildingConverter();
            var building = converter.Convert(wallPolylines, windowPolylines, doorPolylines,
                botElevation: 0, topElevation: 3.0);

            editor.WriteMessage($"\nBuilding created:");
            editor.WriteMessage($"\n  Walls:   {building.Walls.Count}");
            editor.WriteMessage($"\n  Levels:  {building.Levels.Count}");

            int totalWindows = 0;
            int totalDoors = 0;
            foreach (var wall in building.Walls)
            {
                foreach (var opening in wall.Openings)
                {
                    if (opening is Domain.BuildingModel.Window) totalWindows++;
                    if (opening is Domain.BuildingModel.Door) totalDoors++;
                }
            }

            editor.WriteMessage($"\n  Windows: {totalWindows}");
            editor.WriteMessage($"\n  Doors:   {totalDoors}");
            editor.WriteMessage($"\n  Points:  {building.GetPoints().Count()}");
        }

        /// <summary>
        /// akes a keyword string (e.g. "wall"), opens the current drawing's Model Space in a read transaction, 
        /// iterates all entities, and returns only Polyline objects that are both closed and whose layer name 
        /// contains the keyword (case-insensitive)
        /// </summary>
        private static List<Polyline> GetClosedPolylinesFromLayers(string layerKeyword)
        {
            var result = new List<Polyline>();
            var document = Application.DocumentManager.MdiActiveDocument;
            var database = document.Database;

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId objectId in modelSpace)
                {
                    var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Polyline;
                    if (entity == null)
                        continue;

                    if (!entity.Closed)
                        continue;

                    if (entity.Layer.IndexOf(layerKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    result.Add(entity);
                }

                transaction.Commit();
            }

            return result;
        }

        private static void PrintPolylines(List<Polyline> polylines, string keyword)
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            editor.WriteMessage($"\nFound {polylines.Count} closed polyline(s) on '{keyword}' layers:\n");

            for (int i = 0; i < polylines.Count; i++)
            {
                var polyline = polylines[i];
                editor.WriteMessage($"\n  [{i + 1}] Layer: {polyline.Layer}, Vertices: {polyline.NumberOfVertices}");

                for (int j = 0; j < polyline.NumberOfVertices; j++)
                {
                    var point = polyline.GetPoint3dAt(j);
                    editor.WriteMessage($"\n      Point {j}: ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
                }
            }
        }
    }
}
