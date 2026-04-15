using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.ChainBuilding
{
    /// <summary>
    /// Groups walls and openings into chains. A chain is a connected component
    /// of walls linked by openings: an opening "connects" the wall on its left
    /// and the wall on its right, and chains form from the transitive closure
    /// of those links (BFS).
    ///
    /// Pipeline:
    /// 1. <see cref="FindFlankingWalls"/> — for each opening, find its 2 flanking
    ///    walls (one at each end along the opening's long axis).
    /// 2. BFS over openings: two openings join the same chain iff they share a
    ///    flanking wall. Each connected component becomes one <see cref="Chain"/>.
    ///
    /// Assumptions:
    /// - Inputs are already classified by layer name into walls / windows / doors
    ///   by <c>SelectFloorPlanWorkflow</c>; this class does not look at layers.
    /// - Openings always have length &gt; thickness, so each opening's
    ///   <see cref="Rect.Direction2D"/> reliably points along the chain.
    /// - A flanking wall touches the opening: their nearest vertices are within
    ///   <c>touchTolerance</c>. In practice AutoCAD snap makes this distance ≈ 0
    ///   and the tolerance only absorbs floating-point noise. Pass
    ///   <see cref="UnitSystem.LengthEpsilon"/>.
    /// - An opening with fewer than 2 flanking walls is dropped from chain
    ///   building (it cannot be hosted on a wall on both sides).
    /// - Wall geometry NEAR an opening may have its long axis perpendicular to
    ///   the chain (a short "stub" between two openings). The algorithm never
    ///   reads a wall's Direction2D, so stubs are handled identically to normal
    ///   walls — only "is it touching the opening" matters.
    /// </summary>
    public static class ChainBuilder
    {
        /// <summary>
        /// Builds all chains from the given walls and openings.
        /// </summary>
        /// <param name="walls">Wall rectangles in their original input order.</param>
        /// <param name="openings">Window + door rectangles in their original input order.</param>
        /// <param name="touchTolerance">Maximum distance between two rectangles' nearest
        /// vertices for them to count as touching. Pass <see cref="UnitSystem.LengthEpsilon"/>.</param>
        public static List<Chain> Build(
            List<TaggedRect> walls,
            List<TaggedRect> openings,
            double touchTolerance)
        {
            var flanks = new (int leftWall, int rightWall)?[openings.Count];
            for (int i = 0; i < openings.Count; i++)
                flanks[i] = FindFlankingWalls(openings[i].Rect, walls, touchTolerance);

            var visited = new bool[openings.Count];
            var chains = new List<Chain>();

            for (int seed = 0; seed < openings.Count; seed++)
            {
                if (visited[seed]) continue;
                if (flanks[seed] == null) continue;

                var componentWalls = new HashSet<int>();
                var componentOpenings = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(seed);
                visited[seed] = true;

                while (queue.Count > 0)
                {
                    int op = queue.Dequeue();
                    componentOpenings.Add(op);
                    var (lw, rw) = flanks[op]!.Value;
                    componentWalls.Add(lw);
                    componentWalls.Add(rw);

                    // Any other opening that shares one of these flanking walls
                    // belongs to the same chain.
                    for (int j = 0; j < openings.Count; j++)
                    {
                        if (visited[j]) continue;
                        if (flanks[j] == null) continue;
                        var (jl, jr) = flanks[j]!.Value;
                        if (jl == lw || jl == rw || jr == lw || jr == rw)
                        {
                            visited[j] = true;
                            queue.Enqueue(j);
                        }
                    }
                }

                chains.Add(BuildChain(
                    direction: openings[seed].Rect.Direction2D,
                    componentWalls,
                    componentOpenings,
                    walls,
                    openings));
            }

            return chains;
        }

        /// <summary>
        /// For one opening, finds the wall on each side along the opening's long axis.
        ///
        /// Side rule: a wall is on the "left" if its center, projected onto the
        /// opening's <see cref="Rect.Direction2D"/>, is less than the opening's own
        /// center projection; otherwise it is on the "right".
        ///
        /// Returns null if either side has no touching wall — such an opening cannot
        /// be hosted and is dropped by <see cref="Build"/>.
        ///
        /// Assumptions:
        /// - <paramref name="opening"/> has length &gt; thickness, so Direction2D is reliable.
        /// - Touching means MinVertexDistance2D &lt; <paramref name="touchTolerance"/>.
        ///   Wall direction is irrelevant: a perpendicular L-corner wall does not touch
        ///   the opening (there is always a parallel wall stub between them in a real
        ///   floor plan), so it is naturally rejected by the distance check.
        /// </summary>
        private static (int leftWall, int rightWall)? FindFlankingWalls(
            Rect opening,
            List<TaggedRect> walls,
            double touchTolerance)
        {
            var dir = opening.Direction2D;
            double openingT = opening.ProjectCenter2D(dir);

            int leftIdx = -1, rightIdx = -1;
            double leftBest = double.MaxValue, rightBest = double.MaxValue;

            for (int i = 0; i < walls.Count; i++)
            {
                var wall = walls[i].Rect;
                double dist = opening.MinVertexDistance2D(wall);
                if (dist >= touchTolerance) continue;

                double wallT = wall.ProjectCenter2D(dir);
                if (wallT < openingT)
                {
                    if (dist < leftBest) { leftBest = dist; leftIdx = i; }
                }
                else
                {
                    if (dist < rightBest) { rightBest = dist; rightIdx = i; }
                }
            }

            if (leftIdx < 0 || rightIdx < 0) return null;
            return (leftIdx, rightIdx);
        }

        /// <summary>
        /// Materializes one connected component into a <see cref="Chain"/> with walls
        /// and openings sorted along <paramref name="direction"/>.
        /// Cross-row flanking walls (e.g. a tall vertical wall connecting two
        /// horizontal rows) are excluded from the chain — they served only as
        /// graph links during BFS and must remain available as standalone walls.
        /// A wall is cross-row if its perpendicular extent exceeds 2× the row
        /// thickness (derived from the openings).
        /// </summary>
        private static Chain BuildChain(
            Vec2 direction,
            HashSet<int> wallIndices,
            List<int> openingIndices,
            List<TaggedRect> allWalls,
            List<TaggedRect> allOpenings)
        {
            var perp = Vec2Math.Perpendicular(direction);

            // Compute perpendicular extent from openings — they define the row.
            double rowPerpMin = double.MaxValue, rowPerpMax = double.MinValue;
            foreach (var oi in openingIndices)
            {
                foreach (var p in allOpenings[oi].Rect.Points.Take(4))
                {
                    double pv = p.X * perp.X + p.Y * perp.Y;
                    if (pv < rowPerpMin) rowPerpMin = pv;
                    if (pv > rowPerpMax) rowPerpMax = pv;
                }
            }
            double rowThickness = rowPerpMax - rowPerpMin;

            var inRowWallIdx = wallIndices
                .Where(i => FitsInRow(allWalls[i].Rect, perp, rowThickness))
                .OrderBy(i => allWalls[i].Rect.ProjectCenter2D(direction))
                .ToList();

            var orderedOpenings = openingIndices
                .Select(i => allOpenings[i])
                .OrderBy(o => o.Rect.ProjectCenter2D(direction))
                .ToList();

            return new Chain(
                direction,
                inRowWallIdx.Select(i => allWalls[i]).ToList(),
                orderedOpenings,
                inRowWallIdx);
        }

        /// <summary>
        /// A wall fits in the row if its perpendicular extent does not
        /// significantly exceed the row thickness derived from the openings.
        /// A small stub wall (e.g. 2×4 between two windows on a 4-thick row)
        /// passes; a tall cross-row wall (e.g. 5×108 connecting two rows) fails.
        /// </summary>
        private static bool FitsInRow(Rect wall, Vec2 perp, double rowThickness)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var p in wall.Points.Take(4))
            {
                double pv = p.X * perp.X + p.Y * perp.Y;
                if (pv < min) min = pv;
                if (pv > max) max = pv;
            }
            return (max - min) <= rowThickness * 2;
        }
    }
}
