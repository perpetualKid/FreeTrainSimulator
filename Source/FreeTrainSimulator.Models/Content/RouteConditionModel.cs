using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Describes the physical infrastructure conditions of a route, including track gauge,
    /// electrification parameters, and overhead wire configuration.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteConditionModel
    {
        /// <summary>Track gauge in meters. Defaults to standard gauge (1.435 m).</summary>
        public float TrackGauge { get; init; } = 1.435f;
        /// <summary>Indicates whether the route has electrified track sections.</summary>
        public bool Electrified { get; init; }
        /// <summary>Maximum overhead line voltage in volts.</summary>
        public float MaxLineVoltage { get; init; }
        /// <summary>Height of the overhead contact wire above the rail in meters.</summary>
        public float OverheadWireHeight { get; init; }
        /// <summary>Indicates whether double overhead wire rendering is enabled.</summary>
        public bool DoubleWireEnabled { get; init; }
        /// <summary>Vertical separation between the two overhead wires in meters.</summary>
        public float DoubleWireHeight { get; init; }
        /// <summary>Indicates whether triphase (three-phase) overhead wire rendering is enabled.</summary>
        public bool TriphaseEnabled { get; init; }
        /// <summary>Horizontal spacing of the triphase overhead wires in meters.</summary>
        public float TriphaseWidth { get; init; }
    }
}
