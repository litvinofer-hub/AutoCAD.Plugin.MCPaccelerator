using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel.Debugging
{
    /// <summary>
    /// Converts domain objects into <see cref="DrawableLayer"/>s for use with
    /// <see cref="FloorPlanDrawer"/>. Each factory method returns a layer
    /// for one story.
    /// </summary>
    public static class DrawableLayerFactory
    {
        /// <summary>
        /// All walls of the given story, drawn as thick rectangles.
        /// </summary>
        public static DrawableLayer Walls(Building building, Story story)
        {
            var elements = building.Walls
                .Where(w => w.StoryId == story.Id)
                .Select(w => new DrawableElement(w.BotLine, w.Thickness))
                .ToList();
            return new DrawableLayer("Walls", elements);
        }

        /// <summary>
        /// All windows across all walls of the given story.
        /// Drawn using the parent wall's thickness.
        /// </summary>
        public static DrawableLayer Windows(Building building, Story story)
        {
            var elements = new List<DrawableElement>();
            foreach (var wall in building.Walls.Where(w => w.StoryId == story.Id))
            {
                foreach (var opening in wall.Openings)
                {
                    if (opening is Window)
                        elements.Add(new DrawableElement(opening.Line, wall.Thickness));
                }
            }
            return new DrawableLayer("Windows", elements);
        }

        /// <summary>
        /// All doors across all walls of the given story.
        /// Drawn using the parent wall's thickness.
        /// </summary>
        public static DrawableLayer Doors(Building building, Story story)
        {
            var elements = new List<DrawableElement>();
            foreach (var wall in building.Walls.Where(w => w.StoryId == story.Id))
            {
                foreach (var opening in wall.Openings)
                {
                    if (opening is Door)
                        elements.Add(new DrawableElement(opening.Line, wall.Thickness));
                }
            }
            return new DrawableLayer("Doors", elements);
        }

        /// <summary>
        /// All axial lines for the given story, drawn as thin lines (no thickness).
        /// The label shows the axis symbol (A, B, 1, etc.).
        /// </summary>
        public static DrawableLayer AxialLines(Building building, Story story)
        {
            var elements = new List<DrawableElement>();
            if (story.AxialSystem == null)
                return new DrawableLayer("AxialLines", elements);

            foreach (var direction in story.AxialSystem.Directions)
            {
                foreach (var axialLine in direction.AxialLines)
                {
                    elements.Add(new DrawableElement(
                        axialLine.Line, 0, axialLine.Symbol));
                }
            }
            return new DrawableLayer("AxialLines", elements);
        }

        /// <summary>
        /// Axial lines for a specific direction only.
        /// </summary>
        public static DrawableLayer AxialLines(Building building, Story story, Vec2 direction)
        {
            var elements = new List<DrawableElement>();
            if (story.AxialSystem == null)
                return new DrawableLayer("AxialLines", elements);

            var dir = story.AxialSystem.FindDirection(direction);
            if (dir == null)
                return new DrawableLayer("AxialLines", elements);

            foreach (var axialLine in dir.AxialLines)
            {
                elements.Add(new DrawableElement(
                    axialLine.Line, 0, axialLine.Symbol));
            }

            string dirLabel = $"AxialLines ({direction.X:0.#},{direction.Y:0.#})";
            return new DrawableLayer(dirLabel, elements);
        }
    }
}
