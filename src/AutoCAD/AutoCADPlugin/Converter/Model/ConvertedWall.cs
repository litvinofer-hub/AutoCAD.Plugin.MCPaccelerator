using System.Collections.Generic;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model
{
    /// <summary>
    /// A wall produced by <c>FloorPlanConverter.Convert</c>, described only in 2D
    /// (no <c>Building</c> / <c>Story</c> / <c>Level</c> context). Intended to be
    /// fed into <c>FloorPlanConverter.Apply</c>, which attaches it to a concrete
    /// building and story.
    ///
    /// Contains:
    /// - The wall's centerline as a pair of 2D endpoints (X1,Y1) → (X2,Y2).
    /// - The wall's thickness (Length of the short side of the original rectangle
    ///   for standalone walls, or the chain's row thickness for merged walls).
    /// - A list of openings sitting on this wall, each described as a type (Window/Door)
    ///   and a 2D line.
    ///
    /// No vertical information (elevation, sill height, opening height) — that comes
    /// from the building's <c>UnitSystem</c> at apply time.
    /// </summary>
    public class ConvertedWall(double x1, double y1, double x2, double y2, double thickness)
    {
        public double X1 { get; } = x1;
        public double Y1 { get; } = y1;
        public double X2 { get; } = x2;
        public double Y2 { get; } = y2;
        public double Thickness { get; } = thickness;
        public List<ConvertedOpening> Openings { get; } = [];
    }

    /// <summary>
    /// A window or door produced by <c>FloorPlanConverter.Convert</c>, described
    /// only in 2D and tagged with its type. Hosted on a <see cref="ConvertedWall"/>.
    /// </summary>
    public class ConvertedOpening(ElementType type, double x1, double y1, double x2, double y2)
    {
        public ElementType Type { get; } = type;
        public double X1 { get; } = x1;
        public double Y1 { get; } = y1;
        public double X2 { get; } = x2;
        public double Y2 { get; } = y2;
    }
}
