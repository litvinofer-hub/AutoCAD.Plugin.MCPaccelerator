using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Selection
{
    /// <summary>
    /// Result of classifying a user selection into wall/window/door polylines by layer name.
    /// </summary>
    public class ClassifiedPolylines
    {
        public List<Polyline> Walls { get; } = [];
        public List<Polyline> Windows { get; } = [];
        public List<Polyline> Doors { get; } = [];

        public int Total => Walls.Count + Windows.Count + Doors.Count;
    }

    /// <summary>
    /// Prompts the user for a selection and classifies each closed polyline
    /// into walls / windows / doors based on its layer name.
    /// </summary>
    public static class FloorPlanSelection
    {
        /// <summary>
        /// Returns classified polylines, or null if the user cancelled selection.
        /// </summary>
        public static ClassifiedPolylines SelectAndClassify()
        {
            var editor = AcadContext.Editor;

            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect floor plan elements (walls, windows, doors), then press Enter: "
            };

            var selectionResult = editor.GetSelection(options);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nSelection cancelled.");
                return null;
            }

            var result = new ClassifiedPolylines();
            var database = AcadContext.Document.Database;

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                {
                    if (transaction.GetObject(objectId, OpenMode.ForRead) is Polyline polyline
                        && polyline.Closed)
                    {
                        Classify(polyline, result);
                    }
                }

                transaction.Commit();
            }

            return result;
        }

        private static void Classify(Polyline polyline, ClassifiedPolylines bins)
        {
            string layer = polyline.Layer;

            if (layer.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
                bins.Walls.Add(polyline);
            else if (layer.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0)
                bins.Windows.Add(polyline);
            else if (layer.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
                bins.Doors.Add(polyline);
        }
    }
}
