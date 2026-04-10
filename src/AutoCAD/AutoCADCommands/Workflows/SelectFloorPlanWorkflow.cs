using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADCommands.Converter;
using MCPAccelerator.AutoCAD.AutoCADCommands.Prompts;
using MCPAccelerator.AutoCAD.AutoCADCommands.Selection;
using MCPAccelerator.AutoCAD.AutoCADCommands.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Workflows
{
    /// <summary>
    /// Orchestrates the OL_SELECT_FLOOR_PLAN command:
    /// pick building+story → user selects polylines → classify by layer →
    /// convert to domain objects → report.
    /// </summary>
    public class SelectFloorPlanWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            var context = BuildingContextPrompt.PickBuildingAndStory("floor plan elements");
            if (context == null) return;

            var selection = FloorPlanSelection.SelectAndClassify();
            if (selection == null) return;

            _editor.WriteMessage(
                $"\nFound: {selection.Walls.Count} wall(s), {selection.Windows.Count} window(s), {selection.Doors.Count} door(s)");

            if (selection.Total == 0)
            {
                _editor.WriteMessage("\nNo matching polylines found in selection.");
                return;
            }

            var result = FloorPlanConverter.Convert(
                context.Value.building, context.Value.story,
                selection.Walls, selection.Windows, selection.Doors);

            ReportResult(context.Value.building, context.Value.story, result);
        }

        private void ReportResult(Building building, Story story, FloorPlanResult result)
        {
            _editor.WriteMessage($"\n\nAdded to '{story.Name}' in '{building.Name}':");
            _editor.WriteMessage($"\n  Walls:   {result.WallsCreated}");
            _editor.WriteMessage($"\n  Windows: {result.WindowsCreated}");
            _editor.WriteMessage($"\n  Doors:   {result.DoorsCreated}");
            if (result.OpeningsSkipped > 0)
                _editor.WriteMessage($"\n  Skipped: {result.OpeningsSkipped} opening(s) (validation failed)");
        }
    }
}
