
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Input;

namespace Orts.Toolbox.Settings
{
    public static class InputSettings
    {
        public static EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = new EnumArray<UserCommandInput, UserCommand>();

        public static void Initialize()
        {
            UserCommandModifierInput moveSlow = new UserCommandModifierInput(KeyModifiers.Control);
            UserCommandModifierInput moveFast = new UserCommandModifierInput(KeyModifiers.Shift);
            UserCommandModifierInput windowTab = new UserCommandModifierInput(KeyModifiers.Shift);

            // All UserCommandModifierInput commands go here.
            UserCommands[UserCommand.Cancel] = new UserCommandKeyInput(Keys.Escape);
            UserCommands[UserCommand.PrintScreen] = new UserCommandKeyInput(Keys.PrintScreen);
            UserCommands[UserCommand.NewInstance] = new UserCommandKeyInput(Keys.F);
            UserCommands[UserCommand.ChangeScreenMode] = new UserCommandKeyInput(Keys.Enter, KeyModifiers.Alt);
            UserCommands[UserCommand.QuitWindow] = new UserCommandKeyInput(Keys.Q);
            UserCommands[UserCommand.ResetZoomAndLocation] = new UserCommandKeyInput(Keys.R);
            UserCommands[UserCommand.MoveLeft] = new UserCommandModifiableKeyInput(Keys.Left, moveFast, moveSlow);
            UserCommands[UserCommand.MoveRight] = new UserCommandModifiableKeyInput(Keys.Right, moveFast, moveSlow);
            UserCommands[UserCommand.MoveUp] = new UserCommandModifiableKeyInput(Keys.Up, moveFast, moveSlow);
            UserCommands[UserCommand.MoveDown] = new UserCommandModifiableKeyInput(Keys.Down, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomIn] = new UserCommandModifiableKeyInput(Keys.PageUp, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomOut] = new UserCommandModifiableKeyInput(Keys.PageDown, moveFast, moveSlow);
            UserCommands[UserCommand.DisplayDebugScreen] = new UserCommandModifiableKeyInput(Keys.F5, windowTab);
            UserCommands[UserCommand.DisplayLocationWindow] = new UserCommandModifiableKeyInput(Keys.F12, windowTab);
            UserCommands[UserCommand.DisplayHelpWindow] = new UserCommandModifiableKeyInput(Keys.F1, windowTab);
            UserCommands[UserCommand.DisplayTrackNodeInfoWindow] = new UserCommandModifiableKeyInput(Keys.F4, windowTab);
            UserCommands[UserCommand.DisplayTrackItemInfoWindow] = new UserCommandModifiableKeyInput(Keys.F3, windowTab);
            UserCommands[UserCommand.DisplaySettingsWindow] = new UserCommandModifiableKeyInput(Keys.F10, windowTab);
            UserCommands[UserCommand.DisplayLogWindow] = new UserCommandModifiableKeyInput(Keys.F11, windowTab);
            UserCommands[UserCommand.DisplayTrainPathWindow] = new UserCommandModifiableKeyInput(Keys.F8, windowTab);
        }
    }
}
