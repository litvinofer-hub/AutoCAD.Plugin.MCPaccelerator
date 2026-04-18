using System;

namespace MCPAccelerator.Utils.GeometryModel.FloorGraph
{
    public enum FloorEdgeType
    {
        UNKNOWN,
        LB, // load bearing wall
        NB, // none bearing wall
        BM, // beam
        NW  // none wall
    }

    public class FloorEdge
    {
        public Guid Id { get; }
        public FloorNode Node1 { get; }
        public FloorNode Node2 { get; }
        public FloorEdgeType Type { get; set; }

        public FloorEdge(FloorNode node1, FloorNode node2, FloorEdgeType type = FloorEdgeType.UNKNOWN)
        {
            if (node1 == null) throw new ArgumentNullException(nameof(node1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));

            bool sameX = Math.Abs(node1.Point.X - node2.Point.X) < GeometrySettings.Tolerance;
            bool sameY = Math.Abs(node1.Point.Y - node2.Point.Y) < GeometrySettings.Tolerance;

            if (sameX && sameY)
            {
                throw new ArgumentException(
                    "FloorEdge requires two distinct points: node1 and node2 share both X and Y.");
            }

            if (!sameX && !sameY)
            {
                throw new ArgumentException(
                    "FloorEdge must be orthogonal: node1 and node2 must share either X or Y coordinate.");
            }

            Id = Guid.NewGuid();
            Node1 = node1;
            Node2 = node2;
            Type = type;
        }
    }
}
