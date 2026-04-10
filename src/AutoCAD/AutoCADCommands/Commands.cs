using Autodesk.AutoCAD.Runtime;
using MCPAccelerator.AutoCAD.AutoCADCommands.Workflows;

// NETLOAD
// C:\Users\Ofer\Desktop\MCPaccelerator\src\AutoCAD\AutoCADCommands\bin\Debug\net10.0\MCPAccelerator.AutoCAD.AutoCADCommands.dll
// AutoCAD Text Window (F2)

namespace MCPAccelerator.AutoCAD.AutoCADCommands
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

        [CommandMethod("OL_DELETE_BUILDING")]
        public static void DeleteBuilding() => new DeleteBuildingWorkflow().Run();

        [CommandMethod("OL_RESET_SESSION")]
        public static void ResetSession() => new ResetSessionWorkflow().Run();
    }
}
