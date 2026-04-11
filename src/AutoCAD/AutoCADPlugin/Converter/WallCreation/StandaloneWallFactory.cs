using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.WallCreation
{
    /// <summary>
    /// Creates a <see cref="ConvertedWall"/> for a rectangle that was not consumed
    /// by any chain (i.e. has no adjacent opening). Pure — no <c>Building</c>
    /// or <c>Story</c> dependency.
    ///
    /// Assumptions:
    /// - A standalone wall always has length &gt; thickness, so its cached
    ///   <see cref="MCPAccelerator.Utils.GeometryModel.Rect.CenterLineStart2D"/> /
    ///   <see cref="MCPAccelerator.Utils.GeometryModel.Rect.CenterLineEnd2D"/> /
    ///   <see cref="MCPAccelerator.Utils.GeometryModel.Rect.Thickness2D"/> are
    ///   reliable. Short "stub" walls never reach this path because they always
    ///   flank an opening and therefore belong to a chain.
    /// </summary>
    public static class StandaloneWallFactory
    {
        /// <summary>
        /// Returns a <see cref="ConvertedWall"/> from the rectangle's cached center
        /// line and thickness. The resulting wall has no openings.
        /// </summary>
        public static ConvertedWall Create(TaggedRect element)
        {
            var rect = element.Rect;
            return new ConvertedWall(
                rect.CenterLineStart2D.X, rect.CenterLineStart2D.Y,
                rect.CenterLineEnd2D.X, rect.CenterLineEnd2D.Y,
                rect.Thickness2D);
        }
    }
}
