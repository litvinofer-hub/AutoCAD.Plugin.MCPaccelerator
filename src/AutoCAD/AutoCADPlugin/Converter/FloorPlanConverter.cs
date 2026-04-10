using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.ChainBuilding;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.Model;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Converter.WallCreation;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using AcadPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using GeomPoint = MCPAccelerator.Utils.GeometryModel.Point;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Converter
{
    /// <summary>
    /// Converts AutoCAD floor-plan polylines (walls, windows, doors) into BuildingModel objects.
    ///
    /// In a 2D floor plan, openings (windows/doors) sit between wall polylines forming straight chains:
    ///   [wall] [opening] [wall] [opening] [wall]
    ///
    /// Rules:
    /// - Openings always have length &gt; thickness, so their long axis is reliable.
    /// - Wall polylines near openings can be very short (thickness &gt; length), so their
    ///   long axis is NOT reliable. We use the adjacent opening's direction instead.
    /// - Openings always alternate with walls (never two openings in a row).
    /// - Chains are always straight lines.
    /// - A chain always starts and ends with a wall.
    ///
    /// Pipeline:
    /// 1. <see cref="ToTaggedRects"/>          — convert AutoCAD polylines into pure <see cref="Rect"/>s tagged with their role.
    /// 2. <see cref="ChainBuilder"/>           — grow chains from each opening using <see cref="Adjacency"/>.
    /// 3. <see cref="ChainWallFactory"/>       — merge each chain into one wall with its openings.
    /// 4. <see cref="StandaloneWallFactory"/>  — create a wall for every rectangle no chain consumed.
    ///
    /// All pure geometry lives in <see cref="Rect"/> (and its base <see cref="Polyline"/>),
    /// so future projects can reuse the same geometry without depending on AutoCAD.
    /// </summary>
    public static class FloorPlanConverter
    {
        public static FloorPlanResult Convert(
            Building building,
            Story story,
            List<AcadPolyline> wallPolylines,
            List<AcadPolyline> windowPolylines,
            List<AcadPolyline> doorPolylines)
        {
            double botElevation = story.BotLevel.Elevation;
            double topElevation = story.TopLevel.Elevation;

            var walls = ToTaggedRects(wallPolylines, ElementType.Wall);
            var openings = ToTaggedRects(windowPolylines, ElementType.Window)
                .Concat(ToTaggedRects(doorPolylines, ElementType.Door))
                .ToList();

            var builder = new ChainBuilder(walls, openings, building.Units.LengthEpsilon);
            var chains = builder.BuildAll();

            var result = new FloorPlanResult();

            foreach (var chain in chains)
                ChainWallFactory.Create(building, chain, botElevation, topElevation, result);

            for (int i = 0; i < walls.Count; i++)
            {
                if (builder.UsedWalls.Contains(i)) continue;
                StandaloneWallFactory.Create(building, walls[i], botElevation, topElevation, result);
            }

            return result;
        }

        /// <summary>
        /// Flattens each AutoCAD polyline into a <see cref="Rect"/> (4 corners),
        /// tags it with its floor-plan role, and drops shapes that don't have
        /// exactly 4 corners. This is the only place AutoCAD geometry types
        /// touch the pipeline.
        /// </summary>
        private static List<TaggedRect> ToTaggedRects(List<AcadPolyline> polylines, ElementType type)
        {
            var list = new List<TaggedRect>(polylines.Count);
            foreach (var acad in polylines)
            {
                int n = acad.NumberOfVertices;
                if (n < 4) continue;

                var corners = new List<GeomPoint>(4);
                for (int i = 0; i < 4; i++)
                {
                    var p = acad.GetPoint3dAt(i);
                    corners.Add(new GeomPoint(p.X, p.Y, p.Z));
                }

                Rect rect;
                try { rect = new Rect(corners); }
                catch { continue; } // not 4 distinct corners — skip

                list.Add(new TaggedRect(rect, type));
            }
            return list;
        }
    }

    /// <summary>
    /// Counters returned from a single <see cref="FloorPlanConverter.Convert"/> run,
    /// used by the UI layer to report what was (or wasn't) created.
    /// </summary>
    public class FloorPlanResult
    {
        public int WallsCreated { get; set; }
        public int WindowsCreated { get; set; }
        public int DoorsCreated { get; set; }
        public int OpeningsSkipped { get; set; }
    }
}
