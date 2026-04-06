using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Void : Opening
    {
        internal Void(Guid wallId, double height, LineSegment line)
            : base(wallId, height, line)
        {
        }
    }
}
