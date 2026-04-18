using System;

namespace MCPAccelerator.Utils.GeometryModel.FloorGraph
{
    public class FloorNode
    {
        public Guid Id { get; }
        public Point Point { get; }

        public Guid? NorthEdgeId { get; set; }
        public Guid? SouthEdgeId { get; set; }
        public Guid? WestEdgeId { get; set; }
        public Guid? EastEdgeId { get; set; }

        public FloorNode(Point point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));

            Id = Guid.NewGuid();
            Point = point;
        }
    }
}
