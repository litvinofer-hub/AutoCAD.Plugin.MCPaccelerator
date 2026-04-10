using System;

namespace MCPAccelerator.Utils.GeometryModel
{
    /// <summary>
    /// Dimensionless, project-wide numerical settings. Unit-dependent values
    /// (story height, wall thickness, etc.) live on <see cref="UnitSystem"/>
    /// and are attached to a Building instance.
    /// </summary>
    public static class GeometrySettings
    {
        /// <summary>
        /// Numerical tolerance for floating-point comparisons. Dimensionless.
        /// </summary>
        public const double Tolerance = 1e-6;

        /// <summary>
        /// Returns true if a and b are equal within the tolerance.
        /// </summary>
        public static bool AreEqual(double a, double b)
        {
            return Math.Abs(a - b) < Tolerance;
        }

        /// <summary>
        /// Returns true if a is greater than b, beyond the tolerance.
        /// </summary>
        public static bool IsGreaterThan(double a, double b)
        {
            return a - b > Tolerance;
        }

        /// <summary>
        /// Returns true if a is less than b, beyond the tolerance.
        /// </summary>
        public static bool IsLessThan(double a, double b)
        {
            return b - a > Tolerance;
        }

        /// <summary>
        /// Returns true if a is greater than or equal to b, within the tolerance.
        /// </summary>
        public static bool IsGreaterThanOrEqual(double a, double b)
        {
            return a - b > -Tolerance;
        }

        /// <summary>
        /// Returns true if a is less than or equal to b, within the tolerance.
        /// </summary>
        public static bool IsLessThanOrEqual(double a, double b)
        {
            return b - a > -Tolerance;
        }
    }
}
