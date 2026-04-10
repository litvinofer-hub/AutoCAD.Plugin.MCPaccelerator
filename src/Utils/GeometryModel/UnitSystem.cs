namespace MCPAccelerator.Utils.GeometryModel
{
    /// <summary>
    /// Length units supported by the project. Add new values here as needed
    /// (e.g. Millimeters, Feet) and provide a matching factory on <see cref="UnitSystem"/>.
    /// </summary>
    public enum LengthUnit
    {
        Inches,
        Meters
    }

    /// <summary>
    /// An instance-based container for all length-unit-dependent defaults.
    ///
    /// This is passed into a <c>Building</c> at construction time so that every
    /// building carries its own unit context. This avoids static state and lets
    /// different buildings in the same session use different units.
    ///
    /// Use the static factories <see cref="Inches"/> / <see cref="Meters"/>
    /// to get a preset, or construct one directly with custom values.
    /// </summary>
    public class UnitSystem(
        LengthUnit unit,
        double defaultStoryHeight,
        double defaultWallThickness,
        double lengthEpsilon,
        double defaultDoorHeight,
        double defaultDoorWidth,
        double defaultDoorSillHeight,
        double defaultWindowHeight,
        double defaultWindowWidth,
        double defaultWindowSillHeight)
    {
        /// <summary>
        /// The length unit that all values in this instance are expressed in.
        /// </summary>
        public LengthUnit Unit { get; private set; } = unit;

        /// <summary>
        /// Default vertical distance between stories (floor-to-floor height).
        /// </summary>
        public double DefaultStoryHeight { get; private set; } = defaultStoryHeight;

        /// <summary>
        /// Default wall thickness used as a fallback when thickness cannot be
        /// derived from geometry.
        /// </summary>
        public double DefaultWallThickness { get; private set; } = defaultWallThickness;

        /// <summary>
        /// Small length used as a minimum threshold for adjacency / snapping gaps
        /// (roughly equivalent to 1 mm in the chosen unit).
        /// </summary>
        public double LengthEpsilon { get; private set; } = lengthEpsilon;

        // --- Door defaults ---

        /// <summary>
        /// Default door height (top of door above its sill).
        /// </summary>
        public double DefaultDoorHeight { get; private set; } = defaultDoorHeight;

        /// <summary>
        /// Default door width (length along the wall).
        /// </summary>
        public double DefaultDoorWidth { get; private set; } = defaultDoorWidth;

        /// <summary>
        /// Default door sill height above the floor (0 for standard doors).
        /// </summary>
        public double DefaultDoorSillHeight { get; private set; } = defaultDoorSillHeight;

        // --- Window defaults ---

        /// <summary>
        /// Default window height (top of window above its sill).
        /// </summary>
        public double DefaultWindowHeight { get; private set; } = defaultWindowHeight;

        /// <summary>
        /// Default window width (length along the wall).
        /// </summary>
        public double DefaultWindowWidth { get; private set; } = defaultWindowWidth;

        /// <summary>
        /// Default window sill height above the floor.
        /// </summary>
        public double DefaultWindowSillHeight { get; private set; } = defaultWindowSillHeight;

        /// <summary>
        /// Imperial preset. 
        /// This is the project default.
        /// </summary>
        public static UnitSystem Inches() => new(
            unit: LengthUnit.Inches,
            defaultStoryHeight: 120,
            defaultWallThickness: 6,
            lengthEpsilon: 0.04,
            defaultDoorHeight: 80.0,
            defaultDoorWidth: 36.0,
            defaultDoorSillHeight: 0.0,
            defaultWindowHeight: 48.0,
            defaultWindowWidth: 36.0,
            defaultWindowSillHeight: 36.0);

        /// <summary>
        /// Metric preset.
        /// </summary>
        public static UnitSystem Meters() => new(
            unit: LengthUnit.Meters,
            defaultStoryHeight: 3.0,
            defaultWallThickness: 0.2,
            lengthEpsilon: 0.001,
            defaultDoorHeight: 2.1,
            defaultDoorWidth: 0.9,
            defaultDoorSillHeight: 0.0,
            defaultWindowHeight: 1.2,
            defaultWindowWidth: 0.9,
            defaultWindowSillHeight: 0.9);
    }
}
