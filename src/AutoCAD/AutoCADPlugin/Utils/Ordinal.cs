namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Utils
{
    /// <summary>
    /// Converts integers into their ordinal string form ("1st", "2nd", "3rd", "4th", ...).
    /// </summary>
    public static class Ordinal
    {
        public static string For(int number)
        {
            // 11th, 12th, 13th are exceptions
            if (number % 100 >= 11 && number % 100 <= 13)
                return number + "th";

            return (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th",
            };
        }
    }
}
