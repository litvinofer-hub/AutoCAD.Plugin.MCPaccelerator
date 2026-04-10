using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Wall : IHavePoints
    {
        public Guid Id { get; private set; }
        public Guid BuildingId { get; private set; }

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

        public Wall(Guid buildingId, LineSegment botLine, double thickness, Level topLevel, IReadOnlyList<Level> buildingLevels)
        {
            double lineZ = GetAndValidateLineZ(botLine);
            Level botLevel = FindMatchingLevel(lineZ, buildingLevels);

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            BotLine = botLine;
            Thickness = thickness;
            BotLevel = botLevel;
            TopLevel = topLevel;
            _openings = new List<WallOpening>();
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
