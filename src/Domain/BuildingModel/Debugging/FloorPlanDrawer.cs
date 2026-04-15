using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.Domain.BuildingModel.Debugging
{
    /// <summary>
    /// A single drawable element: a line segment with optional thickness.
    /// When thickness > 0, drawn as a filled rectangle.
    /// When thickness == 0, drawn as a simple line.
    /// </summary>
    public class DrawableElement
    {
        public LineSegment Line { get; }
        public double Thickness { get; }
        public string Label { get; }

        public DrawableElement(LineSegment line, double thickness, string label = "")
        {
            Line = line;
            Thickness = thickness;
            Label = label;
        }

        public double CenterX => (Line.StartPoint.X + Line.EndPoint.X) / 2;
        public double CenterY => (Line.StartPoint.Y + Line.EndPoint.Y) / 2;
    }

    /// <summary>
    /// A named list of drawable elements, drawn in one color.
    /// </summary>
    public class DrawableLayer
    {
        public string Name { get; }
        public List<DrawableElement> Elements { get; }

        public DrawableLayer(string name, List<DrawableElement> elements)
        {
            Name = name;
            Elements = elements;
        }
    }

    /// <summary>
    /// Renders domain objects as SVG files for debugging.
    ///
    /// Usage:
    /// <code>
    /// var drawer = new FloorPlanDrawer(building, outputDir);
    ///
    /// // Draw walls + windows + doors (one multi-layer file per story):
    /// drawer.Draw("floor_plan", story => new[]
    /// {
    ///     DrawableLayerFactory.Walls(building, story),
    ///     DrawableLayerFactory.Windows(building, story),
    ///     DrawableLayerFactory.Doors(building, story),
    /// });
    ///
    /// // Draw only walls (each wall gets its own color):
    /// drawer.Draw("walls_only", story => new[]
    /// {
    ///     DrawableLayerFactory.Walls(building, story),
    /// });
    /// </code>
    ///
    /// Each call to <see cref="Draw"/> produces one SVG per story.
    /// Multiple layers → each layer gets a fixed color.
    /// Single layer → each element gets a unique color from a palette.
    /// Elements are numbered by their index in the list.
    /// </summary>
    public class FloorPlanDrawer
    {
        private readonly Building _building;
        private readonly string _outputDir;

        // Distinct colors for multi-layer mode (one color per layer)
        private static readonly string[] LayerColors =
        [
            "#888888", // gray
            "#e03030", // red
            "#3070e0", // blue
            "#30a040", // green
            "#d0a020", // gold
            "#a030d0", // purple
            "#e07020", // orange
            "#20b0b0", // teal
        ];

        // Palette for single-layer mode (each element gets a unique color)
        private static readonly string[] ElementPalette =
        [
            "#e03030", "#3070e0", "#30a040", "#d0a020", "#a030d0",
            "#e07020", "#20b0b0", "#c04080", "#6080c0", "#80b030",
            "#d06060", "#4090a0", "#b07030", "#7050c0", "#40b070",
            "#c03050", "#5070b0", "#90a020", "#e04090", "#3080c0",
        ];

        public FloorPlanDrawer(Building building, string outputDir)
        {
            _building = building;
            _outputDir = outputDir;
            Directory.CreateDirectory(outputDir);
        }

        /// <summary>
        /// Draws one SVG per story. The <paramref name="layerFactory"/> function
        /// receives a <see cref="Story"/> and returns the layers to draw for it.
        /// </summary>
        /// <param name="filePrefix">Base name for the output files
        /// (e.g. "floor_plan" → "floor_plan_Story1.svg").</param>
        /// <param name="layerFactory">Function that produces the layers for a given story.</param>
        public List<string> Draw(string filePrefix,
            Func<Story, IEnumerable<DrawableLayer>> layerFactory)
        {
            var files = new List<string>();
            var sortedStories = _building.Stories
                .OrderBy(s => s.BotLevel.Elevation)
                .ToList();

            foreach (var story in sortedStories)
            {
                var layers = layerFactory(story).Where(l => l.Elements.Count > 0).ToList();
                if (layers.Count == 0) continue;

                string safeName = SanitizeFileName(story.Name);
                string fileName = $"{filePrefix}_{safeName}.svg";
                string filePath = Path.Combine(_outputDir, fileName);

                string svg = RenderSvg(layers, $"{_building.Name} - {story.Name} - {filePrefix}");
                File.WriteAllText(filePath, svg);
                files.Add(filePath);
            }

            return files;
        }

        private static string RenderSvg(List<DrawableLayer> layers, string title)
        {
            bool singleLayer = layers.Count == 1;

            // Compute bounding box across all elements
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var layer in layers)
            {
                foreach (var elem in layer.Elements)
                {
                    ExpandBounds(elem, ref minX, ref minY, ref maxX, ref maxY);
                }
            }

            if (minX > maxX) return "<svg xmlns=\"http://www.w3.org/2000/svg\"/>";

            double margin = Math.Max((maxX - minX), (maxY - minY)) * 0.08;
            minX -= margin;
            minY -= margin;
            maxX += margin;
            maxY += margin;

            double width = maxX - minX;
            double height = maxY - minY;

            // SVG flips Y (Y goes down in SVG, up in floor plan)
            // viewBox: minX, -maxY, width, height
            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" " +
                          $"viewBox=\"{F(minX)} {F(-maxY)} {F(width)} {F(height)}\" " +
                          $"width=\"1400\" height=\"{(int)(1400 * height / width)}\">");

            // Background
            sb.AppendLine($"  <rect x=\"{F(minX)}\" y=\"{F(-maxY)}\" " +
                          $"width=\"{F(width)}\" height=\"{F(height)}\" fill=\"#fff\"/>");

            // Title
            double titleSize = Math.Max(width, height) * 0.02;
            sb.AppendLine($"  <text x=\"{F(minX + margin * 0.3)}\" y=\"{F(-maxY + titleSize * 1.5)}\" " +
                          $"font-family=\"Arial\" font-size=\"{F(titleSize)}\" fill=\"#333\">{Esc(title)}</text>");

            // Legend
            double legendY = -maxY + titleSize * 3;
            double legendSize = titleSize * 0.8;
            for (int li = 0; li < layers.Count; li++)
            {
                string color = singleLayer ? "#666" : LayerColors[li % LayerColors.Length];
                sb.AppendLine($"  <rect x=\"{F(minX + margin * 0.3)}\" y=\"{F(legendY + li * legendSize * 1.8)}\" " +
                              $"width=\"{F(legendSize)}\" height=\"{F(legendSize)}\" fill=\"{color}\"/>");
                sb.AppendLine($"  <text x=\"{F(minX + margin * 0.3 + legendSize * 1.5)}\" " +
                              $"y=\"{F(legendY + li * legendSize * 1.8 + legendSize * 0.85)}\" " +
                              $"font-family=\"Arial\" font-size=\"{F(legendSize)}\" fill=\"#333\">" +
                              $"{Esc(layers[li].Name)} ({layers[li].Elements.Count})</text>");
            }

            // Draw elements
            double labelSize = Math.Max(width, height) * 0.012;

            for (int li = 0; li < layers.Count; li++)
            {
                var layer = layers[li];
                string layerColor = LayerColors[li % LayerColors.Length];

                for (int ei = 0; ei < layer.Elements.Count; ei++)
                {
                    var elem = layer.Elements[ei];
                    string color = singleLayer
                        ? ElementPalette[ei % ElementPalette.Length]
                        : layerColor;

                    DrawElement(sb, elem, ei, color, labelSize);
                }
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void DrawElement(StringBuilder sb, DrawableElement elem,
            int index, string color, double labelSize)
        {
            double x1 = elem.Line.StartPoint.X;
            double y1 = -elem.Line.StartPoint.Y; // flip Y
            double x2 = elem.Line.EndPoint.X;
            double y2 = -elem.Line.EndPoint.Y; // flip Y

            if (elem.Thickness > 0)
            {
                // Draw as a filled rectangle
                var corners = elem.Line.ToRect(elem.Thickness);
                sb.Append($"  <polygon points=\"");
                for (int i = 0; i < 4; i++)
                {
                    var p = corners.Points[i];
                    if (i > 0) sb.Append(' ');
                    sb.Append($"{F(p.X)},{F(-p.Y)}");
                }
                sb.AppendLine($"\" fill=\"{color}\" fill-opacity=\"0.4\" " +
                              $"stroke=\"{color}\" stroke-width=\"0.3\"/>");
            }
            else
            {
                // Draw as a line
                sb.AppendLine($"  <line x1=\"{F(x1)}\" y1=\"{F(y1)}\" " +
                              $"x2=\"{F(x2)}\" y2=\"{F(y2)}\" " +
                              $"stroke=\"{color}\" stroke-width=\"0.8\"/>");
            }

            // Index label at center
            double cx = (x1 + x2) / 2;
            double cy = (y1 + y2) / 2;
            string label = elem.Label.Length > 0
                ? $"#{index} {elem.Label}"
                : $"#{index}";

            sb.AppendLine($"  <text x=\"{F(cx)}\" y=\"{F(cy + labelSize * 0.35)}\" " +
                          $"font-family=\"Arial\" font-size=\"{F(labelSize)}\" " +
                          $"fill=\"{color}\" text-anchor=\"middle\" font-weight=\"bold\">" +
                          $"{Esc(label)}</text>");
        }

        private static void ExpandBounds(DrawableElement elem,
            ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            double halfT = elem.Thickness / 2;
            double x1 = elem.Line.StartPoint.X;
            double y1 = elem.Line.StartPoint.Y;
            double x2 = elem.Line.EndPoint.X;
            double y2 = elem.Line.EndPoint.Y;

            minX = Math.Min(minX, Math.Min(x1, x2) - halfT);
            maxX = Math.Max(maxX, Math.Max(x1, x2) + halfT);
            minY = Math.Min(minY, Math.Min(y1, y2) - halfT);
            maxY = Math.Max(maxY, Math.Max(y1, y2) + halfT);
        }

        private static string F(double v) =>
            v.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
