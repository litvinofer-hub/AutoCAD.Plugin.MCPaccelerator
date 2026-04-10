using MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.WallCreation
{
    /// <summary>
    /// Creates walls for rectangles that never joined any chain (no adjacent openings).
    /// Safe to use the long-axis center line directly here because standalone walls
    /// are always long enough that length &gt; thickness.
    /// </summary>
    public static class StandaloneWallFactory
    {
        /// <summary>
        /// Reads the cached center line and thickness of a standalone wall <see cref="Rect"/>
        /// (<see cref="Rect.CenterLineStart2D"/>, <see cref="Rect.CenterLineEnd2D"/>,
        /// <see cref="Rect.Thickness2D"/>) and adds one <see cref="Wall"/> to the
        /// <paramref name="building"/>. Increments <c>result.WallsCreated</c>.
        /// </summary>
        /// <param name="building">The building that will own the new wall.</param>
        /// <param name="element">The tagged wall rectangle (tag is ignored here — only walls reach this path).</param>
        /// <param name="botElevation">Bottom elevation (Z) of the wall.</param>
        /// <param name="topElevation">Top elevation (Z) of the wall.</param>
        /// <param name="result">Counters updated in-place: <c>WallsCreated</c>.</param>
        public static void Create(Building building, TaggedRect element,
            double botElevation, double topElevation, FloorPlanResult result)
        {
            var rect = element.Rect;
            building.AddWall(
                rect.CenterLineStart2D.X, rect.CenterLineStart2D.Y,
                rect.CenterLineEnd2D.X, rect.CenterLineEnd2D.Y,
                botElevation, topElevation, rect.Thickness2D);
            result.WallsCreated++;
        }
    }
}
