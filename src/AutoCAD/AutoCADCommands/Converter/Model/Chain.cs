using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model
{
    /// <summary>
    /// A single entry in a chain: the tagged rectangle, its original index in its
    /// source list, and whether it's an opening (window/door) or a wall.
    /// </summary>
    public readonly struct ChainEntry(TaggedRect element, int originalIndex, bool isOpening)
    {
        public TaggedRect Element { get; } = element;
        public int OriginalIndex { get; } = originalIndex;
        public bool IsOpening { get; } = isOpening;
    }

    /// <summary>
    /// A straight-line alternating sequence of walls and openings along a single axis.
    /// Always starts and ends with a wall (when fully built).
    /// </summary>
    public class Chain
    {
        public Vec2 Direction { get; set; }
        public List<ChainEntry> Elements { get; set; } = new();
    }
}
