using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.WallCreation
{
    /// <summary>
    /// Collapses one <see cref="Chain"/> (walls + openings) into one 2D
    /// <see cref="ConvertedWall"/> with its openings. Pure — no
    /// <see cref="MCPAccelerator.Domain.BuildingModel.Building"/> or
    /// <see cref="MCPAccelerator.Domain.BuildingModel.Story"/> dependency.
    /// The caller feeds the result into <c>FloorPlanConverter.Apply</c>.
    ///
    /// Assumptions about the input <see cref="Chain"/>:
    /// - <c>chain.Direction</c> is the chain's long axis, taken from an opening's
    ///   <see cref="Rect.Direction2D"/> (reliable because openings always have
    ///   length &gt; thickness).
    /// - Every opening in the chain has 2 flanking walls already in the chain,
    ///   so <c>chain.Openings</c> is non-empty.
    /// - Walls and openings sit on the same row (same perpendicular position),
    ///   so averaging the perpendicular coordinate of all vertices yields the
    ///   row's centerline offset.
    /// - Row thickness is taken from the openings (always normal, so
    ///   <see cref="Rect.Thickness2D"/> is reliable), not from the walls — a
    ///   "stub" wall's <see cref="Rect.Thickness2D"/> may be its tiny side, not
    ///   the architectural wall thickness.
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
        /// Merges <paramref name="chain"/> into a single <see cref="ConvertedWall"/>
        /// whose <see cref="ConvertedWall.Openings"/> list contains one entry per opening
        /// in the chain, projected onto the merged wall's centerline.
        /// </summary>
        public static ConvertedWall Create(Chain chain)
        {
            var bounds = ComputeBounds(chain);
            var start = AxisFrameToWorld(bounds.MinT, bounds.AvgPerp, chain.Direction, bounds.Perp);
            var end   = AxisFrameToWorld(bounds.MaxT, bounds.AvgPerp, chain.Direction, bounds.Perp);

            var converted = new ConvertedWall(start.X, start.Y, end.X, end.Y, bounds.RowThickness);

            foreach (var opening in chain.Openings)
            {
                var (os, oe) = ProjectOpeningOntoCenterline(opening.Rect, chain.Direction, bounds);
                converted.Openings.Add(new ConvertedOpening(opening.Type, os.X, os.Y, oe.X, oe.Y));
            }

            return converted;
        }

        /// <summary>
        /// Projects every vertex of every chain element onto the chain's (dir, perp)
        /// frame and returns:
        /// - <c>MinT</c>/<c>MaxT</c>: span along the chain (merged wall's endpoints).
        /// - <c>AvgPerp</c>: average perpendicular coordinate (merged wall's row offset).
        /// - <c>RowThickness</c>: average <see cref="Rect.Thickness2D"/> of the openings.
        /// </summary>
        private static ChainBounds ComputeBounds(Chain chain)
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

            double avgPerp = perpSum / vertexCount;
            double rowThickness = chain.Openings.Average(o => o.Rect.Thickness2D);

            return new ChainBounds(minT, maxT, avgPerp, rowThickness, perp);
        }

        /// <summary>
        /// Projects one opening rectangle onto the chain's centerline and returns
        /// its 2D start/end points in world coordinates.
        /// </summary>
        private static (Vec2 start, Vec2 end) ProjectOpeningOntoCenterline(
            Rect opening, Vec2 dir, ChainBounds bounds)
        {
            double minT = double.MaxValue, maxT = double.MinValue;
            foreach (var p in opening.Points)
            {
                double t = p.X * dir.X + p.Y * dir.Y;
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }
            var start = AxisFrameToWorld(minT, bounds.AvgPerp, dir, bounds.Perp);
            var end   = AxisFrameToWorld(maxT, bounds.AvgPerp, dir, bounds.Perp);
            return (start, end);
        }

        /// <summary>
        /// Converts a point expressed in the (dir, perp) chain frame back into world 2D.
        /// </summary>
        private static Vec2 AxisFrameToWorld(double t, double p, Vec2 dir, Vec2 perp)
            => new(t * dir.X + p * perp.X, t * dir.Y + p * perp.Y);
    }
}
