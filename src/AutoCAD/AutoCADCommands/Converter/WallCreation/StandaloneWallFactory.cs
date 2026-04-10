using MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.WallCreation
{
    /// <summary>
    /// Creates walls for polylines that never joined any chain (no adjacent openings).
    /// Safe to use the long-axis heuristic here because standalone walls are always
    /// long enough that length &gt; thickness.
    /// </summary>
    public static class StandaloneWallFactory
    {
        /// <summary>
        /// Extracts the center line and thickness of a standalone wall polyline
        /// (using <see cref="Utils.GeometryModel.Polyline.TryLongAxisRect2D"/>) and
        /// adds one <see cref="Wall"/> to the <paramref name="building"/>.
        ///
        /// Silently skips polylines that don't have enough vertices for the long-axis
        /// heuristic (i.e. fewer than 4). Increments <c>result.WallsCreated</c> on success.
        /// </summary>
        /// <param name="building">The building that will own the new wall.</param>
        /// <param name="element">The tagged wall polyline (tag is ignored here — only walls reach this path).</param>
        /// <param name="botElevation">Bottom elevation (Z) of the wall.</param>
        /// <param name="topElevation">Top elevation (Z) of the wall.</param>
        /// <param name="result">Counters updated in-place: <c>WallsCreated</c>.</param>
        public static void Create(Building building, TaggedPolyline element,
            double botElevation, double topElevation, FloorPlanResult result)
        {
            if (!element.Polyline.TryLongAxisRect2D(out var start, out var end, out double thickness))
                return;

            building.AddWall(start.X, start.Y, end.X, end.Y,
                botElevation, topElevation, thickness);
            result.WallsCreated++;
        }
    }
}
