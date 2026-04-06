using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

// NETLOAD
// C:\Users\Ofer\Desktop\MCPaccelerator\src\Presentation\AutoCADCommands\bin\Debug\net10.0\MCPAccelerator.Presentation.AutoCADCommands.dll

namespace MCPAccelerator.Presentation.AutoCADCommands
{
    public class Commands
    {
        [CommandMethod("PS_GET_WALLS")]
        public static void GetWalls()
        {
            var polylines = GetClosedPolylinesFromLayers("wall");
        }

        [CommandMethod("PS_GET_WINDOWS")]
        public static void GetWindows()
        {
            var polylines = GetClosedPolylinesFromLayers("window");
        }

        [CommandMethod("PS_GET_DOORS")]
        public static void GetDoors()
        {
            var polylines = GetClosedPolylinesFromLayers("door");
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
    }
}
