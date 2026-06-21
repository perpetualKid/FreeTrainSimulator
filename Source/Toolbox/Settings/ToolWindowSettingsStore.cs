using MemoryPack;

namespace FreeTrainSimulator.Toolbox.Settings
{
    [MemoryPackable]
    public sealed partial record ToolWindowSettingsStore
    {
        [MemoryPackConstructor]
        public ToolWindowSettingsStore(LocationToolSettings locationToolSettings, DebugOverlaySettings debugOverlaySettings)
        {
            LocationToolSettings = locationToolSettings ?? new LocationToolSettings(useWorldCoordinates: true);
            DebugOverlaySettings = debugOverlaySettings ?? new DebugOverlaySettings(currentPanel: null);
        }

        public ToolWindowSettingsStore()
            : this(new LocationToolSettings(useWorldCoordinates: true), new DebugOverlaySettings(currentPanel: null))
        {
        }

        public LocationToolSettings LocationToolSettings { get; init; } = new LocationToolSettings(useWorldCoordinates: true);

        public DebugOverlaySettings DebugOverlaySettings { get; init; } = new DebugOverlaySettings(currentPanel: null);
    }
}
