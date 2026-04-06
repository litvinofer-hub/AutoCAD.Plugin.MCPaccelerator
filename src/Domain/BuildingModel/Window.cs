using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Window : Opening
    {
        internal Window(Guid wallId, double height, LineSegment line)
            : base(wallId, height, line)
        {
        }
    }
}
