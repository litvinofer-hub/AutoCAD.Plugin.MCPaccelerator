using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Void : Opening
    {
        public Void(Guid wallId, double height, LineSegment line)
            : base(wallId, height, line)
        {
        }
    }
}
