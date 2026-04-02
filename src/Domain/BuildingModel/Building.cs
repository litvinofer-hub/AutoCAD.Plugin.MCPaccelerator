using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Building
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
    }
}
