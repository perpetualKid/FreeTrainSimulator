using System;
using System.Windows;
using System.Windows.Interop;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Toolbox.Settings;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Persists and restores the WPF shell window's placement (position, size, and maximized state) across
    /// sessions. AvalonDock only serializes the docked layout inside the window, not the window's own bounds, so
    /// the shell remembers its placement separately. This is the interop boundary: it translates between the
    /// platform-neutral <see cref="WindowPlacementSettings"/> stored in the profile and the Win32 placement
    /// structure used by <see cref="NativeMethods.SetWindowPlacement"/>/<see cref="NativeMethods.GetWindowPlacement"/>.
    /// </summary>
    internal static class WindowBoundsManager
    {
        /// <summary>
        /// Applies the placement stored in <paramref name="settings"/> to <paramref name="window"/>. The window
        /// must already have a native handle (call after it is shown). A missing or degenerate placement is
        /// ignored, and a non-maximized placement always restores as a normal window so the shell never starts
        /// minimized.
        /// </summary>
        internal static void RestoreWindowBounds(Window window, ProfileToolboxSettingsModel settings)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(settings);

            WindowPlacementSettings saved = settings.WindowPlacements;
            if (!IsValid(saved))
                return;

            nint handle = new WindowInteropHelper(window).Handle;
            if (handle == 0)
                return;

            NativeMethods.WindowPlacement placement = default;
            placement.ShowCommand = saved.Maximized ? NativeMethods.SwShowMaximized : NativeMethods.SwShowNormal;
            placement.NormalPositionLeft = saved.Left;
            placement.NormalPositionTop = saved.Top;
            placement.NormalPositionRight = saved.Right;
            placement.NormalPositionBottom = saved.Bottom;

            _ = NativeMethods.SetWindowPlacement(handle, ref placement);
        }

        /// <summary>
        /// Captures the current placement of <paramref name="window"/> into <paramref name="settings"/> so it can
        /// be persisted. Does nothing if the window has no native handle yet.
        /// </summary>
        internal static void SaveBoundsToSettings(Window window, ProfileToolboxSettingsModel settings)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(settings);

            nint handle = new WindowInteropHelper(window).Handle;
            if (handle == 0)
                return;

            if (!NativeMethods.GetWindowPlacement(handle, out NativeMethods.WindowPlacement placement))
                return;

            // GetWindowPlacement reports the restored (normal) rectangle even when the window is maximized or
            // minimized, so the saved bounds are always the un-maximized size. Only the maximized state is
            // durable; a minimized window is persisted as non-maximized so the shell never restores minimized.
            settings.WindowPlacements = new WindowPlacementSettings(
                placement.NormalPositionLeft,
                placement.NormalPositionTop,
                placement.NormalPositionRight,
                placement.NormalPositionBottom,
                placement.ShowCommand == NativeMethods.SwShowMaximized);
        }

        // Null means nothing has been saved yet; a degenerate (empty) rectangle is also rejected so the shell is
        // never restored to a zero size.
        private static bool IsValid(WindowPlacementSettings placement)
            => placement is not null
                && placement.Right > placement.Left
                && placement.Bottom > placement.Top;
    }
}
