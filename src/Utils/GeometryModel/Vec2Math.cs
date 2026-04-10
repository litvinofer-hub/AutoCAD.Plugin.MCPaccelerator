using System;

namespace MCPAccelerator.Utils.GeometryModel
{
    /// <summary>
    /// Pure 2D vector value type. No external dependencies.
    /// Used both as a point and as a direction/displacement.
    /// </summary>
    public readonly struct Vec2(double x, double y)
    {
        public double X { get; } = x;
        public double Y { get; } = y;

        public static Vec2 Zero => new(0, 0);
    }

    /// <summary>
    /// Pure 2D math helpers. Treats <see cref="Vec2"/> as either a point or a vector depending on context.
    /// </summary>
    public static class Vec2Math
    {
        public static Vec2 Subtract(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

        public static Vec2 Mid(Vec2 a, Vec2 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static double Distance(Vec2 a, Vec2 b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double Length(Vec2 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);

        public static Vec2 Normalize(Vec2 v)
        {
            double len = Length(v);
            if (len < 1e-12) return Vec2.Zero;
            return new Vec2(v.X / len, v.Y / len);
        }

        /// <summary>
        /// Returns the perpendicular vector (rotated 90° counter-clockwise).
        /// </summary>
        public static Vec2 Perpendicular(Vec2 v) => new(-v.Y, v.X);
    }
}
