using System;
using System.Collections.Generic;
using MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.ChainBuilding
{
    /// <summary>
    /// Tests for whether two floor-plan rectangles are adjacent (share an edge) and lie on
    /// the same chain axis. Pure functions — no mutable state.
    /// </summary>
    public static class Adjacency
    {
        /// <summary>
        /// Tolerance for calling two rectangles "touching". Scales with the smaller
        /// of their thicknesses (short sides) to absorb floating-point snap gaps.
        /// </summary>
        public static double AdjacencyThreshold(Rect a, Rect b, double lengthEpsilon)
        {
            double reference = Math.Min(a.Thickness2D, b.Thickness2D);
            return Math.Max(reference * 0.5, lengthEpsilon);
        }

        /// <summary>
        /// True if <paramref name="a"/> and <paramref name="b"/> lie on the same axis
        /// (their centers are close in the perpendicular direction).
        /// </summary>
        public static bool AreOnSameAxis(Rect a, Rect b, Vec2 direction)
        {
            var perp = Vec2Math.Perpendicular(direction);
            double perpA = Vec2Math.Dot(a.Center2D(), perp);
            double perpB = Vec2Math.Dot(b.Center2D(), perp);
            double maxThickness = Math.Max(a.Extent2D(perp), b.Extent2D(perp));
            return Math.Abs(perpA - perpB) < maxThickness;
        }

        /// <summary>
        /// Finds the closest still-unused candidate that (a) touches <paramref name="current"/>
        /// and (b) is on the same axis as <paramref name="direction"/>. Returns -1 if none qualify.
        /// </summary>
        public static int FindAdjacent(
            Rect current,
            List<TaggedRect> candidates,
            HashSet<int> used,
            Vec2 direction,
            double lengthEpsilon)
        {
            int bestIdx = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (used.Contains(i)) continue;

                var candidate = candidates[i].Rect;
                double dist = current.MinVertexDistance2D(candidate);
                if (dist >= bestDist) continue;
                if (dist >= AdjacencyThreshold(current, candidate, lengthEpsilon)) continue;
                if (!AreOnSameAxis(current, candidate, direction)) continue;

                bestDist = dist;
                bestIdx = i;
            }

            return bestIdx;
        }
    }
}
