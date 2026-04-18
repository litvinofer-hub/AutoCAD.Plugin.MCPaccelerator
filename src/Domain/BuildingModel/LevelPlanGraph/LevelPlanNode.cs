using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class LevelPlanNode
    {
        public Guid Id { get; }
        public Point Point { get; }

        public Guid? NorthEdgeId { get; set; }
        public Guid? SouthEdgeId { get; set; }
        public Guid? WestEdgeId { get; set; }
        public Guid? EastEdgeId { get; set; }

        public LevelPlanNode(Point point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));

            Id = Guid.NewGuid();
            Point = point;
        }

        public LevelPlanNode(double x, double y, double z) : this(new Point(x, y, z)) { }

        public LevelPlanNode(double x, double y) : this(new Point(x, y, 0)) { }

        /// <summary>
        /// Returns the non-null directional edge ids in N, S, W, E order.
        /// </summary>
        public IReadOnlyList<Guid> EdgesIds
        {
            get
            {
                var list = new List<Guid>(4);
                if (NorthEdgeId.HasValue) list.Add(NorthEdgeId.Value);
                if (SouthEdgeId.HasValue) list.Add(SouthEdgeId.Value);
                if (WestEdgeId.HasValue)  list.Add(WestEdgeId.Value);
                if (EastEdgeId.HasValue)  list.Add(EastEdgeId.Value);
                return list;
            }
        }

        /// <summary>
        /// Equality is based solely on the Point (matches Python __eq__).
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is LevelPlanNode other)
                return Point.Equals(other.Point);
            return false;
        }

        /// <summary>
        /// Hash is based solely on the Point (matches Python __hash__).
        /// </summary>
        public override int GetHashCode() => Point.GetHashCode();

        public override string ToString() => Point.ToString();
    }
}
