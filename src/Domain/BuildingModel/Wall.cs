using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Wall
    {
        public Guid Id { get; set; }
        public Guid BuildingId { get; set; }

        /// <summary>
        /// 2D line (X, Y). Z coordinates must equal BotLevel.Elevation.
        /// </summary>
        public LineSegment LineSegment { get; set; }
        public double Thickness { get; set; }
        public Level BotLevel { get; set; }
        public Level TopLevel { get; set; }
        public List<Opening> Openings { get; set; }

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        public Wall(Guid buildingId, LineSegment line, double thickness, Level botLevel, Level topLevel)
        {
            ValidateLineZ(line, botLevel);

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            LineSegment = line;
            Thickness = thickness;
            BotLevel = botLevel;
            TopLevel = topLevel;
            Openings = new List<Opening>();
        }

        /// <summary>
        /// Adds an opening to the wall. Validates that the opening line is within
        /// the wall line, the opening line Z matches the wall's BotLevel,
        /// and the opening (SillHeight + Height) does not exceed the wall height.
        /// </summary>
        public void AddOpening(Opening opening)
        {
            ValidateOpeningLineZ(opening);
            ValidateOpeningWithinWallLine(opening);
            ValidateOpeningHeight(opening);

            opening.WallId = Id;
            Openings.Add(opening);
        }

        private static void ValidateLineZ(LineSegment line, Level botLevel)
        {
            if (!GeometrySettings.AreEqual(line.StartPoint.Z, botLevel.Elevation) ||
                !GeometrySettings.AreEqual(line.EndPoint.Z, botLevel.Elevation))
            {
                throw new ArgumentException("Wall line Z coordinates must equal BotLevel elevation.");
            }
        }

        private void ValidateOpeningLineZ(Opening opening)
        {
            double openingZ = opening.Line.StartPoint.Z;

            if (!GeometrySettings.AreEqual(opening.Line.StartPoint.Z, opening.Line.EndPoint.Z))
            {
                throw new ArgumentException("Opening line Z coordinates must be equal (opening must be horizontal).");
            }

            if (GeometrySettings.IsLessThan(openingZ, BotLevel.Elevation))
            {
                throw new ArgumentException("Opening bottom cannot be below the wall's BotLevel elevation.");
            }
        }

        private void ValidateOpeningWithinWallLine(Opening opening)
        {
            if (!LineSegment.IsPointOnSegment2D(opening.Line.StartPoint) ||
                !LineSegment.IsPointOnSegment2D(opening.Line.EndPoint))
            {
                throw new ArgumentException("Opening line must be within the wall line.");
            }
        }

        private void ValidateOpeningHeight(Opening opening)
        {
            double openingTopElevation = opening.Line.StartPoint.Z + opening.Height;
            double wallTopElevation = TopLevel.Elevation;

            if (GeometrySettings.IsGreaterThan(openingTopElevation, wallTopElevation))
            {
                throw new ArgumentException(
                    $"Opening top elevation ({openingTopElevation}) exceeds wall top elevation ({wallTopElevation}).");
            }
        }
    }
}
