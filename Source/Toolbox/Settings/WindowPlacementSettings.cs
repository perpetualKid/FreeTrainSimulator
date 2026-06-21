using MemoryPack;

namespace FreeTrainSimulator.Toolbox.Settings
{
    /// <summary>
    /// Platform-neutral, durable placement of the toolbox shell window: the restored (normal) rectangle plus
    /// whether the window was maximized. <see cref="WindowBoundsManager"/> converts this to and from the Win32
    /// placement structure at the interop boundary, keeping native types out of the persisted settings schema.
    /// </summary>
    [MemoryPackable]
    public sealed partial record WindowPlacementSettings
    {
        [MemoryPackConstructor]
        public WindowPlacementSettings(int left, int top, int right, int bottom, bool maximized)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Maximized = maximized;
        }

        public int Left { get; init; }

        public int Top { get; init; }

        public int Right { get; init; }

        public int Bottom { get; init; }

        public bool Maximized { get; init; }
    }
}
