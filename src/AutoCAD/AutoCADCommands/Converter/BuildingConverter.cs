using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Converter
{
    /// <summary>
    /// Converts AutoCAD polylines into BuildingModel domain objects.
    /// Wall polylines (closed rectangles) are converted to Wall (center line + thickness).
    /// Window/Door polylines are converted to openings on the nearest wall.
    /// </summary>
    public class BuildingConverter
    {
        /// <summary>
        /// Converts AutoCAD closed polylines (walls, windows, doors) into a Building domain object.
        /// </summary>
        /// <param name="wallPolylines">Closed polylines from wall layers</param>
        /// <param name="windowPolylines">Closed polylines from window layers</param>
        /// <param name="doorPolylines">Closed polylines from door layers</param>
        /// <param name="botElevation">Bottom elevation for all elements</param>
        /// <param name="topElevation">Top elevation for all elements</param>
        /// <returns>A Building containing walls with their openings</returns>
        public Building Convert(
            List<Polyline> wallPolylines,
            List<Polyline> windowPolylines,
            List<Polyline> doorPolylines,
            double botElevation,
            double topElevation)
        {
            var building = new Building();

            // Convert wall polylines to Wall domain objects
            var walls = new List<Wall>();
            foreach (var polyline in wallPolylines)
            {
                var wall = ConvertWallPolyline(building, polyline, botElevation, topElevation);
                if (wall != null)
                    walls.Add(wall);
            }

            // Convert window polylines to Window openings on matching walls
            foreach (var polyline in windowPolylines)
            {
                ConvertOpeningPolyline(building, walls, polyline, OpeningType.Window);
            }

            // Convert door polylines to Door openings on matching walls
            foreach (var polyline in doorPolylines)
            {
                ConvertOpeningPolyline(building, walls, polyline, OpeningType.Door);
            }

            return building;
        }

        /// <summary>
        /// Converts a closed wall polyline (rectangle) into a Wall domain object.
        /// Extracts the center line and thickness from the rectangle geometry.
        /// </summary>
        private Wall ConvertWallPolyline(Building building, Polyline polyline, double botElevation, double topElevation)
        {
            var vertices = GetVertices(polyline);
            if (vertices.Count < 4)
                return null;

            // Find the longest edge pair to determine wall direction (center line along the long axis)
            double side1 = vertices[0].DistanceTo(vertices[1]);
            double side2 = vertices[1].DistanceTo(vertices[2]);

            Point3d start, end;
            double thickness;

            if (side1 >= side2)
            {
                // Side 0-1 and 2-3 are the long edges (wall length)
                start = Midpoint(vertices[0], vertices[3]);
                end = Midpoint(vertices[1], vertices[2]);
                thickness = side2;
            }
            else
            {
                // Side 1-2 and 3-0 are the long edges (wall length)
                start = Midpoint(vertices[0], vertices[1]);
                end = Midpoint(vertices[3], vertices[2]);
                thickness = side1;
            }

            return building.AddWall(start.X, start.Y, end.X, end.Y, botElevation, topElevation, thickness);
        }

        /// <summary>
        /// Converts a closed opening polyline (window/door) into an Opening on the matching wall.
        /// Finds the wall whose center line contains the opening's center line.
        /// </summary>
        private void ConvertOpeningPolyline(Building building, List<Wall> walls, Polyline polyline, OpeningType type)
        {
            var vertices = GetVertices(polyline);
            if (vertices.Count < 4)
                return;

            // Extract opening center line (along the long edge) same logic as walls
            double side1 = vertices[0].DistanceTo(vertices[1]);
            double side2 = vertices[1].DistanceTo(vertices[2]);

            Point3d start, end;
            double height;

            if (side1 >= side2)
            {
                start = Midpoint(vertices[0], vertices[3]);
                end = Midpoint(vertices[1], vertices[2]);
                height = side2;
            }
            else
            {
                start = Midpoint(vertices[0], vertices[1]);
                end = Midpoint(vertices[3], vertices[2]);
                height = side1;
            }

            // Find the wall that contains this opening
            var wall = FindContainingWall(walls, start, end);
            if (wall == null)
                return;

            double z = wall.BotLevel.Elevation;

            switch (type)
            {
                case OpeningType.Window:
                    wall.AddWindow(building, start.X, start.Y, end.X, end.Y, z, height);
                    break;
                case OpeningType.Door:
                    wall.AddDoor(building, start.X, start.Y, end.X, end.Y, z, height);
                    break;
            }
        }

        /// <summary>
        /// Finds the wall whose center line contains both opening endpoints (in 2D).
        /// </summary>
        private Wall FindContainingWall(List<Wall> walls, Point3d start, Point3d end)
        {
            foreach (var wall in walls)
            {
                if (wall.BotLine.IsPointOnSegment2D(new Utils.GeometryModel.Point(start.X, start.Y, 0)) &&
                    wall.BotLine.IsPointOnSegment2D(new Utils.GeometryModel.Point(end.X, end.Y, 0)))
                {
                    return wall;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts 2D vertices from a closed AutoCAD Polyline.
        /// </summary>
        private List<Point3d> GetVertices(Polyline polyline)
        {
            var vertices = new List<Point3d>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                vertices.Add(polyline.GetPoint3dAt(i));
            }
            return vertices;
        }

        private Point3d Midpoint(Point3d a, Point3d b)
        {
            return new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
        }

        private enum OpeningType
        {
            Window,
            Door
        }
    }
}
