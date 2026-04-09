using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public abstract class WallOpening : IHavePoints
    {
        public Guid Id { get; private set; }
        public Guid WallId { get; private set; }

        /// <summary>
        /// Height from the bottom of the opening to the top of the opening.
        /// </summary>
        public double Height { get; private set; }

        /// <summary>
        /// Line in global coordinates. Must lie on the parent wall's line (2D).
        /// Z coordinates define the bottom elevation of the opening.
        /// </summary>
        public LineSegment Line { get; private set; }

        internal WallOpening(Guid wallId, double height, LineSegment line)
        {
            Id = Guid.NewGuid();
            WallId = wallId;
            Height = height;
            Line = line;
        }

        public IEnumerable<Point> GetPoints()
        {
            yield return Line.StartPoint;
            yield return Line.EndPoint;
        }
    }
}
