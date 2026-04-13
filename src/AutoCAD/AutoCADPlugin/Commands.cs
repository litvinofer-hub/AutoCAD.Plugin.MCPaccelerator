using Autodesk.AutoCAD.Runtime;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows;

// NETLOAD
// C:\Users\Ofer\Desktop\MCPaccelerator\src\AutoCAD\AutoCADPlugin\bin\Debug\net10.0\MCPAccelerator.AutoCAD.AutoCADPlugin.dll
// AutoCAD Text Window (F2)

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// AutoCAD command entry points. This file is intentionally a table of
    /// contents — each [CommandMethod] delegates immediately to a workflow
    /// class under Workflows/. All the actual logic lives there.
    /// </summary>
    public class Commands
    {
        [CommandMethod("OL_CREATE_BUILDING")]
        public static void CreateBuilding() => new CreateBuildingWorkflow().Run();

        [CommandMethod("OL_SELECT_FLOOR_PLAN")]
        public static void SelectFloorPlan() => new SelectFloorPlanWorkflow().Run();

        [CommandMethod("OL_CREATE_AXIAL_SYSTEM")]
        public static void CreateAxialSystem() => new CreateAxialSystemWorkflow().Run();

        [CommandMethod("OL_CLEAR_AXIAL_SYSTEM")]
        public static void ClearAxialSystem() => new ClearAxialSystemWorkflow().Run();

        [CommandMethod("OL_PRINT_BUILDING")]
        public static void PrintBuilding() => new PrintBuildingWorkflow().Run();

        [CommandMethod("OL_DELETE_BUILDING")]
        public static void DeleteBuilding() => new DeleteBuildingWorkflow().Run();

        [CommandMethod("OL_RESET_SESSION")]
        public static void ResetSession() => new ResetSessionWorkflow().Run();

        [CommandMethod("OL_SHOW_3D")]
        public static void Show3D() => new Show3DViewWorkflow().Run();

        [CommandMethod("OL_CLEAR_3D")]
        public static void Clear3D() => new Clear3DViewWorkflow().Run();

        [CommandMethod("OL_EXPORT_JSON")]
        public static void ExportJson() => new ExportJsonWorkflow().Run();
    }
}
