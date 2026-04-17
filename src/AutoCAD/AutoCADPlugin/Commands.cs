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
    ///
    /// Commands are grouped into three categories:
    ///
    /// <b>Preparation</b> — one-time setup commands that define the building
    /// model (create building, select floor plan elements, create/clear axes).
    ///
    /// <b>Refresh</b> — re-scans every working area boundary, re-collects
    /// entities, and re-converts to the domain model. Run after any drawing edit.
    ///
    /// <b>On-demand</b> — utilities that query or export the current state
    /// (3D view, reset).
    ///
    /// <b>On-demand, triggers OL_REFRESH</b> — commands the user invokes on
    /// demand that finish by running OL_REFRESH themselves, so the canvas
    /// ends up consistent with the session after a single command
    /// (e.g. delete building).
    ///
    /// <b>On-demand, replayed by OL_REFRESH</b> — commands the user invokes
    /// on demand whose last invocation is remembered and re-run every time
    /// OL_REFRESH runs, so the visualization stays in sync with the
    /// re-ingested model (e.g. print building).
    ///
    /// <b>Debugging</b> — export and visualize the domain model for diagnostics
    /// (JSON export, SVG drawing).
    /// </summary>
    public class Commands
    {
        // =================================================================
        // Preparation — define the building model
        // =================================================================

        [CommandMethod("OL_CREATE_BUILDING")]
        public static void CreateBuilding() => new CreateBuildingWorkflow().Run();

        [CommandMethod("OL_CREATE_FLOOR_PLAN_AREA")]
        public static void CreateFloorPlanArea() => new SetFloorPlanAreaWorkflow().Run();

        [CommandMethod("OL_CREATE_AXIAL_SYSTEM")]
        public static void CreateAxialSystem() => new CreateAxialSystemWorkflow().Run();

        [CommandMethod("OL_REGISTER_STORY_WITH_AXIAL_SYSTEM")]
        public static void RegisterStoryWithAxialSystem()
            => new RegisterStoryWithAxialSystemWorkflow().Run();

        [CommandMethod("OL_DELETE_AXIAL_LINE")]
        public static void DeleteAxialLine() => new DeleteAxialLineWorkflow().Run();

        [CommandMethod("OL_CLEAR_AXIAL_SYSTEM")]
        public static void ClearAxialSystem() => new ClearAxialSystemWorkflow().Run();

        // =================================================================
        // Refresh — re-scan boundaries and rebuild domain model
        // =================================================================

        [CommandMethod("OL_REFRESH")]
        public static void Refresh() => new RefreshWorkflow().Run();

        // =================================================================
        // On-demand — query, visualize, export
        // =================================================================

        [CommandMethod("OL_RESET_SESSION")]
        public static void ResetSession() => new ResetSessionWorkflow().Run();

        [CommandMethod("OL_SHOW_3D")]
        public static void Show3D() => new Show3DViewWorkflow().Run();

        [CommandMethod("OL_CLEAR_3D")]
        public static void Clear3D() => new Clear3DViewWorkflow().Run();

        // =================================================================
        // On-demand, triggers OL_REFRESH — each command here runs OL_REFRESH
        // itself at the end of its flow, so the canvas is consistent with the
        // session after a single user action.
        // =================================================================

        [CommandMethod("OL_DELETE_BUILDING")]
        public static void DeleteBuilding() => new DeleteBuildingWorkflow().Run();

        // =================================================================
        // On-demand, replayed by OL_REFRESH — the user invokes these directly,
        // but OL_REFRESH remembers the last invocation and re-runs it every
        // time, so the visualization stays in sync with the re-ingested model.
        // =================================================================

        [CommandMethod("OL_PRINT_BUILDING")]
        public static void PrintBuilding() => new PrintBuildingWorkflow().Run();

        // =================================================================
        // Debugging — export and visualize the domain model
        // =================================================================

        [CommandMethod("OL_EXPORT_JSON")]
        public static void ExportJson() => new ExportJsonWorkflow().Run();

        [CommandMethod("OL_DEBUG_DRAW")]
        public static void DebugDraw() => new DebugDrawWorkflow().Run();
    }
}
