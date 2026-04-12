using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPAccelerator.Domain.BuildingModel
{
    /// <summary>
    /// Serializes a <see cref="Building"/> to a human-readable JSON string.
    ///
    /// The domain classes have circular references (Wall → StoryId → Story → Levels)
    /// and internal state (flyweight point cache) that must not leak into the output.
    /// This serializer maps the object graph to flat DTOs, replacing object references
    /// with Guid-based IDs so the JSON is self-contained and round-trippable by any
    /// consumer that understands the schema.
    /// </summary>
    public static class BuildingJsonSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string ToJson(Building building)
        {
            var dto = new BuildingDto
            {
                Id = building.Id.ToString(),
                Name = building.Name,
                Units = new UnitsDto
                {
                    Unit = building.Units.Unit.ToString(),
                    DefaultStoryHeight = building.Units.DefaultStoryHeight,
                    DefaultWallThickness = building.Units.DefaultWallThickness,
                    DefaultDoorHeight = building.Units.DefaultDoorHeight,
                    DefaultDoorWidth = building.Units.DefaultDoorWidth,
                    DefaultDoorSillHeight = building.Units.DefaultDoorSillHeight,
                    DefaultWindowHeight = building.Units.DefaultWindowHeight,
                    DefaultWindowWidth = building.Units.DefaultWindowWidth,
                    DefaultWindowSillHeight = building.Units.DefaultWindowSillHeight
                },
                Levels = building.Levels
                    .OrderBy(l => l.Elevation)
                    .Select(ToDto)
                    .ToList(),
                Stories = building.Stories
                    .OrderBy(s => s.BotLevel.Elevation)
                    .Select(ToDto)
                    .ToList(),
                Walls = building.Walls.Select(ToDto).ToList(),
                Rooms = building.Rooms.Select(ToDto).ToList()
            };

            return JsonSerializer.Serialize(dto, Options);
        }

        // ---------------------------------------------------------------
        // Mapping helpers
        // ---------------------------------------------------------------

        private static LevelDto ToDto(Level level) => new()
        {
            Id = level.Id.ToString(),
            Elevation = level.Elevation,
            SubLevels = level.SubLevels.Count > 0
                ? level.SubLevels.Select(s => new SubLevelDto
                {
                    Id = s.Id.ToString(),
                    Offset = s.Offset
                }).ToList()
                : null
        };

        private static StoryDto ToDto(Story story) => new()
        {
            Id = story.Id.ToString(),
            Name = story.Name,
            BotLevelId = story.BotLevel.Id.ToString(),
            TopLevelId = story.TopLevel.Id.ToString(),
            BotElevation = story.BotLevel.Elevation,
            TopElevation = story.TopLevel.Elevation,
            IntermediateLevelIds = story.IntermediateLevels.Count > 0
                ? story.IntermediateLevels.Select(l => l.Id.ToString()).ToList()
                : null
        };

        private static WallDto ToDto(Wall wall) => new()
        {
            Id = wall.Id.ToString(),
            StoryId = wall.StoryId.ToString(),
            BotLevelId = wall.BotLevel.Id.ToString(),
            TopLevelId = wall.TopLevel.Id.ToString(),
            BotElevation = wall.BotLevel.Elevation,
            TopElevation = wall.TopLevel.Elevation,
            Thickness = wall.Thickness,
            Line = ToDto(wall.BotLine),
            Openings = wall.Openings.Count > 0
                ? wall.Openings.Select(o => ToDto(o, wall)).ToList()
                : null
        };

        private static OpeningDto ToDto(WallOpening opening, Wall parentWall) => new()
        {
            Id = opening.Id.ToString(),
            Type = opening is Door ? "Door" : "Window",
            Height = opening.Height,
            SillElevation = opening.Line.StartPoint.Z,
            Line = ToDto(opening.Line)
        };

        private static RoomDto ToDto(Room room) => new()
        {
            Id = room.Id.ToString(),
            BotLevelId = room.BotLevel.Id.ToString(),
            TopLevelId = room.TopLevel.Id.ToString(),
            BotElevation = room.BotLevel.Elevation,
            TopElevation = room.TopLevel.Elevation,
            Vertices = room.GetPoints().Select(PointToArray).ToList()
        };

        private static LineDto ToDto(Utils.GeometryModel.LineSegment line) => new()
        {
            Start = PointToArray(line.StartPoint),
            End = PointToArray(line.EndPoint)
        };

        private static double[] PointToArray(Utils.GeometryModel.Point p) =>
            [p.X, p.Y, p.Z];

        // ---------------------------------------------------------------
        // DTOs — internal, used only for serialization shape
        // ---------------------------------------------------------------

        private class BuildingDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public UnitsDto Units { get; set; }
            public List<LevelDto> Levels { get; set; }
            public List<StoryDto> Stories { get; set; }
            public List<WallDto> Walls { get; set; }
            public List<RoomDto> Rooms { get; set; }
        }

        private class UnitsDto
        {
            public string Unit { get; set; }
            public double DefaultStoryHeight { get; set; }
            public double DefaultWallThickness { get; set; }
            public double DefaultDoorHeight { get; set; }
            public double DefaultDoorWidth { get; set; }
            public double DefaultDoorSillHeight { get; set; }
            public double DefaultWindowHeight { get; set; }
            public double DefaultWindowWidth { get; set; }
            public double DefaultWindowSillHeight { get; set; }
        }

        private class LevelDto
        {
            public string Id { get; set; }
            public double Elevation { get; set; }
            public List<SubLevelDto> SubLevels { get; set; }
        }

        private class SubLevelDto
        {
            public string Id { get; set; }
            public double Offset { get; set; }
        }

        private class StoryDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string BotLevelId { get; set; }
            public string TopLevelId { get; set; }
            public double BotElevation { get; set; }
            public double TopElevation { get; set; }
            public List<string> IntermediateLevelIds { get; set; }
        }

        private class WallDto
        {
            public string Id { get; set; }
            public string StoryId { get; set; }
            public string BotLevelId { get; set; }
            public string TopLevelId { get; set; }
            public double BotElevation { get; set; }
            public double TopElevation { get; set; }
            public double Thickness { get; set; }
            public LineDto Line { get; set; }
            public List<OpeningDto> Openings { get; set; }
        }

        private class OpeningDto
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public double Height { get; set; }
            public double SillElevation { get; set; }
            public LineDto Line { get; set; }
        }

        private class RoomDto
        {
            public string Id { get; set; }
            public string BotLevelId { get; set; }
            public string TopLevelId { get; set; }
            public double BotElevation { get; set; }
            public double TopElevation { get; set; }
            public List<double[]> Vertices { get; set; }
        }

        private class LineDto
        {
            public double[] Start { get; set; }
            public double[] End { get; set; }
        }
    }
}
