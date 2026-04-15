using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Utils
{
    /// <summary>
    /// Shared helpers for drawing / redrawing the bounding-box frame and label
    /// of a <see cref="FloorPlanWorkingArea"/>. Used by SetFloorPlanAreaWorkflow,
    /// CreateAxialSystemWorkflow, and RefreshWorkflow.
    /// </summary>
    public static class WorkingAreaFrameHelper
    {
        /// <summary>
        /// Draws a 2D axis-aligned bounding-box rectangle + label around all
        /// given ObjectIds. Returns both new ObjectIds.
        /// </summary>
        public static (ObjectId frameId, ObjectId labelId) DrawFrame(
            List<ObjectId> objectIds, string buildingName, string storyName)
        {
            var (minX, minY, maxX, maxY) = ComputeBoundingBox(objectIds);

            double margin = (maxX - minX + maxY - minY) * 0.02;
            minX -= margin;
            minY -= margin;
            maxX += margin;
            maxY += margin;

            return DrawFrameAtBounds(minX, minY, maxX, maxY, buildingName, storyName);
        }

        /// <summary>
        /// Erases the old frame + label, recomputes the bbox from the current
        /// ObjectIds, draws a new frame + label, and updates the working area.
        /// </summary>
        public static void RedrawFrame(FloorPlanWorkingArea area)
        {
            EraseEntity(area.FrameId);
            EraseEntity(area.LabelId);

            var (frameId, labelId) = DrawFrame(
                area.SelectedObjectIds, area.BuildingName, area.StoryName);

            area.FrameId = frameId;
            area.LabelId = labelId;
        }

        /// <summary>
        /// Computes the 2D axis-aligned bounding box of any set of entities
        /// using <see cref="Entity.GeometricExtents"/>.
        /// </summary>
        public static (double minX, double minY, double maxX, double maxY) ComputeBoundingBox(
            List<ObjectId> objectIds)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                foreach (var objectId in objectIds)
                {
                    if (objectId.IsErased) continue;
                    var entity = tx.GetObject(objectId, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;

                    Extents3d extents;
                    try { extents = entity.GeometricExtents; }
                    catch { continue; }

                    if (extents.MinPoint.X < minX) minX = extents.MinPoint.X;
                    if (extents.MaxPoint.X > maxX) maxX = extents.MaxPoint.X;
                    if (extents.MinPoint.Y < minY) minY = extents.MinPoint.Y;
                    if (extents.MaxPoint.Y > maxY) maxY = extents.MaxPoint.Y;
                }

                tx.Commit();
            }

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Returns the boundary corners of a working area's frame polyline
        /// (reads the actual polyline geometry from the drawing).
        /// </summary>
        public static (double minX, double minY, double maxX, double maxY) GetFrameBounds(
            ObjectId frameId)
        {
            var database = AcadContext.Document.Database;

            using (var tx = database.TransactionManager.StartTransaction())
            {
                var polyline = tx.GetObject(frameId, OpenMode.ForRead) as AcadPolyline;
                if (polyline == null || polyline.NumberOfVertices < 4)
                    return (0, 0, 0, 0);

                var p0 = polyline.GetPoint2dAt(0); // bottom-left
                var p2 = polyline.GetPoint2dAt(2); // top-right

                tx.Commit();
                return (p0.X, p0.Y, p2.X, p2.Y);
            }
        }

        // -------------------------------------------------------------------

        private static (ObjectId frameId, ObjectId labelId) DrawFrameAtBounds(
            double minX, double minY, double maxX, double maxY,
            string buildingName, string storyName)
        {
            string label = $"{buildingName} - {storyName}";
            ObjectId frameId, labelId;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                McpLayers.Ensure(tx, db, McpLayers.FloorFrame, McpLayers.GreenColorIndex);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var rect = new AcadPolyline();
                rect.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                rect.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                rect.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                rect.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                rect.Closed = true;
                rect.Layer = McpLayers.FloorFrame;
                modelSpace.AppendEntity(rect);
                tx.AddNewlyCreatedDBObject(rect, true);
                frameId = rect.ObjectId;

                double textHeight = (maxY - minY) * 0.03;
                if (textHeight < 1.0) textHeight = 1.0;

                var text = new DBText
                {
                    TextString = label,
                    Height = textHeight,
                    Layer = McpLayers.FloorFrame,
                    Position = new Point3d(minX, minY - textHeight * 1.5, 0)
                };
                modelSpace.AppendEntity(text);
                tx.AddNewlyCreatedDBObject(text, true);
                labelId = text.ObjectId;

                tx.Commit();
            }

            return (frameId, labelId);
        }

        private static void EraseEntity(ObjectId id)
        {
            if (id.IsNull || id.IsErased) return;

            var doc = AcadContext.Document;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var entity = tx.GetObject(id, OpenMode.ForWrite) as Entity;
                entity?.Erase();
                tx.Commit();
            }
        }
    }
}
