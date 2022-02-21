
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Input;

namespace Orts.TrackViewer.Settings
{
    public static class InputSettings
    {
        public static EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = new EnumArray<UserCommandInput, UserCommand>();

        public static void Initialize()
        {
            UserCommandModifierInput moveSlow = new UserCommandModifierInput(KeyModifiers.Control);
            UserCommandModifierInput moveFast = new UserCommandModifierInput(KeyModifiers.Shift);

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
            UserCommands[UserCommand.DebugScreen] = new UserCommandKeyInput(Keys.F5);
            UserCommands[UserCommand.DebugScreenTab] = new UserCommandKeyInput(Keys.F5, KeyModifiers.Shift);
            UserCommands[UserCommand.LocationWindow] = new UserCommandKeyInput(Keys.F12);
            UserCommands[UserCommand.LocationWindowTab] = new UserCommandKeyInput(Keys.F12, KeyModifiers.Shift);
            UserCommands[UserCommand.HelpWindow] = new UserCommandKeyInput(Keys.F1);
            UserCommands[UserCommand.HelpWindowTab] = new UserCommandKeyInput(Keys.F1, KeyModifiers.Shift);
        }
    }
}
