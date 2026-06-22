using MemoryPack;

namespace FreeTrainSimulator.Toolbox.Settings
{
    [MemoryPackable]
    public sealed partial record LocationToolSettings
    {
        [MemoryPackConstructor]
        public LocationToolSettings(bool useWorldCoordinates)
        {
            UseWorldCoordinates = useWorldCoordinates;
        }

        public bool UseWorldCoordinates { get; init; }
    }
}
