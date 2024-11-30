using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record RouteConditionModel
    {
        public float TrackGauge { get; init; } = 1.435f;
        public bool Electrified { get; init; }
        public float MaxLineVoltage { get; init; }
        public float OverheadWireHeight { get; init; }
        public bool DoubleWireEnabled { get; init; }
        public float DoubleWireHeight { get; init; }
        public bool TriphaseEnabled { get; init; }
        public float TriphaseWidth { get; init; }
    }
}
