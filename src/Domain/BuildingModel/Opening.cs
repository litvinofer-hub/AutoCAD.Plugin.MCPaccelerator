using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public abstract class Opening
    {
        public Guid Id { get; set; }
        public Guid WallId { get; set; }

        /// <summary>
        /// Height from the bottom of the opening to the top of the opening.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Line in global coordinates. Must lie on the parent wall's line (2D).
        /// Z coordinates define the bottom elevation of the opening.
        /// </summary>
        public LineSegment Line { get; set; }

        protected Opening(Guid wallId, double height, LineSegment line)
        {
            Id = Guid.NewGuid();
            WallId = wallId;
            Height = height;
            Line = line;
        }
    }
}
