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
    /// frames and labels, and printed floor plan polylines — are NOT erased
    /// here. Their ObjectIds are staged in <see cref="PendingCanvasCleanup"/>
    /// and swept by the next OL_REFRESH, so the user only pays the canvas-edit
    /// cost once, during the refresh they were going to run anyway.
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
            StagePrintedEntities(building);

            if (BuildingSession.Remove(building))
            {
                PrintBuildingRegistry.Remove(building);
                _editor.WriteMessage(
                    $"\nBuilding '{building.Name}' deleted. Run OL_REFRESH to clean up the canvas.");
            }
            else
                _editor.WriteMessage($"\nBuilding '{building.Name}' was not found in the session.");
        }

        /// <summary>
        /// Queues the frame and label ObjectIds of every working area that
        /// belongs to <paramref name="building"/>. <see cref="FloorPlanWorkingArea.SelectedObjectIds"/>
        /// is deliberately NOT queued — those are the user's own floor plan
        /// entities and must survive a building deletion.
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

        private static void StagePrintedEntities(Building building)
        {
            var entry = PrintBuildingRegistry.TryGet(building);
            if (entry == null) return;

            PendingCanvasCleanup.AddRange(entry.DrawnEntityIds);
        }
    }
}
