using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Selection;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_SET_FLOOR_PLAN_AREA command:
    ///
    /// 1. Pick building + story.
    /// 2. User selects any entities (no filtering — every entity type accepted).
    /// 3. Save ALL selected ObjectIds into a <see cref="FloorPlanWorkingArea"/>,
    ///    draw a bounding-box frame + label around every selected entity, and
    ///    register the working area in <see cref="BuildingSession"/>.
    /// 4. Filter the saved selection down to closed polylines only.
    /// 5. Classify closed polylines into walls / windows / doors by layer name.
    /// 6. Convert classified polylines to domain Building elements.
    /// 7. Map each new domain element ID back to the source AutoCAD ObjectIds.
    /// 8. Report results.
    /// </summary>
    public class SetFloorPlanAreaWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        public void Run()
        {
            // --- 1. Pick building + story ---
            var context = BuildingContextPrompt.PickBuildingAndStory("floor plan elements");
            if (context == null) return;

            var building = context.Value.building;
            var story = context.Value.story;

            // --- 2. Raw selection — any entity type, no filtering ---
            var raw = FloorPlanSelection.Select();
            if (raw == null) return;

            if (raw.ObjectIds.Count == 0)
            {
                _editor.WriteMessage("\nNo elements found in selection.");
                return;
            }

            _editor.WriteMessage($"\nSelected {raw.ObjectIds.Count} element(s).");

            // --- 3. Save to FloorPlanWorkingArea + draw bbox + register in session ---
            var (frameId, labelId) = WorkingAreaFrameHelper.DrawFrame(
                raw.ObjectIds, building.Name, story.Name);

            var workingArea = new FloorPlanWorkingArea(
                building.Id, story.Id,
                building.Name, story.Name,
                raw.ObjectIds,
                frameId, labelId);

            BuildingSession.GetWorkingAreas(building).Add(workingArea);

            // --- 4. Filter to closed polylines ---
            var closedPolylines = FloorPlanSelection.FilterClosedPolylines(raw);

            if (closedPolylines.Count == 0)
            {
                _editor.WriteMessage("\nNo closed polylines found in selection.");
                return;
            }

            // --- 5. Classify into walls / windows / doors ---
            var classified = FloorPlanSelection.Classify(closedPolylines);

            _editor.WriteMessage(
                $"\nClassified: {classified.Walls.Count} wall(s), " +
                $"{classified.Windows.Count} window(s), " +
                $"{classified.Doors.Count} door(s)");

            if (classified.Total == 0)
            {
                _editor.WriteMessage("\nNo wall/window/door polylines found after classification.");
                return;
            }

            // --- 6. Convert to domain Building elements ---
            var converted = FloorPlanConverter.Convert(
                classified.Walls, classified.Windows, classified.Doors,
                building.Units.LengthEpsilon);

            var result = FloorPlanConverter.Apply(building, story, converted);

            // --- 7. Map domain element IDs to source AutoCAD ObjectIds ---
            MapDomainElementIds(building, story, raw, workingArea);

            // --- 8. Report ---
            ReportResult(building, story, result);
        }

        private static void MapDomainElementIds(
            Building building, Story story,
            RawSelection raw, FloorPlanWorkingArea workingArea)
        {
            var storyWalls = building.Walls
                .Where(w => w.StoryId == story.Id)
                .ToList();

            foreach (var wall in storyWalls)
            {
                workingArea.MapDomainElement(wall.Id, raw.ObjectIds);

                foreach (var opening in wall.Openings)
                    workingArea.MapDomainElement(opening.Id, raw.ObjectIds);
            }
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
