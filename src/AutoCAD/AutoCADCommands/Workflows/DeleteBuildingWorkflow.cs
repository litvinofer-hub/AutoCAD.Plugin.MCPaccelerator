using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADCommands.Prompts;
using MCPAccelerator.AutoCAD.AutoCADCommands.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Workflows
{
    /// <summary>
    /// Orchestrates OL_DELETE_BUILDING: lets the user pick a building from
    /// the session, confirms, and removes it.
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

            if (BuildingSession.Remove(building))
                _editor.WriteMessage($"\nBuilding '{building.Name}' deleted.");
            else
                _editor.WriteMessage($"\nBuilding '{building.Name}' was not found in the session.");
        }
    }
}
