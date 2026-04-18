using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Utils
{
    /// <summary>
    /// Centralized registry of every MCP-owned AutoCAD layer: names, ACI colors,
    /// and a single <see cref="Ensure(Transaction, Database, string, short)"/>
    /// helper that creates a layer in the current drawing if it doesn't exist.
    ///
    /// Any workflow or helper that creates or references an MCP layer must use
    /// the constants and methods on this class — never hard-code layer names.
    /// </summary>
    public static class McpLayers
    {
        // ---------- 2D floor plan (PrintBuildingWorkflow) ----------
        public const string Walls   = "MCP_Walls";
        public const string Windows = "MCP_Windows";
        public const string Doors   = "MCP_Doors";

        // ---------- 3D view (Show3DViewWorkflow / Clear3DViewWorkflow) ----------
        public const string Walls3D   = "MCP_3D_Walls";
        public const string Windows3D = "MCP_3D_Windows";
        public const string Doors3D   = "MCP_3D_Doors";

        // ---------- Axial system ----------
        public const string Axes = "MCP_Axial_System";

        // ---------- Working-area frame ----------
        public const string FloorFrame = "MCP_Floor_Frame";

        // ---------- Level plan graphs (PrintGraphsWorkflow) ----------
        public const string GraphEdges  = "MCP_Graph_Edges";
        public const string GraphNodes  = "MCP_Graph_Nodes";
        public const string GraphLabels = "MCP_Graph_Labels";

        // ---------- AutoCAD Color Index (ACI) values used by the layers above ----------
        public const short WhiteColorIndex  = 7;
        public const short RedColorIndex    = 1;
        public const short YellowColorIndex = 2;
        public const short GreenColorIndex  = 3;
        public const short CyanColorIndex   = 4;

        /// <summary>
        /// Convenience grouping of the three 3D layer names — used by workflows
        /// that clear the 3D view or check whether it is currently active.
        /// </summary>
        public static readonly string[] All3D = { Walls3D, Windows3D, Doors3D };

        /// <summary>
        /// Creates <paramref name="name"/> in the drawing's LayerTable with
        /// <paramref name="colorIndex"/>. No-op if the layer already exists.
        /// </summary>
        public static void Ensure(Transaction tx, Database db, string name, short colorIndex)
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
