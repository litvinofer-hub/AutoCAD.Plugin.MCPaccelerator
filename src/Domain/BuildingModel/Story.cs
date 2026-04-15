using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel
{
    public class Story(Guid buildingId, Level botLevel, Level topLevel, string name = "")
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid BuildingId { get; private set; } = buildingId;
        public string Name { get; private set; } = name;
        public Level BotLevel { get; private set; } = botLevel;
        public Level TopLevel { get; private set; } = topLevel;

        /// <summary>
        /// Intermediate levels between BotLevel and TopLevel, sorted by elevation.
        /// </summary>
        public List<Level> IntermediateLevels
        {
            get { return [.. _intermediateLevels.OrderBy(l => l.Elevation)]; }
        }
        private readonly List<Level> _intermediateLevels = new List<Level>();

        public double Height => TopLevel.Elevation - BotLevel.Elevation;

        /// <summary>
        /// Where this story's building-space origin (grid A-1 intersection;
        /// building coord 0,0) sits on the 2D AutoCAD canvas. This is how the
        /// building-wide <see cref="AxialSystem"/> is mapped onto each story's
        /// floor plan when stories are laid out side-by-side on the canvas.
        ///
        /// Defaults to (0,0) and <see cref="HasCanvasOrigin"/> = false. Becomes
        /// set when the user creates the axial system on this story, or
        /// registers this story against an existing axial system by picking
        /// matched reference points.
        /// </summary>
        public Vec2 CanvasOrigin { get; private set; } = Vec2.Zero;

        /// <summary>True once <see cref="CanvasOrigin"/> has been explicitly set.</summary>
        public bool HasCanvasOrigin { get; private set; }

        /// <summary>
        /// Sets this story's canvas origin — the canvas point that represents
        /// building-space (0,0,<see cref="BotLevel"/>.Elevation).
        /// </summary>
        public void SetCanvasOrigin(Vec2 canvasOrigin)
        {
            CanvasOrigin = canvasOrigin;
            HasCanvasOrigin = true;
        }

        /// <summary>Clears this story's canvas origin back to (0,0), unset.</summary>
        public void ClearCanvasOrigin()
        {
            CanvasOrigin = Vec2.Zero;
            HasCanvasOrigin = false;
        }

        /// <summary>
        /// Converts a canvas-space 2D point (as drawn in AutoCAD) to a
        /// building-space 2D coordinate by subtracting <see cref="CanvasOrigin"/>.
        /// </summary>
        public (double x, double y) CanvasToBuilding(double canvasX, double canvasY)
            => (canvasX - CanvasOrigin.X, canvasY - CanvasOrigin.Y);

        /// <summary>
        /// Converts a building-space 2D coordinate to canvas space by adding
        /// <see cref="CanvasOrigin"/>.
        /// </summary>
        public (double x, double y) BuildingToCanvas(double buildingX, double buildingY)
            => (buildingX + CanvasOrigin.X, buildingY + CanvasOrigin.Y);

        /// <summary>
        /// Returns all levels in order: BotLevel, intermediate levels, TopLevel.
        /// </summary>
        [JsonIgnore]
        public List<Level> AllLevels
        {
            get
            {
                var levels = new List<Level> { BotLevel };
                levels.AddRange(IntermediateLevels);
                levels.Add(TopLevel);
                return levels;
            }
        }

        /// <summary>
        /// Adds an intermediate level. If the level's elevation is at or below BotLevel,
        /// it replaces BotLevel. If at or above TopLevel, it replaces TopLevel.
        /// </summary>
        public void AddIntermediateLevel(Level level)
        {
            if (GeometrySettings.IsLessThanOrEqual(level.Elevation, BotLevel.Elevation))
            {
                BotLevel = level;
            }
            else if (GeometrySettings.IsGreaterThanOrEqual(level.Elevation, TopLevel.Elevation))
            {
                TopLevel = level;
            }
            else
            {
                _intermediateLevels.Add(level);
            }
        }
    }
}
