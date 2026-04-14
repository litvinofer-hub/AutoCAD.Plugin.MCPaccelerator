using System;
using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    /// <summary>
    /// Symbol labeling strategy for axis lines.
    /// </summary>
    public enum AxisSymbolType
    {
        Numbers,
        LowerCase,
        UpperCase
    }

    /// <summary>
    /// Represents one set of parallel axis lines for a <see cref="Story"/>.
    /// Stores the geometric definition (direction, perpendicular positions,
    /// extent) and the labeling strategy — everything needed to re-create the
    /// axes without AutoCAD types.
    ///
    /// A story may have multiple axial systems (e.g. one for X-direction walls
    /// and one for Y-direction walls).
    /// </summary>
    public class AxialSystem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid StoryId { get; }

        /// <summary>Normalized direction the axis lines run along.</summary>
        public Vec2 Direction { get; }

        /// <summary>The perpendicular direction used to position each axis.</summary>
        public Vec2 PerpDirection { get; }

        /// <summary>How the axes are labeled (1,2,3 / a,b,c / A,B,C).</summary>
        public AxisSymbolType SymbolType { get; }

        /// <summary>
        /// Sorted list of unique perpendicular positions — one per axis line.
        /// Each value is the dot product of a wall midpoint with
        /// <see cref="PerpDirection"/>.
        /// </summary>
        public IReadOnlyList<double> Positions { get; }

        /// <summary>Start of the axis lines along <see cref="Direction"/>.</summary>
        public double LineStart { get; }

        /// <summary>End of the axis lines along <see cref="Direction"/>.</summary>
        public double LineEnd { get; }

        /// <summary>Radius of the bubble circles at each end of the axis lines.</summary>
        public double BubbleRadius { get; }

        public AxialSystem(
            Guid storyId,
            Vec2 direction, Vec2 perpDirection,
            AxisSymbolType symbolType,
            IReadOnlyList<double> positions,
            double lineStart, double lineEnd,
            double bubbleRadius)
        {
            StoryId = storyId;
            Direction = direction;
            PerpDirection = perpDirection;
            SymbolType = symbolType;
            Positions = positions;
            LineStart = lineStart;
            LineEnd = lineEnd;
            BubbleRadius = bubbleRadius;
        }

        /// <summary>
        /// Returns the label string for the axis at the given index.
        /// </summary>
        public string GetSymbol(int index)
        {
            return SymbolType switch
            {
                AxisSymbolType.Numbers   => (index + 1).ToString(),
                AxisSymbolType.LowerCase => ((char)('a' + index)).ToString(),
                AxisSymbolType.UpperCase => ((char)('A' + index)).ToString(),
                _                        => (index + 1).ToString()
            };
        }
    }
}
