using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter.Model
{
    /// <summary>
    /// Floor-plan role of a rectangle. This is domain knowledge (not geometry),
    /// so it lives in the Converter, not in <c>GeometryModel</c>.
    /// </summary>
    public enum ElementType
    {
        Wall,
        Window,
        Door
    }

    /// <summary>
    /// Pairs a pure-geometry <see cref="Rect"/> with its floor-plan role.
    /// All geometric operations go through <see cref="Rect"/>'s properties
    /// and inherited <see cref="Polyline"/> 2D methods; this struct exists
    /// only to carry the <see cref="ElementType"/> tag.
    /// </summary>
    public readonly struct TaggedRect(Rect rect, ElementType type)
    {
        public Rect Rect { get; } = rect;
        public ElementType Type { get; } = type;
    }
}
