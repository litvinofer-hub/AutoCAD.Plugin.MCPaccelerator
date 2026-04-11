using System.Collections.Generic;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model
{
    /// <summary>
    /// A connected component of walls + openings that will be merged into one
    /// <see cref="MCPAccelerator.Domain.BuildingModel.Wall"/> with its openings.
    /// Built by <c>ChainBuilder.Build</c>; consumed by <c>ChainWallFactory.Create</c>.
    ///
    /// Layout:
    /// - <see cref="Direction"/> is the chain's long axis (taken from one of the
    ///   openings, since openings always have length &gt; thickness so their
    ///   <see cref="Rect.Direction2D"/> is reliable).
    /// - <see cref="Walls"/> and <see cref="Openings"/> are both sorted along
    ///   <see cref="Direction"/>.
    /// - <see cref="WallIndices"/> are the original positions of the walls in the
    ///   list passed to ChainBuilder, so the converter's standalone-wall pass can
    ///   skip walls already consumed by a chain.
    /// </summary>
    public class Chain(
        Vec2 direction,
        List<TaggedRect> walls,
        List<TaggedRect> openings,
        List<int> wallIndices)
    {
        public Vec2 Direction { get; } = direction;
        public List<TaggedRect> Walls { get; } = walls;
        public List<TaggedRect> Openings { get; } = openings;
        public List<int> WallIndices { get; } = wallIndices;
    }
}
