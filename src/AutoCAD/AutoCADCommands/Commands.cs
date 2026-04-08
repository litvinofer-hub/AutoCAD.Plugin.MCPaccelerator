using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
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
        [CommandMethod("OL_SELECT_WALLS")]
        public static void SelectWalls()
        {
            var polylines = SelectClosedPolylinesFromLayers("wall");
            if (polylines != null)
                PrintPolylines(polylines, "Wall");
        }

        [CommandMethod("OL_SELECT_WINDOWS")]
        public static void SelectWindows()
        {
            var polylines = SelectClosedPolylinesFromLayers("window");
            if (polylines != null)
                PrintPolylines(polylines, "Window");
        }

        [CommandMethod("OL_SELECT_DOORS")]
        public static void SelectDoors()
        {
            var polylines = SelectClosedPolylinesFromLayers("door");
            if (polylines != null)
                PrintPolylines(polylines, "Door");
        }

        /// <summary>
        /// Prompts the user to select elements from a single floor plan,
        /// then filters for closed polylines whose layer contains the keyword (case-insensitive).
        /// </summary>
        private static List<Polyline> SelectClosedPolylinesFromLayers(string layerKeyword)
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var editor = document.Editor;

            var options = new PromptSelectionOptions
            {
                MessageForAdding = $"\nSelect floor plan elements to find {layerKeyword}s, then press Enter: "
            };

            var selectionResult = editor.GetSelection(options);

            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nSelection cancelled.");
                return null;
            }

            var result = new List<Polyline>();

            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
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

        private static void PrintPolylines(List<Polyline> polylines, string elementType)
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            editor.WriteMessage($"\n{polylines.Count} {elementType}(s) found:\n");

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
