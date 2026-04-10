using System.Collections.Generic;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.ChainBuilding
{
    /// <summary>
    /// Builds wall/opening chains from a flat list of tagged floor-plan rectangles.
    ///
    /// Held as an instance so that the "used" sets and the element lists become
    /// private fields instead of being threaded through every helper method.
    /// Construct one builder per floor plan conversion, then call
    /// <see cref="BuildAll"/> to get the chains.
    /// </summary>
    public class ChainBuilder(List<TaggedRect> walls, List<TaggedRect> openings, double lengthEpsilon)
    {
        private readonly List<TaggedRect> _walls = walls;
        private readonly List<TaggedRect> _openings = openings;
        private readonly double _lengthEpsilon = lengthEpsilon;
        private readonly HashSet<int> _usedWalls = [];
        private readonly HashSet<int> _usedOpenings = [];

        /// <summary>Indices of wall elements that were consumed by some chain.</summary>
        public HashSet<int> UsedWalls => _usedWalls;

        /// <summary>
        /// Walks every opening and grows a chain from it (if not already consumed).
        /// Returns the list of completed chains.
        /// </summary>
        public List<Chain> BuildAll()
        {
            var chains = new List<Chain>();
            for (int i = 0; i < _openings.Count; i++)
            {
                if (_usedOpenings.Contains(i)) continue;
                var chain = BuildFromOpening(i);
                if (chain != null) chains.Add(chain);
            }
            return chains;
        }

        /// <summary>
        /// Builds a chain seeded at <paramref name="startOpeningIdx"/>, walking both
        /// ways along the opening's long axis until no more neighbours are found.
        /// </summary>
        private Chain BuildFromOpening(int startOpeningIdx)
        {
            var startOpening = _openings[startOpeningIdx];
            var dir = startOpening.Rect.Direction2D;

            var chainElements = new List<ChainEntry>
            {
                new(startOpening, startOpeningIdx, isOpening: true)
            };
            _usedOpenings.Add(startOpeningIdx);

            // Walk in both directions (positive and negative along dir)
            Walk(chainElements, dir, forward: true);
            Walk(chainElements, dir, forward: false);

            // Sort chain elements along the direction so they appear in spatial order
            chainElements.Sort((a, b) =>
            {
                double tA = a.Element.Rect.ProjectCenter2D(dir);
                double tB = b.Element.Rect.ProjectCenter2D(dir);
                return tA.CompareTo(tB);
            });

            return new Chain { Direction = dir, Elements = chainElements };
        }

        /// <summary>
        /// From the current endpoint in a given direction, alternates wall/opening lookups
        /// until the chain can grow no further.
        /// </summary>
        private void Walk(List<ChainEntry> chainElements, Vec2 direction, bool forward)
        {
            var current = GetEndpoint(chainElements, direction, forward);
            bool expectWall = true; // seeds are openings, so the next element must be a wall

            while (true)
            {
                if (expectWall)
                {
                    int wallIdx = Adjacency.FindAdjacent(current.Element.Rect, _walls, _usedWalls, direction, _lengthEpsilon);
                    if (wallIdx < 0) break;

                    var entry = new ChainEntry(_walls[wallIdx], wallIdx, isOpening: false);
                    chainElements.Add(entry);
                    _usedWalls.Add(wallIdx);
                    current = entry;
                }
                else
                {
                    int openingIdx = Adjacency.FindAdjacent(current.Element.Rect, _openings, _usedOpenings, direction, _lengthEpsilon);
                    if (openingIdx < 0) break;

                    var entry = new ChainEntry(_openings[openingIdx], openingIdx, isOpening: true);
                    chainElements.Add(entry);
                    _usedOpenings.Add(openingIdx);
                    current = entry;
                }

                expectWall = !expectWall;
            }
        }

        /// <summary>
        /// Returns the chain entry that is currently furthest along (or against) the walking direction.
        /// </summary>
        private static ChainEntry GetEndpoint(List<ChainEntry> chainElements, Vec2 direction, bool forward)
        {
            ChainEntry best = chainElements[0];
            double bestT = best.Element.Rect.ProjectCenter2D(direction);
            for (int i = 1; i < chainElements.Count; i++)
            {
                double t = chainElements[i].Element.Rect.ProjectCenter2D(direction);
                if (forward ? t > bestT : t < bestT)
                {
                    bestT = t;
                    best = chainElements[i];
                }
            }
            return best;
        }
    }
}
