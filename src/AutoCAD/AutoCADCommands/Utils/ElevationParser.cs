using System;
using System.Collections.Generic;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Utils
{
    /// <summary>
    /// Parses a space-separated list of elevation values from user input.
    /// </summary>
    public static class ElevationParser
    {
        /// <summary>
        /// Splits the input on whitespace and parses each token as a double.
        /// Returns null if any token cannot be parsed.
        /// </summary>
        public static List<double> Parse(string input)
        {
            var parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var elevations = new List<double>();

            foreach (var part in parts)
            {
                if (!double.TryParse(part, out double value))
                    return null;
                elevations.Add(value);
            }

            return elevations;
        }
    }
}
