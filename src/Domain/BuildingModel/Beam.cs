using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public enum BeamType
    {
        UNKNOWN,
        BM,  // beam
        TBM  // transfer beam
    }

    /// <summary>
    /// Represents a beam element. Unlike <see cref="Wall"/> — which is defined
    /// by its <b>bottom</b> line — a beam is defined by its <b>top</b> line
    /// (<see cref="TopLine"/>) because that is what registers in the level
    /// plan graph above. The beam hangs <see cref="Height"/> below TopLine;
    /// its bottom is stored as <see cref="BotSubLevel"/>, a SubLevel on
    /// <see cref="TopLevel"/> with negative offset.
    /// </summary>
    public class Beam : IHavePoints
    {
        public Guid Id { get; private set; }
        public Guid BuildingId { get; private set; }

        /// <summary>
        /// Id of the story this beam belongs to. Beams cannot exist without a
        /// story.
        /// </summary>
        public Guid StoryId { get; private set; }

        /// <summary>
        /// 2D line at the top elevation. Z coordinates match TopLevel.Elevation.
        /// </summary>
        public LineSegment TopLine { get; private set; }
        public double Thickness { get; private set; }

        /// <summary>Level matching TopLine.Z.</summary>
        public Level TopLevel { get; private set; }

        /// <summary>
        /// SubLevel on <see cref="TopLevel"/> describing the beam's bottom as
        /// a negative offset below TopLevel.Elevation.
        /// </summary>
        public SubLevel BotSubLevel { get; private set; }

        public BeamType Type { get; set; } = BeamType.BM;

        /// <summary>Beam height (always positive). Derived from BotSubLevel.Offset.</summary>
        public double Height => -BotSubLevel.Offset;

        public Beam(Guid buildingId, LineSegment topLine, double thickness, double height,
            IReadOnlyList<Level> buildingLevels, Guid storyId)
        {
            double lineZ = GetAndValidateLineZ(topLine);
            Level topLevel = FindMatchingLevel(lineZ, buildingLevels);

            if (storyId == Guid.Empty)
                throw new ArgumentException("Beam must belong to a story (storyId cannot be empty).", nameof(storyId));

            Id = Guid.NewGuid();
            BuildingId = buildingId;
            StoryId = storyId;
            TopLine = topLine;
            Thickness = thickness;
            TopLevel = topLevel;
            BotSubLevel = topLevel.GetOrAddSubLevel(-height);
        }

        public IEnumerable<Point> GetPoints()
        {
            yield return TopLine.StartPoint;
            yield return TopLine.EndPoint;
        }

        /// <summary>
        /// Validates that both endpoints of the line have the same Z coordinate.
        /// Returns that common Z value.
        /// </summary>
        private static double GetAndValidateLineZ(LineSegment line)
        {
            if (!GeometrySettings.AreEqual(line.StartPoint.Z, line.EndPoint.Z))
            {
                throw new ArgumentException("Beam line Z coordinates must be equal.");
            }

            return line.StartPoint.Z;
        }

        private static Level FindMatchingLevel(double z, IReadOnlyList<Level> levels)
        {
            var level = levels.FirstOrDefault(l => GeometrySettings.AreEqual(l.Elevation, z));

            if (level == null)
            {
                throw new ArgumentException(
                    $"No Level found with elevation matching beam line Z ({z}). " +
                    "Register the Level in the Building before creating the Beam.");
            }

            return level;
        }
    }
}
