using MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.WallCreation
{
    /// <summary>
    /// Collapses a chain (walls + openings) into one merged <see cref="Wall"/> on the
    /// building, then projects each opening inside the chain onto the merged wall and
    /// registers it as a window or door.
    /// </summary>
    public static class ChainWallFactory
    {
        /// <summary>
        /// Axis-aligned bounds of a chain, computed in the chain's local (dir, perp) frame.
        /// </summary>
        private readonly struct ChainBounds(double minT, double maxT, double avgPerp, double avgWallThickness, Vec2 perp)
        {
            public double MinT { get; } = minT;
            public double MaxT { get; } = maxT;
            public double AvgPerp { get; } = avgPerp;
            public double AvgWallThickness { get; } = avgWallThickness;
            public Vec2 Perp { get; } = perp;
        }

        /// <summary>
        /// Merges all walls + openings in a single chain into one <see cref="Wall"/> on the
        /// <paramref name="building"/> and attaches each opening as a window or door.
        ///
        /// Pipeline:
        /// 1. <see cref="ComputeBounds"/> — projects every vertex onto the chain's (dir, perp)
        ///    frame to get start/end along the chain and an average perpendicular offset.
        /// 2. <see cref="CreateMergedWall"/> — converts those bounds back to world 2D and
        ///    creates one wall spanning the whole chain.
        /// 3. For each opening in the chain, <see cref="TryAddOpening"/> projects it onto
        ///    the same frame and registers it on the merged wall.
        /// </summary>
        /// <param name="building">The building that will own the new wall and openings.</param>
        /// <param name="chain">The chain of alternating wall/opening polylines to merge.</param>
        /// <param name="botElevation">Bottom elevation (Z) of the merged wall.</param>
        /// <param name="topElevation">Top elevation (Z) of the merged wall.</param>
        /// <param name="result">Counters updated in-place: WallsCreated, WindowsCreated,
        /// DoorsCreated, OpeningsSkipped.</param>
        public static void Create(Building building, Chain chain, double botElevation, double topElevation, FloorPlanResult result)
        {
            var bounds = ComputeBounds(chain, building.Units.DefaultWallThickness);
            var wall = CreateMergedWall(building, chain.Direction, bounds, botElevation, topElevation);
            result.WallsCreated++;

            foreach (var entry in chain.Elements)
            {
                if (!entry.IsOpening) continue;
                TryAddOpening(building, wall, entry.Element, chain.Direction, bounds, botElevation, result);
            }
        }

        /// <summary>
        /// Projects every vertex of every element in the chain onto the chain's axis,
        /// computing min/max along the axis and an average perpendicular offset. Also
        /// averages per-wall thicknesses (or falls back to the unit system default).
        /// </summary>
        private static ChainBounds ComputeBounds(Chain chain, double defaultWallThickness)
        {
            var dir = chain.Direction;
            var perp = Vec2Math.Perpendicular(dir);

            double minT = double.MaxValue, maxT = double.MinValue;
            double perpSum = 0;
            int vertexCount = 0;
            double totalThickness = 0;
            int wallCount = 0;

            foreach (var entry in chain.Elements)
            {
                var poly = entry.Element.Polyline;
                foreach (var p in poly.Points)
                {
                    var v = new Vec2(p.X, p.Y);
                    double t = Vec2Math.Dot(v, dir);
                    perpSum += Vec2Math.Dot(v, perp);
                    vertexCount++;

                    if (t < minT) minT = t;
                    if (t > maxT) maxT = t;
                }

                if (!entry.IsOpening)
                {
                    totalThickness += poly.Extent2D(perp);
                    wallCount++;
                }
            }

            double avgPerp = perpSum / vertexCount;
            double thickness = wallCount > 0 ? totalThickness / wallCount : defaultWallThickness;

            return new ChainBounds(minT, maxT, avgPerp, thickness, perp);
        }

        /// <summary>
        /// Converts chain bounds back into 2D endpoints and adds the merged wall to the building.
        /// </summary>
        private static Wall CreateMergedWall(Building building, Vec2 dir, ChainBounds bounds,
            double botElevation, double topElevation)
        {
            var start = AxisFrameToWorld(bounds.MinT, bounds.AvgPerp, dir, bounds.Perp);
            var end = AxisFrameToWorld(bounds.MaxT, bounds.AvgPerp, dir, bounds.Perp);

            return building.AddWall(start.X, start.Y, end.X, end.Y,
                botElevation, topElevation, bounds.AvgWallThickness);
        }

        /// <summary>
        /// Projects one opening element onto the chain axis, converts back to 2D,
        /// and adds it to the given <paramref name="wall"/> as a window or door.
        /// Increments the corresponding counter on <paramref name="result"/>.
        /// </summary>
        private static void TryAddOpening(Building building, Wall wall, TaggedPolyline element,
            Vec2 dir, ChainBounds bounds, double botElevation, FloorPlanResult result)
        {
            var poly = element.Polyline;
            double minT = double.MaxValue, maxT = double.MinValue;
            foreach (var p in poly.Points)
            {
                double t = p.X * dir.X + p.Y * dir.Y;
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }

            double openingHeight = poly.Extent2D(bounds.Perp);

            var openStart = AxisFrameToWorld(minT, bounds.AvgPerp, dir, bounds.Perp);
            var openEnd = AxisFrameToWorld(maxT, bounds.AvgPerp, dir, bounds.Perp);

            try
            {
                switch (element.Type)
                {
                    case ElementType.Window:
                        wall.AddWindow(building, openStart.X, openStart.Y, openEnd.X, openEnd.Y,
                            botElevation, openingHeight);
                        result.WindowsCreated++;
                        break;
                    case ElementType.Door:
                        wall.AddDoor(building, openStart.X, openStart.Y, openEnd.X, openEnd.Y,
                            botElevation, openingHeight);
                        result.DoorsCreated++;
                        break;
                }
            }
            catch
            {
                result.OpeningsSkipped++;
            }
        }

        /// <summary>
        /// Converts a point expressed in the (dir, perp) frame back into world 2D coordinates.
        /// </summary>
        private static Vec2 AxisFrameToWorld(double t, double p, Vec2 dir, Vec2 perp)
            => new(t * dir.X + p * perp.X, t * dir.Y + p * perp.Y);
    }
}
