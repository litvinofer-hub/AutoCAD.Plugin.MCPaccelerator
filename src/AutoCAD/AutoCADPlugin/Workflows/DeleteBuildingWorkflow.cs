using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates OL_DELETE_BUILDING: lets the user pick a building from the
    /// session, confirms, and removes it from <see cref="BuildingSession"/>
    /// (together with its <see cref="PrintBuildingRegistry"/> entry).
    ///
    /// The on-canvas entities that belonged to the building — working-area
    /// frames and labels, axial-system lines and bubbles, and printed floor
    /// plan polylines — are staged in <see cref="PendingCanvasCleanup"/>,
    /// then wiped by the <see cref="RefreshWorkflow"/> that runs at the end
    /// of <see cref="Run"/>. The staging + automatic refresh handshake means
    /// the canvas is always consistent with the session after the command
    /// returns, without the user having to hit OL_REFRESH themselves.
    ///
    /// The user's own floor plan entities (their walls/windows/doors) are
    /// NOT queued — those are the source drawing and must survive a
    /// building deletion.
    /// </summary>
    public class DeleteBuildingWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            Building building = BuildingContextPrompt.PickBuilding("delete");
            if (building == null) return;

            bool confirmed = ConfirmPrompt.Ask(
                $"Delete building '{building.Name}' and all its contents? This cannot be undone.",
                defaultYes: false);

            if (!confirmed)
            {
                _editor.WriteMessage("\nCancelled.");
                return;
            }

            // Stage every on-canvas entity the building owns before we lose
            // the references to it.
            StageWorkingAreaEntities(building);
            StageAxialSystemEntities(building);
            StagePrintedEntities(building);

            if (!BuildingSession.Remove(building))
            {
                _editor.WriteMessage($"\nBuilding '{building.Name}' was not found in the session.");
                return;
            }

            PrintBuildingRegistry.Remove(building);
            _editor.WriteMessage($"\nBuilding '{building.Name}' deleted. Refreshing...");

            // Auto-chain into OL_REFRESH so the staged cleanup runs and the
            // remaining buildings are re-ingested in one user action.
            new RefreshWorkflow().Run();
        }

        /// <summary>
        /// Queues the frame and label ObjectIds of every working area that
        /// belongs to <paramref name="building"/>.
        /// </summary>
        private static void StageWorkingAreaEntities(Building building)
        {
            var workingAreas = BuildingSession.GetWorkingAreas(building);
            if (workingAreas == null) return;

            foreach (var area in workingAreas.Areas)
            {
                PendingCanvasCleanup.Add(area.FrameId);
                PendingCanvasCleanup.Add(area.LabelId);
            }
        }

        /// <summary>
        /// Queues every axial-system entity (line, bubble, label) drawn on
        /// <see cref="McpLayers.Axes"/> inside any of the building's working
        /// areas. The axial system is a building-level construct, so erasing
        /// it is part of erasing the building.
        /// </summary>
        private static void StageAxialSystemEntities(Building building)
        {
            var workingAreas = BuildingSession.GetWorkingAreas(building);
            if (workingAreas == null) return;

            var db = AcadContext.Document.Database;
            using (var tx = db.TransactionManager.StartTransaction())
            {
                foreach (var area in workingAreas.Areas)
                {
                    foreach (var id in area.SelectedObjectIds)
                    {
                        if (!id.IsValid || id.IsErased) continue;
                        var entity = tx.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity != null && entity.Layer == McpLayers.Axes)
                            PendingCanvasCleanup.Add(id);
                    }
                }
                tx.Commit();
            }
        }

        private static void StagePrintedEntities(Building building)
        {
            var entry = PrintBuildingRegistry.TryGet(building);
            if (entry == null) return;

            PendingCanvasCleanup.AddRange(entry.DrawnEntityIds);
        }
    }
}
