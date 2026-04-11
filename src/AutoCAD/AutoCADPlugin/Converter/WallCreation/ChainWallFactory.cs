using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.WallCreation
{
    /// <summary>
    /// Collapses one chain (walls + openings) into one merged <see cref="Wall"/>
    /// on the building, then projects each opening onto that merged wall as a
    /// window or door.
    ///
    /// Assumptions about the input <see cref="Chain"/>:
    /// - <c>chain.Direction</c> is the chain's long axis, taken from an opening's
    ///   <see cref="Rect.Direction2D"/> — reliable because openings always have
    ///   length &gt; thickness.
    /// - Every opening in the chain has 2 flanking walls already in the chain.
    /// - Walls and openings sit on the same row (same perpendicular position),
    ///   so averaging the perpendicular coordinate of all vertices yields the
    ///   row's centerline offset.
    /// - The row's wall thickness comes from the openings, not the walls,
    ///   because a "stub" wall (length &lt; thickness) may sit between two
    ///   openings and its <see cref="Rect.Thickness2D"/> would be its tiny side,
    ///   not the architectural wall thickness. Openings are always normal
    ///   (length &gt; thickness) so their <see cref="Rect.Thickness2D"/> is reliable.
    /// </summary>
    public static class ChainWallFactory
    {
        private readonly struct ChainBounds(double minT, double maxT, double avgPerp, double rowThickness, Vec2 perp)
        {
            public double MinT { get; } = minT;
            public double MaxT { get; } = maxT;
            public double AvgPerp { get; } = avgPerp;
            public double RowThickness { get; } = rowThickness;
            public Vec2 Perp { get; } = perp;
        }

        /// <summary>
        /// Merges <paramref name="chain"/> into one wall and adds each of its openings.
        /// Increments <c>result.WallsCreated</c> once and the matching opening counter
        /// (<c>WindowsCreated</c> / <c>DoorsCreated</c> / <c>OpeningsSkipped</c>) per opening.
        /// </summary>
        public static void Create(Building building, Chain chain, Story story, FloorPlanResult result)
        {
            var bounds = ComputeBounds(chain, building.Units.DefaultWallThickness);
            var wall = CreateMergedWall(building, chain.Direction, bounds, story);
            result.WallsCreated++;

            double botElevation = story.BotLevel.Elevation;
            foreach (var opening in chain.Openings)
                TryAddOpening(building, wall, opening, chain.Direction, bounds, botElevation, result);
        }

        /// <summary>
        /// Projects every vertex of every chain element onto the chain's (dir, perp)
        /// frame and returns:
        /// - <c>MinT</c>/<c>MaxT</c>: span along the chain (the merged wall's endpoints).
        /// - <c>AvgPerp</c>: average perpendicular coordinate (the merged wall's row offset).
        /// - <c>RowThickness</c>: average <see cref="Rect.Thickness2D"/> of the openings.
        ///   Falls back to walls' Thickness2D only if the chain has no openings (which
        ///   <see cref="ChainBuilding.ChainBuilder"/> never produces), and finally to
        ///   <paramref name="defaultWallThickness"/>.
        /// </summary>
        private static ChainBounds ComputeBounds(Chain chain, double defaultWallThickness)
        {
            var dir = chain.Direction;
            var perp = Vec2Math.Perpendicular(dir);

            double minT = double.MaxValue, maxT = double.MinValue;
            double perpSum = 0;
            int vertexCount = 0;

            foreach (var elem in chain.Walls.Concat(chain.Openings))
            {
                foreach (var p in elem.Rect.Points)
                {
                    double t = p.X * dir.X + p.Y * dir.Y;
                    perpSum += p.X * perp.X + p.Y * perp.Y;
                    vertexCount++;
                    if (t < minT) minT = t;
                    if (t > maxT) maxT = t;
                }
            }

            double avgPerp = vertexCount > 0 ? perpSum / vertexCount : 0;

            double rowThickness;
            if (chain.Openings.Count > 0)
                rowThickness = chain.Openings.Average(o => o.Rect.Thickness2D);
            else if (chain.Walls.Count > 0)
                rowThickness = chain.Walls.Average(w => w.Rect.Thickness2D);
            else
                rowThickness = defaultWallThickness;

            return new ChainBounds(minT, maxT, avgPerp, rowThickness, perp);
        }

        /// <summary>
        /// Builds the merged wall's two endpoints from the chain's (MinT, MaxT) span and
        /// (AvgPerp) row offset, then adds the wall to the building under <paramref name="story"/>.
        /// </summary>
        private static Wall CreateMergedWall(Building building, Vec2 dir, ChainBounds bounds, Story story)
        {
            var start = AxisFrameToWorld(bounds.MinT, bounds.AvgPerp, dir, bounds.Perp);
            var end = AxisFrameToWorld(bounds.MaxT, bounds.AvgPerp, dir, bounds.Perp);
            return building.AddWall(start.X, start.Y, end.X, end.Y, story, bounds.RowThickness);
        }

        /// <summary>
        /// Projects one opening rectangle onto the chain frame to get its 2D endpoints,
        /// resolves its sill Z and height from the unit system (a 2D floor plan carries
        /// no vertical info), and adds it to <paramref name="wall"/>. Counts on
        /// <paramref name="result"/> are updated. Failures are caught and counted as
        /// <c>OpeningsSkipped</c>.
        /// </summary>
        private static void TryAddOpening(Building building, Wall wall, TaggedRect element,
            Vec2 dir, ChainBounds bounds, double botElevation, FloorPlanResult result)
        {
            double minT = double.MaxValue, maxT = double.MinValue;
            foreach (var p in element.Rect.Points)
            {
                double t = p.X * dir.X + p.Y * dir.Y;
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }

            var openStart = AxisFrameToWorld(minT, bounds.AvgPerp, dir, bounds.Perp);
            var openEnd = AxisFrameToWorld(maxT, bounds.AvgPerp, dir, bounds.Perp);

            var (sillZ, height) = ResolveOpeningZAndHeight(building, element.Type, botElevation);

            try
            {
                switch (element.Type)
                {
                    case ElementType.Window:
                        building.AddWindow(wall, openStart.X, openStart.Y, openEnd.X, openEnd.Y, sillZ, height);
                        result.WindowsCreated++;
                        break;
                    case ElementType.Door:
                        building.AddDoor(wall, openStart.X, openStart.Y, openEnd.X, openEnd.Y, sillZ, height);
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
        /// Pulls the sill height and total opening height from the building's
        /// <see cref="UnitSystem"/> defaults for the given opening type. Floor plans
        /// don't carry vertical information, so these always come from defaults.
        /// </summary>
        private static (double sillZ, double height) ResolveOpeningZAndHeight(
            Building building, ElementType type, double botElevation)
        {
            var units = building.Units;
            return type switch
            {
                ElementType.Window => (botElevation + units.DefaultWindowSillHeight, units.DefaultWindowHeight),
                ElementType.Door   => (botElevation + units.DefaultDoorSillHeight,   units.DefaultDoorHeight),
                _ => (botElevation, 0),
            };
        }

        /// <summary>
        /// Converts a point expressed in the (dir, perp) chain frame back into world 2D.
        /// </summary>
        private static Vec2 AxisFrameToWorld(double t, double p, Vec2 dir, Vec2 perp)
            => new(t * dir.X + p * perp.X, t * dir.Y + p * perp.Y);
    }
}
