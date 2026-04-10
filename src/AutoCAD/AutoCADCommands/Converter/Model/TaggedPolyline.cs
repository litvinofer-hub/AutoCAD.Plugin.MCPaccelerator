using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model
{
    /// <summary>
    /// Floor-plan role of a polyline. This is domain knowledge (not geometry),
    /// so it lives in the Converter, not in <c>GeometryModel</c>.
    /// </summary>
    public enum ElementType
    {
        Wall,
        Window,
        Door
    }

    /// <summary>
    /// Pairs a pure-geometry <see cref="Polyline"/> with its floor-plan role.
    /// All geometric operations go through <see cref="Polyline"/>'s 2D methods;
    /// this struct exists only to carry the <see cref="ElementType"/> tag.
    /// </summary>
    public readonly struct TaggedPolyline(Polyline polyline, ElementType type)
    {
        public Polyline Polyline { get; } = polyline;
        public ElementType Type { get; } = type;
    }
}
