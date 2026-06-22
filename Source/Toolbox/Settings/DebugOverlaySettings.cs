using MemoryPack;

namespace FreeTrainSimulator.Toolbox.Settings
{
    [MemoryPackable]
    public sealed partial record DebugOverlaySettings
    {
        [MemoryPackConstructor]
        public DebugOverlaySettings(string currentPanel)
        {
            CurrentPanel = currentPanel;
        }

        public string CurrentPanel { get; init; }
    }
}
