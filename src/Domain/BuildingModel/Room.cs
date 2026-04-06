using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Room : IHavePoints
    {
        public Guid Id { get; set; }
        public Guid BuildingId { get; set; }

        /// <summary>
        /// 2D polygon (X, Y). Z coordinates must equal BotLevel.Elevation.
        /// </summary>
        public Polygon Polygon { get; set; }
        public Level BotLevel { get; set; }
        public Level TopLevel { get; set; }

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        public Room(Guid buildingId, Polygon polygon, Level botLevel, Level topLevel)
        {
            ValidatePolygonZ(polygon, botLevel);

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            Polygon = polygon;
            BotLevel = botLevel;
            TopLevel = topLevel;
        }

        public IEnumerable<Point> GetPoints()
        {
            var points = Polygon.Points;
            int count = points.Count;

            // Skip the closing point (same reference as first)
            if (count > 1 && ReferenceEquals(points[0], points[count - 1]))
                count--;

            for (int i = 0; i < count; i++)
                yield return points[i];
        }

        private static void ValidatePolygonZ(Polygon polygon, Level botLevel)
        {
            if (polygon.Points.Any(p => !GeometrySettings.AreEqual(p.Z, botLevel.Elevation)))
            {
                throw new ArgumentException("Room polygon Z coordinates must equal BotLevel elevation.");
            }
        }
    }
}
