using System;
using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Selection;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter
{
    /// <summary>
    /// Re-converts a single story's floor plan from its <see cref="FloorPlanWorkingArea"/>
    /// back into domain <see cref="Wall"/>s / openings.
    ///
    /// Called whenever something changes the canvas→building transform for the
    /// story — specifically when <see cref="Story.CanvasOrigin"/> is set (axial
    /// system created) or updated (story registered against an existing axial
    /// system from another story's pick).
    ///
    /// Preserves the same filter → classify → convert → apply pipeline that
    /// <see cref="Workflows.RefreshWorkflow"/> uses, but scoped to one story.
    /// </summary>
    public static class StoryReingestion
    {
        /// <summary>
        /// Removes <paramref name="story"/>'s existing walls from
        /// <paramref name="building"/>, re-converts the polylines tracked in
        /// <paramref name="area"/>, and re-maps the resulting domain elements
        /// back to their source AutoCAD ObjectIds.
        /// </summary>
        public static FloorPlanResult Reingest(
            Building building, Story story, FloorPlanWorkingArea area)
        {
            // 1. Remove old walls for this story
            var wallsToRemove = building.Walls.Where(w => w.StoryId == story.Id).ToList();
            foreach (var wall in wallsToRemove)
                building.RemoveWall(wall);

            // Clear the domain map but keep axial-line entries — those belong
            // to the building-wide axial system and their drawn entities are
            // still on the canvas.
            var keepIds = CollectAxialLineIds(building);
            area.ClearDomainMapExcept(keepIds);

            // 2. Build a RawSelection from the tracked ObjectIds
            var raw = new RawSelection();
            foreach (var id in area.SelectedObjectIds)
                raw.ObjectIds.Add(id);

            // 3. Filter → classify → convert → apply
            var closedPolylines = FloorPlanSelection.FilterClosedPolylines(raw);
            if (closedPolylines.Count == 0)
                return new FloorPlanResult();

            var classified = FloorPlanSelection.Classify(closedPolylines);
            if (classified.Total == 0)
                return new FloorPlanResult();

            var converted = FloorPlanConverter.Convert(
                classified.Walls, classified.Windows, classified.Doors,
                building.Units.LengthEpsilon);

            var result = FloorPlanConverter.Apply(building, story, converted);

            // 4. Re-map domain element IDs
            var storyWalls = building.Walls.Where(w => w.StoryId == story.Id).ToList();
            foreach (var wall in storyWalls)
            {
                area.MapDomainElement(wall.Id, raw.ObjectIds);
                foreach (var opening in wall.Openings)
                    area.MapDomainElement(opening.Id, raw.ObjectIds);
            }

            return result;
        }

        private static System.Collections.Generic.IEnumerable<Guid> CollectAxialLineIds(Building building)
        {
            if (building.AxialSystem == null) yield break;
            foreach (var direction in building.AxialSystem.Directions)
                foreach (var line in direction.AxialLines)
                    yield return line.Id;
        }
    }
}
