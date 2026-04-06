using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Building : IHavePoints
    {
        public Guid Id { get; set; }

        private readonly Dictionary<Point, Point> _uniquePoints = new Dictionary<Point, Point>();
        private readonly List<Level> _levels = new List<Level>();
        private readonly List<Story> _stories = new List<Story>();
        private readonly List<Room> _rooms = new List<Room>();
        private readonly List<Wall> _walls = new List<Wall>();

        public IReadOnlyList<Level> Levels => _levels.AsReadOnly();
        public IReadOnlyList<Story> Stories => _stories.AsReadOnly();
        public IReadOnlyList<Room> Rooms => _rooms.AsReadOnly();
        public IReadOnlyList<Wall> Walls => _walls.AsReadOnly();

        public Building()
        {
            Id = Guid.NewGuid();
        }

        // --- Shared instance management ---

        /// <summary>
        /// Returns the existing Point with matching coordinates (within tolerance),
        /// or registers and returns the new point.
        /// </summary>
        public Point GetOrAddPoint(double x, double y, double z)
        {
            var candidate = new Point(x, y, z);

            if (_uniquePoints.TryGetValue(candidate, out var existing))
                return existing;

            _uniquePoints.Add(candidate, candidate);
            return candidate;
        }

        /// <summary>
        /// Returns the existing Level with matching elevation (within tolerance),
        /// or creates, registers, and returns a new one.
        /// </summary>
        public Level GetOrAddLevel(double elevation)
        {
            var existing = _levels.FirstOrDefault(l => GeometrySettings.AreEqual(l.Elevation, elevation));
            if (existing != null)
                return existing;

            var newLevel = new Level(this.Id, elevation);
            _levels.Add(newLevel);
            return newLevel;
        }

        // --- Add methods ---

        public Room AddRoom(IEnumerable<(double x, double y)> vertices, double botElevation, double topElevation)
        {
            Level bot = GetOrAddLevel(botElevation);
            Level top = GetOrAddLevel(topElevation);

            var sharedPoints = vertices
                .Select(v => GetOrAddPoint(v.x, v.y, bot.Elevation))
                .ToList();

            var polygon = new Polygon(sharedPoints);
            var room = new Room(this.Id, polygon, top, Levels);
            _rooms.Add(room);
            return room;
        }

        public Wall AddWall(double x1, double y1, double x2, double y2,
            double botElevation, double topElevation, double thickness)
        {
            Level bot = GetOrAddLevel(botElevation);
            Level top = GetOrAddLevel(topElevation);

            Point start = GetOrAddPoint(x1, y1, bot.Elevation);
            Point end = GetOrAddPoint(x2, y2, bot.Elevation);

            var botLine = new LineSegment(start, end);
            var wall = new Wall(this.Id, botLine, thickness, top, Levels);
            _walls.Add(wall);
            return wall;
        }

        public Story AddStory(double botElevation, double topElevation)
        {
            Level bot = GetOrAddLevel(botElevation);
            Level top = GetOrAddLevel(topElevation);

            var story = new Story(this.Id, bot, top);
            _stories.Add(story);
            return story;
        }

        // --- Remove methods ---

        public bool RemoveRoom(Room room)
        {
            return _rooms.Remove(room);
        }

        public bool RemoveWall(Wall wall)
        {
            return _walls.Remove(wall);
        }

        public bool RemoveStory(Story story)
        {
            return _stories.Remove(story);
        }

        // --- Cleanup ---

        /// <summary>
        /// Removes points and levels that are no longer referenced by any element.
        /// Call after removing rooms, walls, or stories.
        /// </summary>
        public void Cleanup()
        {
            // Rebuild unique points from current usage
            _uniquePoints.Clear();
            foreach (var point in GetPoints())
            {
                if (!_uniquePoints.ContainsKey(point))
                    _uniquePoints[point] = point;
            }

            // Collect all levels still referenced
            var usedLevels = new List<Level>();
            foreach (var room in _rooms)
            {
                usedLevels.Add(room.BotLevel);
                usedLevels.Add(room.TopLevel);
            }
            foreach (var wall in _walls)
            {
                usedLevels.Add(wall.BotLevel);
                usedLevels.Add(wall.TopLevel);
            }
            foreach (var story in _stories)
            {
                usedLevels.Add(story.BotLevel);
                usedLevels.Add(story.TopLevel);
                foreach (var intermediate in story.IntermediateLevels)
                    usedLevels.Add(intermediate);
            }

            _levels.RemoveAll(l => !usedLevels.Any(used => ReferenceEquals(used, l)));
        }

        // --- IHavePoints ---

        public IEnumerable<Point> GetPoints()
        {
            foreach (var room in _rooms)
            {
                foreach (var point in room.GetPoints())
                    yield return point;
            }

            foreach (var wall in _walls)
            {
                foreach (var point in wall.GetPoints())
                    yield return point;
            }
        }
    }
}
