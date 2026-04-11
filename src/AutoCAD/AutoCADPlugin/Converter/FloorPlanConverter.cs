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
    /// In a 2D floor plan, openings (windows/doors) sit between two wall polylines on the
    /// same row. We turn that row into one merged wall with the openings inside it.
    ///
    /// Pipeline:
    /// 1. <see cref="ToTaggedRects"/>         — flatten each AutoCAD polyline into a pure
    ///    <see cref="Rect"/> tagged with its floor-plan role. Layer-name classification
    ///    happens earlier (in SelectFloorPlanWorkflow); inputs arrive already split into
    ///    walls / windows / doors.
    /// 2. <see cref="ChainBuilder"/>          — for each opening, find its 2 flanking walls
    ///    and group walls connected through shared openings into chains.
    /// 3. <see cref="ChainWallFactory"/>      — merge each chain into one wall with its openings.
    /// 4. <see cref="StandaloneWallFactory"/> — create a plain wall for every wall rect that
    ///    wasn't consumed by a chain (i.e. has no adjacent opening).
    ///
    /// Assumptions:
    /// - Openings always have length &gt; thickness, so their <see cref="Rect.Direction2D"/>
    ///   reliably points along the chain.
    /// - A wall and an opening that belong to the same chain touch at the opening's end —
    ///   their nearest vertices are within <see cref="UnitSystem.LengthEpsilon"/>.
    /// - Short "stub" walls (length &lt; thickness) only appear next to openings; they are
    ///   handled identically to normal walls because the chain builder never reads a wall's
    ///   own direction.
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
            var walls = ToTaggedRects(wallPolylines, ElementType.Wall);
            var openings = ToTaggedRects(windowPolylines, ElementType.Window)
                .Concat(ToTaggedRects(doorPolylines, ElementType.Door))
                .ToList();

            var chains = ChainBuilder.Build(walls, openings, building.Units.LengthEpsilon);

            var result = new FloorPlanResult();
            var usedWalls = new HashSet<int>();
            foreach (var chain in chains)
            {
                ChainWallFactory.Create(building, chain, story, result);
                foreach (var idx in chain.WallIndices)
                    usedWalls.Add(idx);
            }

            for (int i = 0; i < walls.Count; i++)
            {
                if (usedWalls.Contains(i)) continue;
                StandaloneWallFactory.Create(building, walls[i], story, result);
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
