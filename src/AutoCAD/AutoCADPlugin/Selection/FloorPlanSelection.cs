using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Selection
{
    /// <summary>
    /// Raw result of user selection — every entity the user picked,
    /// regardless of type. No filtering has been applied.
    /// </summary>
    public class RawSelection
    {
        /// <summary>ObjectIds of every entity the user selected.</summary>
        public List<ObjectId> ObjectIds { get; } = [];
    }

    /// <summary>
    /// Result of classifying closed polylines into wall/window/door
    /// by layer name.
    /// </summary>
    public class ClassifiedPolylines
    {
        public List<Polyline> Walls { get; } = [];
        public List<Polyline> Windows { get; } = [];
        public List<Polyline> Doors { get; } = [];

        public int Total => Walls.Count + Windows.Count + Doors.Count;
    }

    /// <summary>
    /// Three-phase selection helper:
    /// <list type="number">
    /// <item><see cref="Select"/> — prompts the user, returns every selected
    /// entity's ObjectId (any type — lines, polylines, circles, blocks, etc.).
    /// No filtering at all.</item>
    /// <item><see cref="FilterClosedPolylines"/> — takes a raw selection and
    /// returns only the closed polylines.</item>
    /// <item><see cref="Classify"/> — sorts closed polylines into walls /
    /// windows / doors by layer name.</item>
    /// </list>
    /// Callers control what happens between phases (e.g. persisting the full
    /// raw ObjectIds to a <see cref="FloorPlanWorkingArea"/> before filtering).
    /// </summary>
    public static class FloorPlanSelection
    {
        /// <summary>
        /// Prompts the user to select floor plan elements. Returns every
        /// selected entity's ObjectId with no filtering. Returns null on cancel.
        /// </summary>
        public static RawSelection Select()
        {
            var editor = AcadContext.Editor;

            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect floor plan elements, then press Enter: "
            };

            var selectionResult = editor.GetSelection(options);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nSelection cancelled.");
                return null;
            }

            var raw = new RawSelection();
            foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                raw.ObjectIds.Add(objectId);

            return raw;
        }

        /// <summary>
        /// Filters a raw selection down to closed polylines only.
        /// All other entity types are silently skipped.
        /// </summary>
        public static List<Polyline> FilterClosedPolylines(RawSelection raw)
        {
            var result = new List<Polyline>();
            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in raw.ObjectIds)
                {
                    if (tx.GetObject(objectId, OpenMode.ForRead) is Polyline polyline
                        && polyline.Closed)
                    {
                        result.Add(polyline);
                    }
                }

                tx.Commit();
            }

            return result;
        }

        /// <summary>
        /// Classifies closed polylines into walls / windows / doors based on
        /// their layer names. Call this after filtering.
        /// </summary>
        public static ClassifiedPolylines Classify(List<Polyline> closedPolylines)
        {
            var result = new ClassifiedPolylines();

            foreach (var polyline in closedPolylines)
            {
                string layer = polyline.Layer;

                if (layer.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Walls.Add(polyline);
                else if (layer.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Windows.Add(polyline);
                else if (layer.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Doors.Add(polyline);
            }

            return result;
        }
    }
}
