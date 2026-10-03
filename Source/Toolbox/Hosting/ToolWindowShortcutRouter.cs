using System;
using System.Collections.Generic;
using System.Windows.Input;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Toolbox.Settings;

using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Tracks WPF physical presses independently of later input-stage repeat bookkeeping.
    /// </summary>
    internal sealed class ToolWindowShortcutPressTracker
    {
        private readonly HashSet<Key> pressedKeys = new();

        /// <summary>
        /// Records every preview key down and routes eligible shortcuts without retoggling held keys.
        /// </summary>
        public bool TryHandleKeyDown(Key key, KeyboardState keyboardState, bool canRoute, ICommand toggleHelp,
            ICommand toggleSettings, ICommand toggleLog, ICommand toggleTrainPath)
        {
            bool isRepeat = !pressedKeys.Add(key);
            return canRoute && ToolWindowShortcutRouter.TryMatch(keyboardState, out UserCommand command)
                && ToolWindowShortcutRouter.TryHandle(command, isRepeat, toggleHelp, toggleSettings, toggleLog, toggleTrainPath);
        }

        /// <summary>
        /// Releases a key even when its input event is not eligible for shortcut routing.
        /// </summary>
        public void ReleaseKey(Key key)
        {
            pressedKeys.Remove(key);
        }

        /// <summary>
        /// Recovers releases missed while another input surface had focus.
        /// </summary>
        public void ReleaseMissingKeys(KeyboardDevice keyboardDevice)
        {
            ArgumentNullException.ThrowIfNull(keyboardDevice);
            pressedKeys.RemoveWhere(key => !keyboardDevice.IsKeyDown(key));
        }

        /// <summary>
        /// Forgets presses when the application deactivates or shortcut handling ends.
        /// </summary>
        public void Reset()
        {
            pressedKeys.Clear();
        }
    }

    /// <summary>
    /// Matches configured shell tool-window shortcuts and executes their existing toggle commands.
    /// </summary>
    internal static class ToolWindowShortcutRouter
    {
        /// <summary>
        /// Finds a shell shortcut using the configured key and modifier semantics.
        /// </summary>
        public static bool TryMatch(KeyboardState keyboardState, out UserCommand command)
        {
            if (keyboardState.IsKeyDown(Keys.LeftWindows) || keyboardState.IsKeyDown(Keys.RightWindows))
            {
                command = default;
                return false;
            }

            if (InputSettings.UserCommands[UserCommand.DisplayHelpWindow].IsKeyDown(keyboardState))
                command = UserCommand.DisplayHelpWindow;
            else if (InputSettings.UserCommands[UserCommand.DisplaySettingsWindow].IsKeyDown(keyboardState))
                command = UserCommand.DisplaySettingsWindow;
            else if (InputSettings.UserCommands[UserCommand.DisplayLogWindow].IsKeyDown(keyboardState))
                command = UserCommand.DisplayLogWindow;
            else if (InputSettings.UserCommands[UserCommand.DisplayTrainPathWindow].IsKeyDown(keyboardState))
                command = UserCommand.DisplayTrainPathWindow;
            else
            {
                command = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a configured shortcut key was physically pressed rather than held through a modifier change.
        /// </summary>
        public static bool IsNewPress(UserCommand command, KeyboardState currentState, KeyboardState previousState)
        {
            return InputSettings.UserCommands[command] is UserCommandKeyInput input
                && !currentState.IsKeyDown(Keys.LeftWindows) && !currentState.IsKeyDown(Keys.RightWindows)
                && input.IsKeyDown(currentState)
                && !previousState.IsKeyDown(input.VirtualKey);
        }

        /// <summary>
        /// Handles an available toggle shortcut, consuming repeats without executing another toggle.
        /// </summary>
        public static bool TryHandle(UserCommand command, bool isRepeat, ICommand toggleHelp, ICommand toggleSettings,
            ICommand toggleLog, ICommand toggleTrainPath)
        {
            ICommand toggle = command switch
            {
                UserCommand.DisplayHelpWindow => toggleHelp,
                UserCommand.DisplaySettingsWindow => toggleSettings,
                UserCommand.DisplayLogWindow => toggleLog,
                UserCommand.DisplayTrainPathWindow => toggleTrainPath,
                _ => null,
            };
            if (toggle?.CanExecute(null) != true)
                return false;

            if (!isRepeat)
                toggle.Execute(null);
            return true;
        }
    }
}
