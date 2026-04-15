using System;
using System.Collections.Generic;
using System.Linq;
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
    /// A single axis line with its label and geometry.
    /// </summary>
    public class AxialLine
    {
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Display label for this axis (e.g. "A", "B", "1", "2", "a", "b").
        /// Updated when lines are re-ordered after a deletion.
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// The geometric line segment of this axis on the floor plan (Z = 0).
        /// </summary>
        public LineSegment Line { get; }

        public AxialLine(string symbol, LineSegment line)
        {
            Symbol = symbol;
            Line = line;
        }
    }

    /// <summary>
    /// One direction of parallel axis lines within an <see cref="AxialSystem"/>.
    /// For example, all X-direction axes labeled A, B, C or all Y-direction
    /// axes labeled 1, 2, 3.
    ///
    /// <see cref="AxialLines"/> is kept sorted by perpendicular position so
    /// that index order matches the visual left-to-right / bottom-to-top order.
    /// </summary>
    public class AxialSystemDirection
    {
        /// <summary>Normalized direction the axis lines run along.</summary>
        public Vec2 Direction { get; }

        /// <summary>How the axes are labeled (1,2,3 / a,b,c / A,B,C).</summary>
        public AxisSymbolType SymbolType { get; }

        /// <summary>
        /// Axis lines in this direction, sorted by perpendicular position.
        /// </summary>
        public IReadOnlyList<AxialLine> AxialLines => _axialLines.AsReadOnly();
        private readonly List<AxialLine> _axialLines;

        public AxialSystemDirection(Vec2 direction, AxisSymbolType symbolType,
            List<AxialLine> axialLines)
        {
            Direction = direction;
            SymbolType = symbolType;
            _axialLines = axialLines;
        }

        /// <summary>
        /// Removes the axis line at the given index and re-labels all
        /// subsequent lines so symbols stay sequential
        /// (e.g. A,B,C,D -> remove C -> A,B,D becomes A,B,C).
        /// </summary>
        public void RemoveAxialLine(int index)
        {
            if (index < 0 || index >= _axialLines.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _axialLines.RemoveAt(index);

            // Re-label from the removed index onward
            for (int i = index; i < _axialLines.Count; i++)
                _axialLines[i].Symbol = GetSymbol(i);
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

        /// <summary>
        /// Returns true if this direction is parallel to the given direction
        /// (same or opposite vector).
        /// </summary>
        public bool IsParallelTo(Vec2 other)
        {
            double cross = Direction.X * other.Y - Direction.Y * other.X;
            return GeometrySettings.AreEqual(cross, 0);
        }
    }

    /// <summary>
    /// The axial system for a single <see cref="Story"/>. Contains one or more
    /// <see cref="AxialSystemDirection"/> instances (e.g. X-direction and
    /// Y-direction axes). Two directions with the same (or parallel) vector
    /// are not allowed in the same system.
    /// </summary>
    public class AxialSystem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid StoryId { get; }

        /// <summary>
        /// Directions in this axial system. Each direction holds its own
        /// list of parallel axis lines.
        /// </summary>
        public IReadOnlyList<AxialSystemDirection> Directions => _directions.AsReadOnly();
        private readonly List<AxialSystemDirection> _directions = [];

        /// <summary>Radius of the bubble circles at each end of every axis line.</summary>
        public double BubbleRadius { get; }

        public AxialSystem(Guid storyId, double bubbleRadius)
        {
            StoryId = storyId;
            BubbleRadius = bubbleRadius;
        }

        /// <summary>
        /// Adds a direction to this axial system.
        /// Throws if a parallel direction already exists.
        /// </summary>
        public void AddDirection(AxialSystemDirection direction)
        {
            if (_directions.Any(d => d.IsParallelTo(direction.Direction)))
                throw new InvalidOperationException(
                    "A direction parallel to the given vector already exists in this axial system.");

            _directions.Add(direction);
        }

        /// <summary>
        /// Removes a direction from this axial system.
        /// </summary>
        public bool RemoveDirection(AxialSystemDirection direction)
        {
            return _directions.Remove(direction);
        }

        /// <summary>
        /// Finds the direction that is parallel to the given vector, or null.
        /// </summary>
        public AxialSystemDirection FindDirection(Vec2 direction)
        {
            return _directions.FirstOrDefault(d => d.IsParallelTo(direction));
        }
    }
}
