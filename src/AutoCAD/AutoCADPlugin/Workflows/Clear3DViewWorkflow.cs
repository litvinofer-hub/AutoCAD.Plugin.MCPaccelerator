using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CLEAR_3D command. Erases every entity on the three
    /// MCP_3D_* layers created by <see cref="Show3DViewWorkflow"/>, then
    /// switches the viewport back to a top-down 2D view.
    /// </summary>
    public class Clear3DViewWorkflow
    {
        private static readonly string[] Layers3D =
        {
            "MCP_3D_Walls",
            "MCP_3D_Windows",
            "MCP_3D_Doors"
        };

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            int erased = EraseEntitiesOnLayers();

            if (erased == 0)
            {
                _editor.WriteMessage("\nNo 3D building entities to remove.");
                return;
            }

            _editor.WriteMessage($"\nRemoved {erased} 3D entity(ies).");

            SwitchToTopView();
        }

        private int EraseEntitiesOnLayers()
        {
            var doc = AcadContext.Document;
            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (var entityId in modelSpace)
                {
                    var entity = (Entity)tx.GetObject(entityId, OpenMode.ForRead);

                    foreach (var layer in Layers3D)
                    {
                        if (entity.Layer == layer)
                        {
                            entity.UpgradeOpen();
                            entity.Erase();
                            count++;
                            break;
                        }
                    }
                }

                tx.Commit();
            }

            return count;
        }

        private void SwitchToTopView()
        {
            _editor.Command("_-VIEW", "_TOP");
            _editor.Command("_ZOOM", "_E");
        }
    }
}
