using System;
using System.Collections.Generic;

namespace MCPAccelerator.Utils.GeometryModel.FloorGraph
{
    public class FloorGraph
    {
        public Dictionary<Guid, FloorNode> Nodes { get; }
        public Dictionary<Guid, FloorEdge> Edges { get; }

        public FloorGraph(
            Dictionary<Guid, FloorNode> nodes = null,
            Dictionary<Guid, FloorEdge> edges = null)
        {
            Nodes = nodes ?? new Dictionary<Guid, FloorNode>();
            Edges = edges ?? new Dictionary<Guid, FloorEdge>();
        }

        /// <summary>
        /// Creates a new orthogonal edge between two nodes, wires the
        /// reciprocal N/S/E/W edge-id fields on each node, and registers
        /// both the nodes and the edge in the graph.
        /// </summary>
        public FloorEdge AddEdge(FloorNode node1, FloorNode node2, FloorEdgeType type = FloorEdgeType.UNKNOWN)
        {
            var edge = new FloorEdge(node1, node2, type);

            if (!Nodes.ContainsKey(node1.Id)) Nodes[node1.Id] = node1;
            if (!Nodes.ContainsKey(node2.Id)) Nodes[node2.Id] = node2;

            (Direction dir1To2, Direction dir2To1) = DetermineDirections(node1.Point, node2.Point);

            AssignEdgeToNode(node1, edge.Id, dir1To2);
            AssignEdgeToNode(node2, edge.Id, dir2To1);

            Edges[edge.Id] = edge;
            return edge;
        }

        private static (Direction from1To2, Direction from2To1) DetermineDirections(Point p1, Point p2)
        {
            bool sameX = Math.Abs(p1.X - p2.X) < GeometrySettings.Tolerance;

            if (sameX)
            {
                // Vertical edge: differ in Y only.
                if (p2.Y > p1.Y)
                    return (Direction.North, Direction.South);
                return (Direction.South, Direction.North);
            }

            // Horizontal edge: differ in X only (orthogonality enforced by FloorEdge ctor).
            if (p2.X > p1.X)
                return (Direction.East, Direction.West);
            return (Direction.West, Direction.East);
        }

        private static void AssignEdgeToNode(FloorNode node, Guid edgeId, Direction direction)
        {
            switch (direction)
            {
                case Direction.North: node.NorthEdgeId = edgeId; break;
                case Direction.South: node.SouthEdgeId = edgeId; break;
                case Direction.East:  node.EastEdgeId  = edgeId; break;
                case Direction.West:  node.WestEdgeId  = edgeId; break;
            }
        }

        private enum Direction
        {
            North,
            South,
            East,
            West
        }
    }
}
