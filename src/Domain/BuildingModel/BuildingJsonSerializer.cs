using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPAccelerator.Domain.BuildingModel
{
    /// <summary>
    /// Serializes a <see cref="Building"/> to a human-readable JSON string.
    ///
    /// Uses direct <see cref="System.Text.Json"/> serialization of the domain
    /// classes — no manual DTOs. Any new property added to a domain class
    /// (Building, Story, Wall, AxialSystem, etc.) will automatically appear
    /// in the JSON output. Mark computed/redundant properties with
    /// <see cref="JsonIgnoreAttribute"/> to exclude them.
    /// </summary>
    public static class BuildingJsonSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string ToJson(Building building)
        {
            return JsonSerializer.Serialize(building, Options);
        }
    }
}
