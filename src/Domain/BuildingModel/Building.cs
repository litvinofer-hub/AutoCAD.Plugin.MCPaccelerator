using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Building : IHavePoints
    {
        public Guid Id { get; set; }
        public List<Level> Levels { get; set; }
        public List<Story> Stories { get; set; }
        public List<Room> Rooms { get; set; }
        public List<Wall> Walls { get; set; }

        public Building()
        {
            Id = Guid.NewGuid();
            Levels = new List<Level>();
            Stories = new List<Story>();
            Rooms = new List<Room>();
            Walls = new List<Wall>();
        }

        public IEnumerable<Point> GetPoints()
        {
            foreach (var room in Rooms)
            {
                foreach (var point in room.GetPoints())
                    yield return point;
            }

            foreach (var wall in Walls)
            {
                foreach (var point in wall.GetPoints())
                    yield return point;
            }
        }
    }
}
