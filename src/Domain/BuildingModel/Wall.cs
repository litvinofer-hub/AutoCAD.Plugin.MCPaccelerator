using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public enum WallType
    {
        UNKNOWN,
        LB, // load bearing
        NB  // non-bearing
    }

    /// <summary>
    /// Represents a wall element. A wall is modeled as a <b>box</b>: vertical
    /// sides, constant <see cref="Thickness"/> along its full height. Its
    /// footprint at any elevation between <see cref="BotLevel"/> and
    /// <see cref="TopLevel"/> is the same as <see cref="BotLine"/> projected
    /// to that elevation — in particular, the wall's top middle-line is
    /// BotLine shifted up to TopLevel.Elevation. This assumption is what
    /// lets the <see cref="LevelPlanGraph"/> at the wall's top level
    /// represent the wall as a single edge whose endpoints share the wall's
    /// XY with Z = TopLevel.Elevation.
    /// </summary>
    public class Wall : IHavePoints
    {
        public WallType Type { get; set; } = WallType.UNKNOWN;

        public Guid Id { get; private set; }
        public Guid BuildingId { get; private set; }

        /// <summary>
        /// Id of the story this wall belongs to. Walls cannot exist without a
        /// story — <see cref="Building.AddWall(double, double, double, double, Story, double)"/>
        /// and the elevation-based overload both enforce this invariant.
        /// </summary>
        public Guid StoryId { get; private set; }

        /// <summary>
        /// 2D line at the bottom elevation. Z coordinates match BotLevel.Elevation.
        /// </summary>
        public LineSegment BotLine { get; private set; }
        public double Thickness { get; private set; }

        /// <summary>
        /// Derived from BotLine's Z coordinate. Returns the Level whose elevation matches.
        /// </summary>
        public Level BotLevel { get; private set; }
        public Level TopLevel { get; private set; }

        private readonly List<WallOpening> _openings;
        public IReadOnlyList<WallOpening> Openings => _openings.AsReadOnly();

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        public Wall(Guid buildingId, LineSegment botLine, double thickness, Level topLevel,
            IReadOnlyList<Level> buildingLevels, Guid storyId)
        {
            double lineZ = GetAndValidateLineZ(botLine);
            Level botLevel = FindMatchingLevel(lineZ, buildingLevels);
            if (storyId == Guid.Empty)
                throw new ArgumentException("Wall must belong to a story (storyId cannot be empty).", nameof(storyId));

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            StoryId = storyId;
            BotLine = botLine;
            Thickness = thickness;
            BotLevel = botLevel;
            TopLevel = topLevel;
            _openings = new List<WallOpening>();
        }

        /// <summary>
        /// Splits this wall into its solid 2D sub-wall rectangles — the pieces of
        /// the wall that remain once the openings are subtracted.
        ///
        /// Rules (each opening is projected onto the wall's bottom line):
        /// <list type="bullet">
        /// <item>0 openings → 1 rect (the whole wall).</item>
        /// <item>1 opening  → 2 rects: before and after the opening.</item>
        /// <item>2 openings → 3 rects: before, between, after.</item>
        /// <item>N openings → N + 1 rects in order along the wall.</item>
        /// </list>
        /// Rectangles are built at Z = 0 (pure 2D / floor plan) with the wall's
        /// thickness. Degenerate (zero-length) pieces — e.g. an opening touching
        /// the wall's start or end — are omitted so callers always get valid rects.
        /// </summary>
        public List<Rect> SubWalls()
        {
            var wallStart = new Vec2(BotLine.StartPoint.X, BotLine.StartPoint.Y);
            var wallEnd   = new Vec2(BotLine.EndPoint.X,   BotLine.EndPoint.Y);
            double length = Vec2Math.Distance(wallStart, wallEnd);
            if (length <= 0) return new List<Rect>();

            var dir = Vec2Math.Normalize(Vec2Math.Subtract(wallEnd, wallStart));

            // Project each opening onto the wall axis → [tMin, tMax] intervals
            // measured from the wall's start point.
            var intervals = new List<(double min, double max)>(_openings.Count);
            foreach (var op in _openings)
            {
                double t1 = ProjectOntoAxis(op.Line.StartPoint, wallStart, dir);
                double t2 = ProjectOntoAxis(op.Line.EndPoint,   wallStart, dir);
                if (t1 > t2) (t1, t2) = (t2, t1);
                intervals.Add((t1, t2));
            }
            intervals.Sort((a, b) => a.min.CompareTo(b.min));

            // Walk the axis from 0 to length, emitting a rect for each gap
            // between consecutive opening boundaries.
            var result = new List<Rect>(_openings.Count + 1);
            double cursor = 0;
            foreach (var (imin, imax) in intervals)
            {
                TryEmitSubWall(result, cursor, imin, wallStart, dir);
                cursor = imax;
            }
            TryEmitSubWall(result, cursor, length, wallStart, dir);
            return result;
        }

        private void TryEmitSubWall(List<Rect> result, double tStart, double tEnd, Vec2 wallStart, Vec2 dir)
        {
            if (GeometrySettings.IsLessThan(tEnd, tStart) || GeometrySettings.AreEqual(tStart, tEnd))
                return; // degenerate piece — skip

            var a = new Point(wallStart.X + dir.X * tStart, wallStart.Y + dir.Y * tStart, 0);
            var b = new Point(wallStart.X + dir.X * tEnd,   wallStart.Y + dir.Y * tEnd,   0);
            result.Add(new LineSegment(a, b).ToRect(Thickness));
        }

        private static double ProjectOntoAxis(Point p, Vec2 origin, Vec2 dir)
        {
            var v = new Vec2(p.X - origin.X, p.Y - origin.Y);
            return Vec2Math.Dot(v, dir);
        }

        public bool RemoveOpening(WallOpening opening)
        {
            return _openings.Remove(opening);
        }

        /// <summary>
        /// Validates and attaches a pre-built opening to this wall.
        /// Called by <see cref="Building.AddWindow"/> / <see cref="Building.AddDoor"/>,
        /// which are responsible for building the opening with shared (flyweight) points.
        /// </summary>
        internal void AttachOpening(WallOpening opening)
        {
            ValidateOpeningLineXY(opening);
            ValidateOpeningLineZ(opening);
            ValidateOpeningHeight(opening);
            _openings.Add(opening);
        }

        public IEnumerable<Point> GetPoints()
        {
            yield return BotLine.StartPoint;
            yield return BotLine.EndPoint;

            foreach (var opening in _openings)
            {
                foreach (var point in opening.GetPoints())
                    yield return point;
            }
        }

        /// <summary>
        /// Validates that both endpoints of the line have the same Z coordinate.
        /// Returns that common Z value.
        /// </summary>
        private static double GetAndValidateLineZ(LineSegment line)
        {
            if (!GeometrySettings.AreEqual(line.StartPoint.Z, line.EndPoint.Z))
            {
                throw new ArgumentException("Wall line Z coordinates must be equal.");
            }

            return line.StartPoint.Z;
        }

        private static Level FindMatchingLevel(double z, IReadOnlyList<Level> levels)
        {
            var level = levels.FirstOrDefault(l => GeometrySettings.AreEqual(l.Elevation, z));

            if (level == null)
            {
                throw new ArgumentException(
                    $"No Level found with elevation matching wall line Z ({z}). " +
                    "Register the Level in the Building before creating the Wall.");
            }

            return level;
        }

        private void ValidateOpeningLineXY(WallOpening opening)
        {
            if (!BotLine.IsPointOnSegment2D(opening.Line.StartPoint) ||
                !BotLine.IsPointOnSegment2D(opening.Line.EndPoint))
            {
                throw new ArgumentException("Opening line (X,Y) must be within the wall line.");
            }
        }

        private void ValidateOpeningLineZ(WallOpening opening)
        {
            double openingZ = opening.Line.StartPoint.Z;
            double botZ = BotLevel.Elevation;
            double topZ = TopLevel.Elevation;

            if (!GeometrySettings.AreEqual(opening.Line.StartPoint.Z, opening.Line.EndPoint.Z))
            {
                throw new ArgumentException("Opening line Z coordinates must be equal (opening must be horizontal).");
            }

            if (GeometrySettings.IsLessThan(openingZ, botZ) || GeometrySettings.IsGreaterThan(openingZ, topZ))
            {
                throw new ArgumentException(
                    $"Opening Z ({openingZ}) must be between wall bot ({botZ}) and top ({topZ}) elevation.");
            }
        }

        private void ValidateOpeningHeight(WallOpening opening)
        {
            double openingTopElevation = opening.Line.StartPoint.Z + opening.Height;
            double botZ = BotLevel.Elevation;
            double topZ = TopLevel.Elevation;

            if (GeometrySettings.IsLessThan(openingTopElevation, botZ) || GeometrySettings.IsGreaterThan(openingTopElevation, topZ))
            {
                throw new ArgumentException(
                    $"Opening top elevation ({openingTopElevation}) must be between wall bot ({botZ}) and top ({topZ}) elevation.");
            }
        }
    }
}
