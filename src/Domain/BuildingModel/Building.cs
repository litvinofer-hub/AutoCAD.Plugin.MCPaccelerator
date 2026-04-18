using System;
using System.Collections.Generic;
using System.Linq;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Building(string name = "", UnitSystem units = null) : IHavePoints
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Name { get; private set; } = name;

        /// <summary>
        /// The unit system (inches, meters, ...) and length-dependent defaults
        /// (story height, wall thickness, epsilon) used by this building.
        /// Defaults to <see cref="UnitSystem.Inches"/> when not specified.
        /// </summary>
        public UnitSystem Units { get; private set; } = units ?? UnitSystem.Inches();

        private readonly Dictionary<Point, Point> _uniquePoints = [];
        private readonly List<Level> _levels = [];
        private readonly List<Story> _stories = [];
        private readonly List<Room> _rooms = [];
        private readonly List<Wall> _walls = [];
        private readonly List<Beam> _beams = [];

        public IReadOnlyList<Level> Levels => _levels.AsReadOnly();
        public IReadOnlyList<Story> Stories => _stories.AsReadOnly();
        public IReadOnlyList<Room> Rooms => _rooms.AsReadOnly();
        public IReadOnlyList<Wall> Walls => _walls.AsReadOnly();
        public IReadOnlyList<Beam> Beams => _beams.AsReadOnly();

        /// <summary>
        /// The building-wide axial system (shared across all stories). All its
        /// <see cref="AxialLine"/> coordinates are in building space. Each
        /// <see cref="Story"/> that participates sets its own
        /// <see cref="Story.CanvasOrigin"/> to position the shared system on
        /// that story's floor plan in the 2D canvas.
        /// </summary>
        public AxialSystem AxialSystem { get; private set; }

        /// <summary>Sets the building's axial system.</summary>
        public void SetAxialSystem(AxialSystem axialSystem) => AxialSystem = axialSystem;

        /// <summary>Clears the axial system from the building.</summary>
        public void ClearAxialSystem() => AxialSystem = null;

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

        /// <summary>
        /// Creates a wall at the given elevation. Looks up the <see cref="Story"/>
        /// whose [BotLevel, TopLevel) range contains <paramref name="botElevation"/>
        /// and attaches it to the wall. Throws if no story matches — a wall cannot
        /// exist without a story.
        /// </summary>
        public Wall AddWall(double x1, double y1, double x2, double y2,
            double botElevation, double topElevation, double thickness)
        {
            Level bot = GetOrAddLevel(botElevation);
            Level top = GetOrAddLevel(topElevation);

            Story story = FindStoryFor(bot.Elevation)
                ?? throw new ArgumentException(
                    $"Cannot create wall at bottom elevation {bot.Elevation}: no story contains this elevation. Add a story first.");

            return CreateWall(x1, y1, x2, y2, bot, top, thickness, story);
        }

        /// <summary>
        /// Creates a wall on the given <paramref name="story"/> directly. Use this
        /// overload when the caller already knows the story (e.g. importing a floor
        /// plan the user explicitly assigned to a story) — it skips the elevation
        /// lookup.
        /// </summary>
        public Wall AddWall(double x1, double y1, double x2, double y2,
            Story story, double thickness)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (!_stories.Contains(story))
                throw new ArgumentException("Story does not belong to this building.", nameof(story));

            return CreateWall(x1, y1, x2, y2, story.BotLevel, story.TopLevel, thickness, story);
        }

        private Wall CreateWall(double x1, double y1, double x2, double y2,
            Level bot, Level top, double thickness, Story story)
        {
            Point start = GetOrAddPoint(x1, y1, bot.Elevation);
            Point end = GetOrAddPoint(x2, y2, bot.Elevation);

            var botLine = new LineSegment(start, end);
            var wall = new Wall(this.Id, botLine, thickness, top, Levels, story.Id);
            _walls.Add(wall);

            AddWallTopEdgeToGraph(x1, y1, x2, y2, top, wall);
            return wall;
        }

        /// <summary>
        /// Adds the wall's top middle-line as an edge in the top level's plan
        /// graph. Points go through <see cref="GetOrAddPoint"/> so the graph's
        /// nodes share coordinates with any wall/beam/edge at the same XY/Z.
        /// No-op for non-orthogonal or zero-length walls (the plan graph is
        /// orthogonal-only).
        /// </summary>
        private void AddWallTopEdgeToGraph(double x1, double y1, double x2, double y2,
            Level top, Wall wall)
        {
            bool sameX = GeometrySettings.AreEqual(x1, x2);
            bool sameY = GeometrySettings.AreEqual(y1, y2);
            bool isOrthogonalNonZero = sameX ^ sameY;
            if (!isOrthogonalNonZero) return;

            Point topStart = GetOrAddPoint(x1, y1, top.Elevation);
            Point topEnd   = GetOrAddPoint(x2, y2, top.Elevation);
            var edge = top.Graph.AddEdge(topStart, topEnd);
            edge.ElementId = wall.Id;
        }

        /// <summary>
        /// Creates a beam whose <b>top</b> line is at <paramref name="topElevation"/>.
        /// The beam hangs <paramref name="height"/> below. Looks up the story
        /// whose elevation range contains the beam's bottom (topElevation − height).
        /// </summary>
        public Beam AddBeam(double x1, double y1, double x2, double y2,
            double topElevation, double thickness, double height)
        {
            Level top = GetOrAddLevel(topElevation);

            double botElevation = top.Elevation - height;
            Story story = FindStoryFor(botElevation)
                ?? throw new ArgumentException(
                    $"Cannot create beam at bottom elevation {botElevation}: no story contains this elevation. Add a story first.");

            return CreateBeam(x1, y1, x2, y2, top, thickness, height, story);
        }

        /// <summary>
        /// Creates a beam on the given <paramref name="story"/>. The beam's top
        /// line is placed at the story's TopLevel.Elevation and the beam hangs
        /// <paramref name="height"/> below.
        /// </summary>
        public Beam AddBeam(double x1, double y1, double x2, double y2,
            Story story, double thickness, double height)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (!_stories.Contains(story))
                throw new ArgumentException("Story does not belong to this building.", nameof(story));

            return CreateBeam(x1, y1, x2, y2, story.TopLevel, thickness, height, story);
        }

        private Beam CreateBeam(double x1, double y1, double x2, double y2,
            Level top, double thickness, double height, Story story)
        {
            Point start = GetOrAddPoint(x1, y1, top.Elevation);
            Point end   = GetOrAddPoint(x2, y2, top.Elevation);

            var topLine = new LineSegment(start, end);
            var beam = new Beam(this.Id, topLine, thickness, height, Levels, story.Id);
            _beams.Add(beam);

            AddBeamTopEdgeToGraph(x1, y1, x2, y2, top, beam);
            return beam;
        }

        /// <summary>
        /// Adds the beam's top line as an edge in the top level's plan graph.
        /// Beam endpoints are already shared via <see cref="GetOrAddPoint"/>.
        /// No-op for non-orthogonal or zero-length beams.
        /// </summary>
        private void AddBeamTopEdgeToGraph(double x1, double y1, double x2, double y2,
            Level top, Beam beam)
        {
            bool sameX = GeometrySettings.AreEqual(x1, x2);
            bool sameY = GeometrySettings.AreEqual(y1, y2);
            bool isOrthogonalNonZero = sameX ^ sameY;
            if (!isOrthogonalNonZero) return;

            Point topStart = GetOrAddPoint(x1, y1, top.Elevation);
            Point topEnd   = GetOrAddPoint(x2, y2, top.Elevation);
            var edge = top.Graph.AddEdge(topStart, topEnd);
            edge.ElementId = beam.Id;
        }

        /// <summary>
        /// Returns the story whose [BotLevel, TopLevel) elevation range contains
        /// <paramref name="elevation"/>, or null when no story matches. The lower
        /// bound is tolerant so walls exactly at a story's floor still match.
        /// </summary>
        private Story FindStoryFor(double elevation)
        {
            double tol = GeometrySettings.Tolerance;
            foreach (var s in _stories)
            {
                if (elevation + tol >= s.BotLevel.Elevation && elevation + tol < s.TopLevel.Elevation)
                    return s;
            }
            return null;
        }

        /// <summary>
        /// Creates a window on <paramref name="wall"/>. The window's endpoints go
        /// through this building's flyweight point repository so they are shared
        /// with any existing points at the same coordinates.
        /// </summary>
        public Window AddWindow(Wall wall, double x1, double y1, double x2, double y2,
            double z, double height)
        {
            var line = CreateOpeningLine(x1, y1, x2, y2, z);
            var window = new Window(wall.Id, height, line);
            wall.AttachOpening(window);
            return window;
        }

        /// <summary>
        /// Creates a door on <paramref name="wall"/>. The door's endpoints go
        /// through this building's flyweight point repository so they are shared
        /// with any existing points at the same coordinates.
        /// </summary>
        public Door AddDoor(Wall wall, double x1, double y1, double x2, double y2,
            double z, double height)
        {
            var line = CreateOpeningLine(x1, y1, x2, y2, z);
            var door = new Door(wall.Id, height, line);
            wall.AttachOpening(door);
            return door;
        }

        private LineSegment CreateOpeningLine(double x1, double y1, double x2, double y2, double z)
        {
            Point start = GetOrAddPoint(x1, y1, z);
            Point end = GetOrAddPoint(x2, y2, z);
            return new LineSegment(start, end);
        }

        public Story AddStory(double botElevation, double topElevation, string name = "",
            IEnumerable<double> intermediateElevations = null)
        {
            Level bot = GetOrAddLevel(botElevation);
            Level top = GetOrAddLevel(topElevation);

            var story = new Story(this.Id, bot, top, name);
            if (intermediateElevations != null)
            {
                foreach (var elevation in intermediateElevations)
                {
                    var level = GetOrAddLevel(elevation);
                    story.AddIntermediateLevel(level);
                }
            }
            _stories.Add(story);
            return story;
        }

        // --- Remove methods ---

        public bool RemoveRoom(Room room)
        {
            bool removed = _rooms.Remove(room);
            if (removed) Cleanup();
            return removed;
        }

        public bool RemoveWall(Wall wall)
        {
            bool removed = _walls.Remove(wall);
            if (removed) Cleanup();
            return removed;
        }

        public bool RemoveBeam(Beam beam)
        {
            bool removed = _beams.Remove(beam);
            if (removed) Cleanup();
            return removed;
        }

        public bool RemoveStory(Story story)
        {
            bool removed = _stories.Remove(story);
            if (removed) Cleanup();
            return removed;
        }

        // --- Cleanup ---

        /// <summary>
        /// Removes points and levels that are no longer referenced by any element.
        /// Called automatically after removing rooms, walls, or stories.
        /// </summary>
        private void Cleanup()
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
            foreach (var beam in _beams)
            {
                usedLevels.Add(beam.TopLevel);
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

            foreach (var beam in _beams)
            {
                foreach (var point in beam.GetPoints())
                    yield return point;
            }
        }
    }
}
