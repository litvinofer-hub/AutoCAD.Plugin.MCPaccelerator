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
    /// Converts AutoCAD floor-plan polylines (walls, windows, doors) into domain
    /// <see cref="Wall"/>s and <see cref="WallOpening"/>s. Split into two phases so
    /// the geometric conversion can be tested without a <see cref="Building"/>:
    ///
    /// - <see cref="Convert"/> — pure. Takes AutoCAD polylines and returns a list of
    ///   <see cref="ConvertedWall"/> DTOs (centerline + thickness + 2D openings).
    ///   No Building / Story / UnitSystem dependency.
    /// - <see cref="Apply"/> — attaches the DTOs to a concrete Building + Story,
    ///   resolving opening sill height and total height from the building's
    ///   <see cref="UnitSystem"/> (a 2D floor plan carries no vertical info).
    ///
    /// Convert pipeline:
    /// 1. <see cref="ToTaggedRects"/>         — flatten each AutoCAD polyline into a
    ///    pure <see cref="Rect"/> tagged with its floor-plan role. Layer-name
    ///    classification happens earlier (in SelectFloorPlanWorkflow); inputs arrive
    ///    already split into walls / windows / doors.
    /// 2. <see cref="ChainBuilder.Build"/>    — for each opening, find its 2 flanking
    ///    walls and group walls connected through shared openings into chains.
    /// 3. <see cref="ChainWallFactory.Create"/>    — merge each chain into one
    ///    <see cref="ConvertedWall"/> with its openings.
    /// 4. <see cref="StandaloneWallFactory.Create"/> — emit a plain
    ///    <see cref="ConvertedWall"/> for every wall rect not consumed by a chain.
    ///
    /// Assumptions:
    /// - Openings always have length &gt; thickness, so their
    ///   <see cref="Rect.Direction2D"/> reliably points along the chain.
    /// - A wall and an opening that belong to the same chain touch at the opening's
    ///   end — their nearest vertices are within <paramref name="touchTolerance"/>
    ///   (typically <see cref="UnitSystem.LengthEpsilon"/>).
    /// - Short "stub" walls (length &lt; thickness) only appear next to openings;
    ///   they are handled identically to normal walls because the chain builder
    ///   never reads a wall's own direction.
    ///
    /// All pure geometry lives in <see cref="Rect"/> (and its base
    /// <see cref="Polyline"/>), so future projects can reuse the same geometry
    /// without depending on AutoCAD.
    /// </summary>
    public static class FloorPlanConverter
    {
        /// <summary>
        /// Pure core conversion: takes already-built <see cref="TaggedRect"/>s (no
        /// AutoCAD types) and returns a list of <see cref="ConvertedWall"/> DTOs.
        /// This overload is the one to call from unit tests — it has no AutoCAD
        /// dependency and no Building / Story / UnitSystem dependency.
        /// </summary>
        /// <param name="walls">Wall rectangles in their original input order.</param>
        /// <param name="openings">Window + door rectangles, each tagged with its type,
        /// in their original input order.</param>
        /// <param name="touchTolerance">Maximum distance between two rectangles' nearest
        /// vertices for them to count as touching. Pass
        /// <see cref="UnitSystem.LengthEpsilon"/> from the target building (or any small
        /// length in the drawing's units when testing).</param>
        public static List<ConvertedWall> Convert(
            List<TaggedRect> walls,
            List<TaggedRect> openings,
            double touchTolerance)
        {
            var chains = ChainBuilder.Build(walls, openings, touchTolerance);

            var result = new List<ConvertedWall>();
            var usedWalls = new HashSet<int>();
            foreach (var chain in chains)
            {
                result.Add(ChainWallFactory.Create(chain));
                foreach (var idx in chain.WallIndices)
                    usedWalls.Add(idx);
            }

            for (int i = 0; i < walls.Count; i++)
            {
                if (usedWalls.Contains(i)) continue;
                result.Add(StandaloneWallFactory.Create(walls[i]));
            }

            return result;
        }

        /// <summary>
        /// AutoCAD-facing overload: flattens each AutoCAD polyline into a pure
        /// <see cref="Rect"/>, tags it with its role, and delegates to the pure
        /// <see cref="Convert(List{TaggedRect}, List{TaggedRect}, double)"/> overload.
        /// Used by the plugin workflow; unit tests should prefer the pure overload.
        /// </summary>
        public static List<ConvertedWall> Convert(
            List<AcadPolyline> wallPolylines,
            List<AcadPolyline> windowPolylines,
            List<AcadPolyline> doorPolylines,
            double touchTolerance)
        {
            var walls = ToTaggedRects(wallPolylines, ElementType.Wall);
            var openings = ToTaggedRects(windowPolylines, ElementType.Window)
                .Concat(ToTaggedRects(doorPolylines, ElementType.Door))
                .ToList();
            return Convert(walls, openings, touchTolerance);
        }

        /// <summary>
        /// Adds the converted walls and openings to <paramref name="building"/> under
        /// <paramref name="story"/>. Opening sill height and total height come from
        /// the building's <see cref="UnitSystem"/> defaults — floor plans carry no
        /// vertical information. Failures are caught per opening and counted in
        /// <see cref="FloorPlanResult.OpeningsSkipped"/>.
        /// </summary>
        public static FloorPlanResult Apply(Building building, Story story, List<ConvertedWall> converted)
        {
            var result = new FloorPlanResult();
            var units = building.Units;
            double botZ = story.BotLevel.Elevation;

            foreach (var cw in converted)
            {
                var wall = building.AddWall(cw.X1, cw.Y1, cw.X2, cw.Y2, story, cw.Thickness);
                result.WallsCreated++;

                foreach (var co in cw.Openings)
                {
                    try
                    {
                        switch (co.Type)
                        {
                            case ElementType.Window:
                                building.AddWindow(wall, co.X1, co.Y1, co.X2, co.Y2,
                                    botZ + units.DefaultWindowSillHeight, units.DefaultWindowHeight);
                                result.WindowsCreated++;
                                break;
                            case ElementType.Door:
                                building.AddDoor(wall, co.X1, co.Y1, co.X2, co.Y2,
                                    botZ + units.DefaultDoorSillHeight, units.DefaultDoorHeight);
                                result.DoorsCreated++;
                                break;
                        }
                    }
                    catch
                    {
                        result.OpeningsSkipped++;
                    }
                }
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
    /// Counters returned from <see cref="FloorPlanConverter.Apply"/>, used by the
    /// UI layer to report what was (or wasn't) created on the building.
    /// </summary>
    public class FloorPlanResult
    {
        public int WallsCreated { get; set; }
        public int WindowsCreated { get; set; }
        public int DoorsCreated { get; set; }
        public int OpeningsSkipped { get; set; }
    }
}
