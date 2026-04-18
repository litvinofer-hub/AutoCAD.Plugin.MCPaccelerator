using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public enum Direction
    {
        North,
        South,
        East,
        West
    }

    public static class DirectionExtensions
    {
        /// <summary>
        /// Returns the opposite direction (N↔S, E↔W).
        /// </summary>
        public static Direction Opposite(this Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return Direction.South;
                case Direction.South: return Direction.North;
                case Direction.West:  return Direction.East;
                case Direction.East:  return Direction.West;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }

    /// <summary>
    /// Orthogonal plan graph owned by a <see cref="Level"/>. Each edge represents
    /// the top middle-line of a wall or beam whose top elevation equals this
    /// level's elevation — i.e. the graph at level L describes what stands
    /// <b>below</b> L (the ceiling of the story below / the surface you walk on at L).
    ///
    /// Nodes live at L.Elevation; edges are axis-aligned (endpoints share X or Y).
    /// Each edge may carry a <see cref="LevelPlanEdge.ElementId"/> pointing back
    /// to the <see cref="Wall"/> or Beam it represents, disambiguated by
    /// <see cref="LevelPlanEdge.Type"/>.
    ///
    /// The graph enforces point uniqueness: calling <see cref="GetOrAddNode"/>
    /// with an already-registered <see cref="Point"/> returns the existing node,
    /// so walls, beams, and edges referencing the same XY at the same elevation
    /// share a single node instance.
    /// </summary>
    public class LevelPlanGraph
    {
        public Dictionary<Guid, LevelPlanNode> Nodes { get; }
        public Dictionary<Guid, LevelPlanEdge> Edges { get; }

        // Secondary index for O(1) dedup by point coordinates.
        private readonly Dictionary<Point, LevelPlanNode> _nodesByPoint;

        public LevelPlanGraph(
            Dictionary<Guid, LevelPlanNode> nodes = null,
            Dictionary<Guid, LevelPlanEdge> edges = null)
        {
            Nodes = nodes ?? new Dictionary<Guid, LevelPlanNode>();
            Edges = edges ?? new Dictionary<Guid, LevelPlanEdge>();

            _nodesByPoint = new Dictionary<Point, LevelPlanNode>();
            foreach (var node in Nodes.Values)
                _nodesByPoint[node.Point] = node;
        }

        /// <summary>
        /// Returns the existing node whose Point matches (within tolerance), or
        /// creates, registers, and returns a new node.
        /// </summary>
        public LevelPlanNode GetOrAddNode(Point point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));

            if (_nodesByPoint.TryGetValue(point, out var existing))
                return existing;

            var node = new LevelPlanNode(point);
            Nodes[node.Id] = node;
            _nodesByPoint[point] = node;
            return node;
        }

        /// <summary>
        /// Creates (or reuses) nodes for the two points and adds an orthogonal
        /// edge between them. Sets the reciprocal N/S/E/W edge-id fields on
        /// each node. Preferred entry point — guarantees point-based dedup.
        /// </summary>
        public LevelPlanEdge AddEdge(Point p1, Point p2)
        {
            var n1 = GetOrAddNode(p1);
            var n2 = GetOrAddNode(p2);
            return AddEdge(n1, n2);
        }

        /// <summary>
        /// Adds an orthogonal edge between two nodes. If the nodes are not yet
        /// part of the graph they are registered. Sets the reciprocal N/S/E/W
        /// edge-id fields on each node.
        /// </summary>
        public LevelPlanEdge AddEdge(LevelPlanNode node1, LevelPlanNode node2)
        {
            var edge = new LevelPlanEdge(node1, node2);

            if (!Nodes.ContainsKey(node1.Id))
            {
                Nodes[node1.Id] = node1;
                _nodesByPoint[node1.Point] = node1;
            }
            if (!Nodes.ContainsKey(node2.Id))
            {
                Nodes[node2.Id] = node2;
                _nodesByPoint[node2.Point] = node2;
            }

            Direction dir1To2 = DetermineDirection(node1.Point, node2.Point);

            AssignEdgeToNode(node1, edge.Id, dir1To2);
            AssignEdgeToNode(node2, edge.Id, dir1To2.Opposite());

            Edges[edge.Id] = edge;
            return edge;
        }

        private static Direction DetermineDirection(Point from, Point to)
        {
            bool sameX = Math.Abs(from.X - to.X) < GeometrySettings.Tolerance;

            if (sameX)
            {
                // Vertical edge: differ in Y only.
                return to.Y > from.Y ? Direction.North : Direction.South;
            }

            // Horizontal edge: differ in X only (orthogonality enforced by LevelPlanEdge ctor).
            return to.X > from.X ? Direction.East : Direction.West;
        }

        private static void AssignEdgeToNode(LevelPlanNode node, Guid edgeId, Direction direction)
        {
            switch (direction)
            {
                case Direction.North: node.NorthEdgeId = edgeId; break;
                case Direction.South: node.SouthEdgeId = edgeId; break;
                case Direction.East:  node.EastEdgeId  = edgeId; break;
                case Direction.West:  node.WestEdgeId  = edgeId; break;
            }
        }
    }
}
