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
        /// 2D polygon at the bottom elevation. Z coordinates match BotLevel.Elevation.
        /// </summary>
        public Polygon BotPolygon { get; set; }

        /// <summary>
        /// Derived from the polygon's Z coordinate. Returns the Level whose elevation matches.
        /// </summary>
        public Level BotLevel { get; private set; }
        public Level TopLevel { get; set; }

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        public Room(Guid buildingId, Polygon polygon, Level topLevel, IReadOnlyList<Level> buildingLevels)
        {
            double polygonZ = GetAndValidatePolygonZ(polygon);
            Level botLevel = FindMatchingLevel(polygonZ, buildingLevels);

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            BotPolygon = polygon;
            BotLevel = botLevel;
            TopLevel = topLevel;
        }

        public IEnumerable<Point> GetPoints()
        {
            var points = BotPolygon.Points;
            int count = points.Count;

            // Skip the closing point (same reference as first)
            if (count > 1 && ReferenceEquals(points[0], points[count - 1]))
                count--;

            for (int i = 0; i < count; i++)
                yield return points[i];
        }

        /// <summary>
        /// Validates that all polygon points have the same Z coordinate.
        /// Returns that common Z value.
        /// </summary>
        private static double GetAndValidatePolygonZ(Polygon polygon)
        {
            double firstZ = polygon.Points[0].Z;

            if (polygon.Points.Any(p => !GeometrySettings.AreEqual(p.Z, firstZ)))
            {
                throw new ArgumentException("All polygon Z coordinates must be equal.");
            }

            return firstZ;
        }

        /// <summary>
        /// Finds a Level whose elevation matches the given Z value.
        /// Throws if no matching Level exists.
        /// </summary>
        private static Level FindMatchingLevel(double z, IReadOnlyList<Level> levels)
        {
            var level = levels.FirstOrDefault(l => GeometrySettings.AreEqual(l.Elevation, z));

            if (level == null)
            {
                throw new ArgumentException(
                    $"No Level found with elevation matching polygon Z ({z}). " +
                    "Register the Level in the Building before creating the Room.");
            }

            return level;
        }
    }
}
