using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_EXPORT_JSON command. Picks a building from the
    /// session and prints its full JSON representation to the AutoCAD
    /// text window (F2).
    /// </summary>
    public class ExportJsonWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("export to JSON");
            if (building == null) return;

            string json = BuildingJsonSerializer.ToJson(building);

            _editor.WriteMessage($"\n--- JSON for '{building.Name}' ---\n");
            _editor.WriteMessage(json);
            _editor.WriteMessage("\n--- end ---\n");
        }
    }
}
