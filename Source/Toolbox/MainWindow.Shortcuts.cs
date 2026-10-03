using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using AvalonDock.Controls;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Toolbox
{
    public partial class MainWindow
    {
        private readonly InputManager shortcutInputManager = InputManager.Current;
        private readonly Application shortcutApplication = Application.Current;
        private readonly ToolWindowShortcutPressTracker shortcutPressTracker = new();

        private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (e.StagingItem.Input is not KeyEventArgs keyEvent
                || (keyEvent.RoutedEvent != System.Windows.Input.Keyboard.PreviewKeyDownEvent
                    && keyEvent.RoutedEvent != System.Windows.Input.Keyboard.PreviewKeyUpEvent))
                return;

            Key key = keyEvent.Key == Key.System ? keyEvent.SystemKey : keyEvent.Key;
            shortcutPressTracker.ReleaseMissingKeys(keyEvent.KeyboardDevice);
            if (keyEvent.RoutedEvent == System.Windows.Input.Keyboard.PreviewKeyUpEvent)
            {
                shortcutPressTracker.ReleaseKey(key);
                return;
            }

            ModifierKeys modifiers = keyEvent.KeyboardDevice.Modifiers;
            KeyboardState keyboardState = new KeyboardState(
                (Keys)KeyInterop.VirtualKeyFromKey(key),
                (modifiers & ModifierKeys.Shift) != 0 ? Keys.LeftShift : Keys.None,
                (modifiers & ModifierKeys.Control) != 0 ? Keys.LeftControl : Keys.None,
                (modifiers & ModifierKeys.Alt) != 0 ? Keys.LeftAlt : Keys.None);

            bool canRoute = !e.Canceled && !keyEvent.Handled && (modifiers & ModifierKeys.Windows) == 0
                && CanRouteToolWindowShortcut(keyEvent.KeyboardDevice.ActiveSource?.RootVisual as Window);
            if (shortcutPressTracker.TryHandleKeyDown(key, keyboardState, canRoute, viewModel.ToggleHelpToolCommand,
                viewModel.ToggleSettingsToolCommand, viewModel.ToggleLogToolCommand, viewModel.ToggleTrainPathToolCommand))
            {
                keyEvent.Handled = true;
                e.Cancel();
            }
        }

        private void ShortcutApplication_Deactivated(object sender, EventArgs e)
        {
            shortcutPressTracker.Reset();
        }

        private void MapHost_ToolWindowShortcutRequested(object sender, ToolWindowShortcutRequestedEventArgs e)
        {
            // The game thread queued this request. Activation, modal state and command availability may
            // have changed before the dispatcher delivers it.
            Window activeWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
            if (CanRouteToolWindowShortcut(activeWindow))
                TryHandleToolWindowShortcut(e.Command, false);
        }

        private bool CanRouteToolWindowShortcut(Window window)
        {
            if (disposed || isShuttingDown || !IsEnabled || ComponentDispatcher.IsThreadModal
                || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished
                || window == null || !window.IsActive || !window.IsEnabled
                || !NativeMethods.IsForegroundWindowOwnedByCurrentProcess())
                return false;

            // Ownership alone would also admit dialogs. Floating panes must belong to this exact layout.
            return ReferenceEquals(window, this)
                || window is LayoutFloatingWindowControl floatingWindow
                    && ReferenceEquals(floatingWindow.Model?.Root?.Manager, DockingManager);
        }

        private bool TryHandleToolWindowShortcut(UserCommand command, bool isRepeat)
        {
            return ToolWindowShortcutRouter.TryHandle(command, isRepeat, viewModel.ToggleHelpToolCommand,
                viewModel.ToggleSettingsToolCommand, viewModel.ToggleLogToolCommand, viewModel.ToggleTrainPathToolCommand);
        }

        private void UnsubscribeToolWindowShortcuts()
        {
            shortcutInputManager.PreProcessInput -= InputManager_PreProcessInput;
            shortcutApplication?.Deactivated -= ShortcutApplication_Deactivated;
            shortcutPressTracker.Reset();
            MapHost.ToolWindowShortcutRequested -= MapHost_ToolWindowShortcutRequested;
        }
    }
}
