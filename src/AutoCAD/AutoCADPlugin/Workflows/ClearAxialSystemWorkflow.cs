using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CLEAR_AXIAL_SYSTEM command. Erases every entity on
    /// the MCP_Axial_System layer created by <see cref="CreateAxialSystemWorkflow"/>.
    /// </summary>
    public class ClearAxialSystemWorkflow
    {
        private const string LayerAxes = "MCP_Axial_System";

        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            int erased = EraseEntitiesOnLayer();

            if (erased == 0)
            {
                _editor.WriteMessage("\nNo axial system entities to remove.");
                return;
            }

            _editor.WriteMessage($"\nRemoved {erased} axial system entity(ies).");
        }

        private int EraseEntitiesOnLayer()
        {
            var doc = AcadContext.Document;
            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!layerTable.Has(LayerAxes))
                    return 0;

                var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)tx.GetObject(
                    blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (var entityId in modelSpace)
                {
                    var entity = (Entity)tx.GetObject(entityId, OpenMode.ForRead);
                    if (entity.Layer == LayerAxes)
                    {
                        entity.UpgradeOpen();
                        entity.Erase();
                        count++;
                    }
                }

                tx.Commit();
            }

            return count;
        }
    }
}
