using System;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class LevelPlanEdge
    {
        public Guid Id { get; }
        public LevelPlanNode Node1 { get; }
        public LevelPlanNode Node2 { get; }

        /// <summary>
        /// Back-reference to the building element this edge represents — a
        /// <see cref="Wall"/> Id or <see cref="Beam"/> Id. Null when the edge
        /// has no element behind it yet (pure graph node).
        /// Structural type (LB/NB/BM/TBM/…) lives on the element itself
        /// (<see cref="Wall.Type"/>, <see cref="Beam.Type"/>), not on the edge.
        /// </summary>
        public Guid? ElementId { get; set; }

        public LevelPlanEdge(LevelPlanNode node1, LevelPlanNode node2)
        {
            if (node1 == null) throw new ArgumentNullException(nameof(node1));
            if (node2 == null) throw new ArgumentNullException(nameof(node2));

            bool sameX = Math.Abs(node1.Point.X - node2.Point.X) < GeometrySettings.Tolerance;
            bool sameY = Math.Abs(node1.Point.Y - node2.Point.Y) < GeometrySettings.Tolerance;

            if (sameX && sameY)
            {
                throw new ArgumentException(
                    "LevelPlanEdge requires two distinct points: node1 and node2 share both X and Y.");
            }

            if (!sameX && !sameY)
            {
                throw new ArgumentException(
                    "LevelPlanEdge must be orthogonal: node1 and node2 must share either X or Y coordinate.");
            }

            Id = Guid.NewGuid();
            Node1 = node1;
            Node2 = node2;
        }

        /// <summary>
        /// Equality is order-independent: edge(A, B) equals edge(B, A) when
        /// the endpoint points match.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is LevelPlanEdge other)
            {
                return (Node1.Point.Equals(other.Node1.Point) && Node2.Point.Equals(other.Node2.Point))
                    || (Node1.Point.Equals(other.Node2.Point) && Node2.Point.Equals(other.Node1.Point));
            }
            return false;
        }

        /// <summary>
        /// Hash is order-independent — combines the two endpoint hashes in a
        /// sorted (min/max) order so edge(A, B) and edge(B, A) hash identically.
        /// </summary>
        public override int GetHashCode()
        {
            int h1 = Node1.Point.GetHashCode();
            int h2 = Node2.Point.GetHashCode();
            int lo = Math.Min(h1, h2);
            int hi = Math.Max(h1, h2);
            unchecked
            {
                return lo * 31 + hi;
            }
        }

        public override string ToString()
        {
            return $"(({Math.Round(Node1.Point.X, 2)}, {Math.Round(Node1.Point.Y, 2)}, {Math.Round(Node1.Point.Z, 2)}), "
                 + $"({Math.Round(Node2.Point.X, 2)}, {Math.Round(Node2.Point.Y, 2)}, {Math.Round(Node2.Point.Z, 2)}))";
        }

        /// <summary>3D euclidean length between the two endpoint points.</summary>
        public double GetLength()
        {
            double dx = Node1.Point.X - Node2.Point.X;
            double dy = Node1.Point.Y - Node2.Point.Y;
            double dz = Node1.Point.Z - Node2.Point.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>2D length (X, Y only) between the two endpoint points.</summary>
        public double GetLengthXY() => Node1.Point.Distance2D(Node2.Point);

        /// <summary>Midpoint of the edge in 3D.</summary>
        public Point GetMidPoint()
        {
            double mx = (Node1.Point.X + Node2.Point.X) / 2.0;
            double my = (Node1.Point.Y + Node2.Point.Y) / 2.0;
            double mz = (Node1.Point.Z + Node2.Point.Z) / 2.0;
            return new Point(mx, my, mz);
        }

        /// <summary>
        /// True if the edge is parallel to the X axis (endpoints share Y within tolerance).
        /// </summary>
        public bool IsParallelToXAxis()
        {
            return Math.Abs(Node1.Point.Y - Node2.Point.Y) < GeometrySettings.Tolerance;
        }

        /// <summary>
        /// True if the edge is parallel to the Y axis (endpoints share X within tolerance).
        /// </summary>
        public bool IsParallelToYAxis()
        {
            return Math.Abs(Node1.Point.X - Node2.Point.X) < GeometrySettings.Tolerance;
        }
    }
}
