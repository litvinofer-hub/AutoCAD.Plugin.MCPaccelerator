using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.Utils.GeometryModel;
using Xunit;
using Converter = MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.FloorPlanConverter;

namespace MCPAccelerator.Tests.FloorPlanConverter
{
    /// <summary>
    /// Tests for the pure <see cref="FloorPlanConverter.Convert(List{TaggedRect}, List{TaggedRect}, double)"/>
    /// overload. No Building, no Story, no AutoCAD. The test inputs are
    /// <see cref="TaggedRect"/>s built directly from corner coordinates, and the
    /// outputs are <see cref="ConvertedWall"/> DTOs whose X1/Y1/X2/Y2/Thickness
    /// fields are compared against the expected values.
    /// </summary>
    public class FloorPlanConverterTests
    {
        private const double TouchTolerance = GeometrySettings.Tolerance;

        [Fact]
        public void Convert_Input1_LCornerLegBecomesStandaloneWall()
        {
            // Two-wall L-corner (poly1 vertical leg + poly2 horizontal leg), then a
            // door (poly3), then a closing wall (poly4). Expected: poly1 stays a
            // standalone wall; poly2 + poly3 + poly4 merge into one wall with the
            // door inside.
            var walls = new List<TaggedRect>
            {
                MakeRect(ElementType.Wall, (0,     0),    (4.75,    0),    (4.75,    22.5), (0,    22.5)), // poly1
                MakeRect(ElementType.Wall, (4.75,  17.5), (17,      17.5), (17,      22.5), (4.75, 22.5)), // poly2
                MakeRect(ElementType.Wall, (49,    17.5), (120.355, 17.5), (120.355, 22.5), (49,   22.5)), // poly4
            };
            var openings = new List<TaggedRect>
            {
                MakeRect(ElementType.Door, (17, 17.5), (49, 17.5), (49, 22.5), (17, 22.5)),                // poly3
            };

            var result = Converter.Convert(walls, openings, TouchTolerance);

            Assert.Equal(2, result.Count);

            var standalone = result.Single(w => w.Openings.Count == 0);
            var merged     = result.Single(w => w.Openings.Count == 1);

            // wall1 (standalone L-leg): centerline (2.375,0)-(2.375,22.5), thickness 4.75
            AssertWall(standalone, 2.375, 0, 2.375, 22.5, 4.75);

            // wall2 (merged row): centerline (4.75,20)-(120.355,20), thickness 5
            AssertWall(merged, 4.75, 20, 120.355, 20, 5);

            // door inside wall2: (17,20)-(49,20)
            Assert.Single(merged.Openings);
            AssertOpening(merged.Openings[0], ElementType.Door, 17, 20, 49, 20);
        }

        [Fact]
        public void Convert_Input2_TallLLeg_DoesNotTouchRowWalls()
        {
            // Same as input1 but poly1 is now much taller (y=0..40) — its corners
            // at y=0 and y=40 are far from poly2's corners at y=17.5/22.5, so poly1
            // doesn't "touch" the row at all and is simply a standalone wall.
            var walls = new List<TaggedRect>
            {
                MakeRect(ElementType.Wall, (0,     0),    (4.75,    0),    (4.75,    40),   (0,    40)),   // poly1
                MakeRect(ElementType.Wall, (4.75,  17.5), (17,      17.5), (17,      22.5), (4.75, 22.5)), // poly2
                MakeRect(ElementType.Wall, (49,    17.5), (120.355, 17.5), (120.355, 22.5), (49,   22.5)), // poly4
            };
            var openings = new List<TaggedRect>
            {
                MakeRect(ElementType.Door, (17, 17.5), (49, 17.5), (49, 22.5), (17, 22.5)),                // poly3
            };

            var result = Converter.Convert(walls, openings, TouchTolerance);

            Assert.Equal(2, result.Count);

            var standalone = result.Single(w => w.Openings.Count == 0);
            var merged     = result.Single(w => w.Openings.Count == 1);

            // wall1 (standalone tall leg): centerline (2.375,0)-(2.375,40), thickness 4.75
            AssertWall(standalone, 2.375, 0, 2.375, 40, 4.75);

            // wall2 (merged row): centerline (4.75,20)-(120.355,20), thickness 5
            AssertWall(merged, 4.75, 20, 120.355, 20, 5);

            // door inside wall2: (17,20)-(49,20)
            Assert.Single(merged.Openings);
            AssertOpening(merged.Openings[0], ElementType.Door, 17, 20, 49, 20);
        }

        [Fact]
        public void Convert_Input3_TwoWindowsSharingStubWall_OneMergedWall()
        {
            // Linear sequence wall-window-stub-window-wall. The stub (poly3) is a
            // 2×4 rect — its architectural wall thickness (4) is its LONG side, so
            // Rect.Direction2D points perpendicular to the row. The chain builder
            // doesn't read wall directions so the stub is handled just like a
            // normal wall, and the whole row merges into one wall with two windows.
            var walls = new List<TaggedRect>
            {
                MakeRect(ElementType.Wall,   (0,  0), (15, 0), (15, 4), (0,  4)), // poly1
                MakeRect(ElementType.Wall,   (35, 0), (37, 0), (37, 4), (35, 4)), // poly3 (stub)
                MakeRect(ElementType.Wall,   (57, 0), (70, 0), (70, 4), (57, 4)), // poly5
            };
            var openings = new List<TaggedRect>
            {
                MakeRect(ElementType.Window, (15, 0), (35, 0), (35, 4), (15, 4)), // poly2
                MakeRect(ElementType.Window, (37, 0), (57, 0), (57, 4), (37, 4)), // poly4
            };

            var result = Converter.Convert(walls, openings, TouchTolerance);

            Assert.Single(result);
            var merged = result[0];

            // wall1 (whole row merged): centerline (0,2)-(70,2), thickness 4
            AssertWall(merged, 0, 2, 70, 2, 4);

            Assert.Equal(2, merged.Openings.Count);

            // Openings can come back in any order — match by x midpoint.
            var opByMidX = merged.Openings
                .OrderBy(o => 0.5 * (o.X1 + o.X2))
                .ToList();
            AssertOpening(opByMidX[0], ElementType.Window, 15, 2, 35, 2); // centered at x=25
            AssertOpening(opByMidX[1], ElementType.Window, 37, 2, 57, 2); // centered at x=47
        }

        // --- helpers ---

        /// <summary>
        /// Builds a <see cref="TaggedRect"/> from four 2D corners (Z=0).
        /// </summary>
        private static TaggedRect MakeRect(ElementType type, params (double x, double y)[] corners)
            => new(new Rect(corners.Select(c => new Point(c.x, c.y, 0)).ToList()), type);

        /// <summary>
        /// Asserts that the <see cref="ConvertedWall"/>'s centerline endpoints and
        /// thickness equal the expected values. Centerline comparison is
        /// direction-insensitive — (x1,y1)-(x2,y2) may match either way around.
        /// </summary>
        private static void AssertWall(
            ConvertedWall wall,
            double ex1, double ey1, double ex2, double ey2,
            double eThickness)
        {
            AssertSegmentsEqual(wall.X1, wall.Y1, wall.X2, wall.Y2, ex1, ey1, ex2, ey2);
            Assert.Equal(eThickness, wall.Thickness, 6);
        }

        /// <summary>
        /// Asserts that the <see cref="ConvertedOpening"/>'s type and segment endpoints
        /// equal the expected values. Endpoint comparison is direction-insensitive.
        /// </summary>
        private static void AssertOpening(
            ConvertedOpening opening,
            ElementType eType,
            double ex1, double ey1, double ex2, double ey2)
        {
            Assert.Equal(eType, opening.Type);
            AssertSegmentsEqual(opening.X1, opening.Y1, opening.X2, opening.Y2, ex1, ey1, ex2, ey2);
        }

        /// <summary>
        /// Direction-insensitive comparison of two 2D line segments expressed as
        /// raw doubles.
        /// </summary>
        private static void AssertSegmentsEqual(
            double a1x, double a1y, double a2x, double a2y,
            double e1x, double e1y, double e2x, double e2y)
        {
            const double tol = GeometrySettings.Tolerance;
            bool forward = Near(a1x, a1y, e1x, e1y, tol) && Near(a2x, a2y, e2x, e2y, tol);
            bool flipped = Near(a1x, a1y, e2x, e2y, tol) && Near(a2x, a2y, e1x, e1y, tol);
            Assert.True(forward || flipped,
                $"Segment mismatch: got ({a1x},{a1y})-({a2x},{a2y}), " +
                $"expected ({e1x},{e1y})-({e2x},{e2y})");
        }

        private static bool Near(double ax, double ay, double bx, double by, double tol)
            => Math.Abs(ax - bx) < tol && Math.Abs(ay - by) < tol;
    }
}
