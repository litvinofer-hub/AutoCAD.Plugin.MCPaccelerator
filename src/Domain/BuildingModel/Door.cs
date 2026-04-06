using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Door : Opening
    {
        internal Door(Guid wallId, double height, LineSegment line)
            : base(wallId, height, line)
        {
        }
    }
}
