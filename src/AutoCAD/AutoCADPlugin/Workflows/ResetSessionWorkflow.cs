using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates OL_RESET_SESSION: confirms with the user and then
    /// clears every building (and its stories / walls / openings) from the session.
    /// </summary>
    public class ResetSessionWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            int count = BuildingSession.Buildings.Count;

            if (count == 0)
            {
                _editor.WriteMessage("\nSession is already empty.");
                return;
            }

            bool confirmed = ConfirmPrompt.Ask(
                $"Clear all {count} building(s) from the session? This cannot be undone.",
                defaultYes: false);

            if (!confirmed)
            {
                _editor.WriteMessage("\nCancelled.");
                return;
            }

            BuildingSession.Clear();
            PrintBuildingRegistry.Clear();
            PrintGraphsRegistry.Clear();
            PendingCanvasCleanup.Clear();
            _editor.WriteMessage($"\nSession cleared ({count} building(s) removed).");
        }
    }
}
