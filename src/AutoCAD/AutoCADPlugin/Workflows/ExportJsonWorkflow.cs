using System.IO;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_EXPORT_JSON command. Picks a building from the
    /// session, serializes it to JSON, and writes it to the output/ folder
    /// at the solution root.
    /// </summary>
    public class ExportJsonWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("export to JSON");
            if (building == null) return;

            string json = BuildingJsonSerializer.ToJson(building);

            string outputDir = OutputPathHelper.GetOutputDir();
            Directory.CreateDirectory(outputDir);

            string fileName = $"{building.Name}_export.txt";
            string filePath = Path.Combine(outputDir, fileName);

            File.WriteAllText(filePath, json);

            _editor.WriteMessage($"\nExported '{building.Name}' to:\n{filePath}\n");
        }
    }
}
