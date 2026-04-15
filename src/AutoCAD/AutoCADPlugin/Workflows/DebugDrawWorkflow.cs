using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Domain.BuildingModel.Debugging;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Workflows
{
    /// <summary>
    /// Orchestrates the OL_DEBUG_DRAW command.
    ///
    /// Edit <see cref="Drawings"/> before running to control which SVGs are generated.
    /// Each entry is a list of layer names. Supported names:
    /// "walls", "windows", "doors", "axial lines".
    /// </summary>
    public class DebugDrawWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;

        /// <summary>
        /// Each inner list defines one SVG output (per story).
        /// Change this list before running to control what gets drawn.
        /// </summary>
        public static List<List<string>> Drawings = new List<List<string>>
        {
            new List<string> { "walls", "windows", "doors", "axial lines" },
            new List<string> { "walls", "windows", "doors" },
            new List<string> { "walls" },
            new List<string> { "windows" },
            new List<string> { "doors" },
        };

        private static readonly Dictionary<string, Func<Building, Story, DrawableLayer>> LayerBuilders =
            new Dictionary<string, Func<Building, Story, DrawableLayer>>(StringComparer.OrdinalIgnoreCase)
            {
                { "walls",       DrawableLayerFactory.Walls },
                { "windows",     DrawableLayerFactory.Windows },
                { "doors",       DrawableLayerFactory.Doors },
                { "axial lines", DrawableLayerFactory.AxialLines },
            };

        public void Run()
        {
            var building = BuildingContextPrompt.PickBuilding("debug draw");
            if (building == null) return;

            string outputDir = OutputPathHelper.GetOutputDir();
            var drawer = new FloorPlanDrawer(building, outputDir);
            var allFiles = new List<string>();

            foreach (var drawing in Drawings)
            {
                var validNames = drawing.Where(n => LayerBuilders.ContainsKey(n)).ToList();
                if (validNames.Count == 0) continue;

                string prefix = string.Join("_", validNames.Select(n => n.Replace(" ", "")));

                allFiles.AddRange(drawer.Draw(prefix, story =>
                    validNames.Select(n => LayerBuilders[n](building, story))));
            }

            _editor.WriteMessage($"\nGenerated {allFiles.Count} debug SVG file(s) in:\n{outputDir}\n");
            foreach (var f in allFiles)
                _editor.WriteMessage($"  {System.IO.Path.GetFileName(f)}\n");
        }
    }
}
