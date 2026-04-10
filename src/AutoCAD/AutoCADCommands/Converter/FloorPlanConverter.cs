using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter
{
    /// <summary>
    /// Converts AutoCAD floor plan polylines (walls, windows, doors) into BuildingModel objects.
    ///
    /// In a 2D floor plan, openings (windows/doors) sit between wall polylines forming straight chains:
    ///   [wall] [opening] [wall] [opening] [wall]
    ///
    /// Rules:
    /// - Openings always have length > thickness, so their long axis is reliable.
    /// - Wall polylines near openings can be very short (thickness > length), so their
    ///   long axis is NOT reliable. We use the adjacent opening's direction instead.
    /// - Openings always alternate with walls (never two openings in a row).
    /// - Chains are always straight lines.
    /// - A chain always starts and ends with a wall.
    ///
    /// Algorithm:
    /// 1. Store all polyline vertices (no center line extraction yet for walls).
    /// 2. For each opening, extract center line (long axis — reliable).
    /// 3. Build chains by walking along the opening's direction, finding adjacent
    ///    polylines by shared/close vertices.
    /// 4. For wall polylines in a chain, project onto the chain direction to get
    ///    the correct center line.
    /// 5. Merge the entire chain into one Wall with openings inside it.
    /// 6. Standalone walls (not in any chain) use default long-axis logic.
    /// </summary>
    public static class FloorPlanConverter
    {
        public static FloorPlanResult Convert(
            Building building,
            Story story,
            List<AcadPolyline> wallPolylines,
            List<AcadPolyline> windowPolylines,
            List<AcadPolyline> doorPolylines)
        {
            double botElevation = story.BotLevel.Elevation;
            double topElevation = story.TopLevel.Elevation;

            // Extract raw vertices for all polylines
            var wallElements = wallPolylines
                .Select(p => new PolylineElement(p, ElementType.Wall))
                .Where(e => e.Vertices.Count >= 4)
                .ToList();

            var openingElements = new List<PolylineElement>();
            foreach (var p in windowPolylines)
            {
                var e = new PolylineElement(p, ElementType.Window);
                if (e.Vertices.Count >= 4) openingElements.Add(e);
            }
            foreach (var p in doorPolylines)
            {
                var e = new PolylineElement(p, ElementType.Door);
                if (e.Vertices.Count >= 4) openingElements.Add(e);
            }

            // Build chains starting from openings
            var usedWalls = new HashSet<int>();
            var usedOpenings = new HashSet<int>();
            var chains = new List<Chain>();

            double lengthEpsilon = building.Units.LengthEpsilon;

            for (int i = 0; i < openingElements.Count; i++)
            {
                if (usedOpenings.Contains(i)) continue;

                var chain = BuildChain(i, openingElements, wallElements, usedOpenings, usedWalls, lengthEpsilon);
                if (chain != null)
                    chains.Add(chain);
            }

            // Create domain objects
            var result = new FloorPlanResult();

            foreach (var chain in chains)
            {
                CreateWallFromChain(building, chain, botElevation, topElevation, result);
            }

            // Standalone walls (not part of any chain)
            for (int i = 0; i < wallElements.Count; i++)
            {
                if (usedWalls.Contains(i)) continue;
                CreateStandaloneWall(building, wallElements[i], botElevation, topElevation, result);
            }

            return result;
        }

        // =====================================================================
        // Chain building
        // =====================================================================

        /// <summary>
        /// Builds a chain starting from an opening, walking in both directions
        /// along the opening's axis to find alternating wall-opening-wall sequences.
        /// </summary>
        private static Chain BuildChain(
            int startOpeningIdx,
            List<PolylineElement> openingElements,
            List<PolylineElement> wallElements,
            HashSet<int> usedOpenings,
            HashSet<int> usedWalls,
            double lengthEpsilon)
        {
            var startOpening = openingElements[startOpeningIdx];
            var direction = GetOpeningDirection(startOpening);
            if (direction == null) return null;

            var dir = direction.Value;

            // Collect chain elements in order along the direction
            // Start with the seed opening, then walk left and right
            var chainElements = new List<(PolylineElement element, int originalIdx, bool isOpening)>();
            chainElements.Add((startOpening, startOpeningIdx, true));
            usedOpenings.Add(startOpeningIdx);

            // Walk in both directions (positive and negative along dir)
            WalkDirection(chainElements, dir, openingElements, wallElements, usedOpenings, usedWalls, forward: true, lengthEpsilon);
            WalkDirection(chainElements, dir, openingElements, wallElements, usedOpenings, usedWalls, forward: false, lengthEpsilon);

            // Sort chain elements along the direction
            chainElements.Sort((a, b) =>
            {
                double tA = ProjectCenter(a.element, dir);
                double tB = ProjectCenter(b.element, dir);
                return tA.CompareTo(tB);
            });

            return new Chain
            {
                Direction = dir,
                Elements = chainElements
            };
        }

        /// <summary>
        /// Walks from the current chain endpoints in one direction, alternating
        /// between finding walls and openings adjacent to the last element.
        /// </summary>
        private static void WalkDirection(
            List<(PolylineElement element, int originalIdx, bool isOpening)> chainElements,
            Point2d direction,
            List<PolylineElement> openingElements,
            List<PolylineElement> wallElements,
            HashSet<int> usedOpenings,
            HashSet<int> usedWalls,
            bool forward,
            double lengthEpsilon)
        {
            // Get the current outermost element in this direction
            var current = forward
                ? chainElements.OrderByDescending(e => ProjectCenter(e.element, direction)).First()
                : chainElements.OrderBy(e => ProjectCenter(e.element, direction)).First();

            bool expectWall = true; // After an opening, expect a wall

            while (true)
            {
                if (expectWall)
                {
                    // Find adjacent wall
                    int wallIdx = FindAdjacentElement(current.element, wallElements, usedWalls, direction, lengthEpsilon);
                    if (wallIdx < 0) break;

                    var wallEl = (wallElements[wallIdx], wallIdx, false);
                    chainElements.Add(wallEl);
                    usedWalls.Add(wallIdx);
                    current = wallEl;
                    expectWall = false;
                }
                else
                {
                    // Find adjacent opening
                    int openingIdx = FindAdjacentElement(current.element, openingElements, usedOpenings, direction, lengthEpsilon);
                    if (openingIdx < 0) break;

                    var openingEl = (openingElements[openingIdx], openingIdx, true);
                    chainElements.Add(openingEl);
                    usedOpenings.Add(openingIdx);
                    current = openingEl;
                    expectWall = true;
                }
            }
        }

        /// <summary>
        /// Finds an element from the candidates list that shares vertices with the given element.
        /// </summary>
        private static int FindAdjacentElement(
            PolylineElement current,
            List<PolylineElement> candidates,
            HashSet<int> used,
            Point2d direction,
            double lengthEpsilon)
        {
            int bestIdx = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (used.Contains(i)) continue;

                double dist = MinVertexDistance(current, candidates[i]);
                if (dist < bestDist && dist < GetAdjacencyThreshold(current, candidates[i], lengthEpsilon))
                {
                    // Verify they are along the same axis (perpendicular distance is small)
                    if (AreOnSameAxis(current, candidates[i], direction))
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }
            }

            return bestIdx;
        }

        // =====================================================================
        // Domain object creation
        // =====================================================================

        /// <summary>
        /// Creates a Wall from a chain and adds all openings to it.
        /// The wall center line spans the full chain length.
        /// Wall segments in the chain are projected onto the chain direction.
        /// </summary>
        private static void CreateWallFromChain(
            Building building, Chain chain,
            double botElevation, double topElevation,
            FloorPlanResult result)
        {
            var dir = chain.Direction;

            // Project all vertices of all elements onto the chain direction
            // to find the overall start and end
            double minT = double.MaxValue, maxT = double.MinValue;
            double totalThickness = 0;
            int wallCount = 0;

            // Also compute the perpendicular center (average of all vertices' perp component)
            double perpSum = 0;
            int vertexCount = 0;
            var perp = new Point2d(-dir.Y, dir.X);

            foreach (var (element, _, isOpening) in chain.Elements)
            {
                foreach (var v in element.Vertices)
                {
                    var pt = new Point2d(v.X, v.Y);
                    double t = DotProduct(pt, dir);
                    double p = DotProduct(pt, perp);
                    perpSum += p;
                    vertexCount++;

                    if (t < minT) { minT = t; }
                    if (t > maxT) { maxT = t; }
                }

                if (!isOpening)
                {
                    // Compute thickness of this wall segment perpendicular to chain direction
                    double minPerp = double.MaxValue, maxPerp = double.MinValue;
                    foreach (var v in element.Vertices)
                    {
                        double p = DotProduct(new Point2d(v.X, v.Y), perp);
                        minPerp = Math.Min(minPerp, p);
                        maxPerp = Math.Max(maxPerp, p);
                    }
                    totalThickness += maxPerp - minPerp;
                    wallCount++;
                }
            }

            double avgPerp = perpSum / vertexCount;
            double thickness = wallCount > 0 ? totalThickness / wallCount : building.Units.DefaultWallThickness;

            // Center line endpoints: project min/max T back to 2D using the average perp
            var startPt = new Point2d(minT * dir.X + avgPerp * perp.X, minT * dir.Y + avgPerp * perp.Y);
            var endPt = new Point2d(maxT * dir.X + avgPerp * perp.X, maxT * dir.Y + avgPerp * perp.Y);

            var wall = building.AddWall(startPt.X, startPt.Y, endPt.X, endPt.Y,
                botElevation, topElevation, thickness);
            result.WallsCreated++;

            // Add openings
            foreach (var (element, _, isOpening) in chain.Elements)
            {
                if (!isOpening) continue;

                // Project opening vertices onto chain direction to get opening center line
                double openMinT = double.MaxValue, openMaxT = double.MinValue;
                foreach (var v in element.Vertices)
                {
                    double t = DotProduct(new Point2d(v.X, v.Y), dir);
                    openMinT = Math.Min(openMinT, t);
                    openMaxT = Math.Max(openMaxT, t);
                }

                // Opening thickness (perpendicular extent) = opening height in 3D
                double openMinPerp = double.MaxValue, openMaxPerp = double.MinValue;
                foreach (var v in element.Vertices)
                {
                    double p = DotProduct(new Point2d(v.X, v.Y), perp);
                    openMinPerp = Math.Min(openMinPerp, p);
                    openMaxPerp = Math.Max(openMaxPerp, p);
                }

                var openStart = new Point2d(openMinT * dir.X + avgPerp * perp.X,
                                             openMinT * dir.Y + avgPerp * perp.Y);
                var openEnd = new Point2d(openMaxT * dir.X + avgPerp * perp.X,
                                           openMaxT * dir.Y + avgPerp * perp.Y);
                double openingHeight = openMaxPerp - openMinPerp;

                try
                {
                    double z = botElevation;
                    switch (element.Type)
                    {
                        case ElementType.Window:
                            wall.AddWindow(building, openStart.X, openStart.Y,
                                openEnd.X, openEnd.Y, z, openingHeight);
                            result.WindowsCreated++;
                            break;
                        case ElementType.Door:
                            wall.AddDoor(building, openStart.X, openStart.Y,
                                openEnd.X, openEnd.Y, z, openingHeight);
                            result.DoorsCreated++;
                            break;
                    }
                }
                catch
                {
                    result.OpeningsSkipped++;
                }
            }
        }

        /// <summary>
        /// Creates a standalone wall (no openings). Uses long-axis logic since
        /// standalone walls are always long enough that length > thickness.
        /// </summary>
        private static void CreateStandaloneWall(
            Building building, PolylineElement element,
            double botElevation, double topElevation,
            FloorPlanResult result)
        {
            var rect = ExtractRectByLongAxis(element);
            if (rect == null) return;

            building.AddWall(rect.CenterLine.Start.X, rect.CenterLine.Start.Y,
                rect.CenterLine.End.X, rect.CenterLine.End.Y,
                botElevation, topElevation, rect.Thickness);
            result.WallsCreated++;
        }

        // =====================================================================
        // Geometry extraction
        // =====================================================================

        /// <summary>
        /// Gets the direction of an opening's center line (long axis — always reliable for openings).
        /// </summary>
        private static Point2d? GetOpeningDirection(PolylineElement element)
        {
            if (element.Vertices.Count < 4) return null;

            var pts = element.Vertices;
            double side1 = Distance3d(pts[0], pts[1]);
            double side2 = Distance3d(pts[1], pts[2]);

            Point2d start, end;
            if (side1 >= side2)
            {
                start = To2d(pts[0]);
                end = To2d(pts[1]);
            }
            else
            {
                start = To2d(pts[1]);
                end = To2d(pts[2]);
            }

            return Normalize(Subtract(end, start));
        }

        /// <summary>
        /// Extracts center line and thickness using the long axis (only for standalone walls).
        /// </summary>
        private static RectInfo ExtractRectByLongAxis(PolylineElement element)
        {
            if (element.Vertices.Count < 4) return null;

            var pts = element.Vertices;
            double side1 = Distance3d(pts[0], pts[1]);
            double side2 = Distance3d(pts[1], pts[2]);

            Point2d start, end;
            double thickness;

            if (side1 >= side2)
            {
                start = Mid2d(pts[0], pts[3]);
                end = Mid2d(pts[1], pts[2]);
                thickness = side2;
            }
            else
            {
                start = Mid2d(pts[0], pts[1]);
                end = Mid2d(pts[3], pts[2]);
                thickness = side1;
            }

            return new RectInfo { CenterLine = (start, end), Thickness = thickness };
        }

        // =====================================================================
        // Adjacency detection
        // =====================================================================

        /// <summary>
        /// Computes the minimum distance between any vertex of element A and any vertex of element B.
        /// </summary>
        private static double MinVertexDistance(PolylineElement a, PolylineElement b)
        {
            double min = double.MaxValue;
            foreach (var va in a.Vertices)
            {
                foreach (var vb in b.Vertices)
                {
                    double d = Distance2d(To2d(va), To2d(vb));
                    if (d < min) min = d;
                }
            }
            return min;
        }

        /// <summary>
        /// Adjacency threshold: vertices must be closer than a small fraction of the elements.
        /// In a floor plan, adjacent wall/opening rectangles share edges, so distance ~ 0.
        /// We allow a small tolerance for floating point imprecision.
        /// </summary>
        private static double GetAdjacencyThreshold(PolylineElement a, PolylineElement b, double lengthEpsilon)
        {
            // Use the smaller of the two elements' shortest sides as reference
            double aMin = MinSide(a);
            double bMin = MinSide(b);
            double reference = Math.Min(aMin, bMin);

            // Adjacent elements share edges — distance should be near zero
            // Allow up to half the smaller side as tolerance for snapping gaps
            return Math.Max(reference * 0.5, lengthEpsilon);
        }

        /// <summary>
        /// Checks if two elements are on the same axis (perpendicular distance between their
        /// centers is small relative to their size).
        /// </summary>
        private static bool AreOnSameAxis(PolylineElement a, PolylineElement b, Point2d direction)
        {
            var perp = new Point2d(-direction.Y, direction.X);

            var centerA = Center2d(a);
            var centerB = Center2d(b);

            double perpA = DotProduct(centerA, perp);
            double perpB = DotProduct(centerB, perp);

            double maxThickness = Math.Max(MaxPerpExtent(a, perp), MaxPerpExtent(b, perp));

            return Math.Abs(perpA - perpB) < maxThickness;
        }

        // =====================================================================
        // Geometry helpers
        // =====================================================================

        private static double ProjectCenter(PolylineElement element, Point2d direction)
        {
            var center = Center2d(element);
            return DotProduct(center, direction);
        }

        private static Point2d Center2d(PolylineElement element)
        {
            double sumX = 0, sumY = 0;
            foreach (var v in element.Vertices)
            {
                sumX += v.X;
                sumY += v.Y;
            }
            return new Point2d(sumX / element.Vertices.Count, sumY / element.Vertices.Count);
        }

        private static double MaxPerpExtent(PolylineElement element, Point2d perp)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var v in element.Vertices)
            {
                double p = DotProduct(To2d(v), perp);
                min = Math.Min(min, p);
                max = Math.Max(max, p);
            }
            return max - min;
        }

        private static double MinSide(PolylineElement element)
        {
            if (element.Vertices.Count < 4) return double.MaxValue;
            double s1 = Distance3d(element.Vertices[0], element.Vertices[1]);
            double s2 = Distance3d(element.Vertices[1], element.Vertices[2]);
            return Math.Min(s1, s2);
        }

        private static Point2d To2d(Point3d p) => new(p.X, p.Y);

        private static Point2d Mid2d(Point3d a, Point3d b)
            => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        private static Point2d Subtract(Point2d a, Point2d b)
            => new(a.X - b.X, a.Y - b.Y);

        private static Point2d Normalize(Point2d v)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len < 1e-12) return new Point2d(0, 0);
            return new Point2d(v.X / len, v.Y / len);
        }

        private static double DotProduct(Point2d a, Point2d b)
            => a.X * b.X + a.Y * b.Y;

        private static double Distance2d(Point2d a, Point2d b)
            => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private static double Distance3d(Point3d a, Point3d b)
            => a.DistanceTo(b);

        // =====================================================================
        // Helper types
        // =====================================================================

        internal class PolylineElement
        {
            public List<Point3d> Vertices { get; }
            public ElementType Type { get; }

            public PolylineElement(AcadPolyline polyline, ElementType type)
            {
                Type = type;
                Vertices = new List<Point3d>();
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                    Vertices.Add(polyline.GetPoint3dAt(i));
            }
        }

        internal enum ElementType
        {
            Wall,
            Window,
            Door
        }

        private class RectInfo
        {
            public (Point2d Start, Point2d End) CenterLine { get; set; }
            public double Thickness { get; set; }
        }

        private class Chain
        {
            public Point2d Direction { get; set; }
            public List<(PolylineElement element, int originalIdx, bool isOpening)> Elements { get; set; }
        }
    }

    public class FloorPlanResult
    {
        public int WallsCreated { get; set; }
        public int WindowsCreated { get; set; }
        public int DoorsCreated { get; set; }
        public int OpeningsSkipped { get; set; }
    }
}
