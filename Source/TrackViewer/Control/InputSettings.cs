
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Input;

namespace Orts.TrackViewer.Control
{
    public static class InputSettings
    {
        public static EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = new EnumArray<UserCommandInput, UserCommand>();

        public static void Initialize()
        {
            UserCommandModifierInput moveSlow = new UserCommandModifierInput(KeyModifiers.Control);
            UserCommandModifierInput moveFast = new UserCommandModifierInput(KeyModifiers.Shift);

            UserCommands[UserCommand.PrintScreen] = new UserCommandKeyInput(Keys.PrintScreen, KeyModifiers.None);
            UserCommands[UserCommand.NewInstance] = new UserCommandKeyInput(Keys.F, KeyModifiers.None);
            UserCommands[UserCommand.ChangeScreenMode] = new UserCommandKeyInput(Keys.Enter, KeyModifiers.Alt);
            UserCommands[UserCommand.QuitGame] = new UserCommandKeyInput(Keys.Q, KeyModifiers.None);
            UserCommands[UserCommand.ResetZoomAndLocation] = new UserCommandKeyInput(Keys.R, KeyModifiers.None);
            UserCommands[UserCommand.MoveLeft] = new UserCommandModifiableKeyInput(Keys.Left, KeyModifiers.None, moveFast, moveSlow);
            UserCommands[UserCommand.MoveRight] = new UserCommandModifiableKeyInput(Keys.Right, KeyModifiers.None, moveFast, moveSlow);
            UserCommands[UserCommand.MoveUp] = new UserCommandModifiableKeyInput(Keys.Up, KeyModifiers.None, moveFast, moveSlow);
            UserCommands[UserCommand.MoveDown] = new UserCommandModifiableKeyInput(Keys.Down, KeyModifiers.None, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomIn] = new UserCommandModifiableKeyInput(Keys.PageUp, KeyModifiers.None, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomOut] = new UserCommandModifiableKeyInput(Keys.PageDown, KeyModifiers.None, moveFast, moveSlow);
        }
    }
}
