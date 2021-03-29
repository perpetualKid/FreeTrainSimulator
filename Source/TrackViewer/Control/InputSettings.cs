
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

            UserCommands[UserCommand.PrintScreen] = new UserCommandModifiableKeyInput(Keys.PrintScreen, KeyModifiers.None);
            UserCommands[UserCommand.NewInstance] = new UserCommandModifiableKeyInput(Keys.F, KeyModifiers.None);
            UserCommands[UserCommand.ChangeScreenMode] = new UserCommandModifiableKeyInput(Keys.Enter, KeyModifiers.Alt);
            UserCommands[UserCommand.QuitGame] = new UserCommandModifiableKeyInput(Keys.Q, KeyModifiers.None);
            UserCommands[UserCommand.ResetZoomAndLocation] = new UserCommandModifiableKeyInput(Keys.R, KeyModifiers.None);
            UserCommands[UserCommand.MoveLeft] = new UserCommandModifiableKeyInput(Keys.Left, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.MoveRight] = new UserCommandModifiableKeyInput(Keys.Right, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.MoveUp] = new UserCommandModifiableKeyInput(Keys.Up, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.MoveDown] = new UserCommandModifiableKeyInput(Keys.Down, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.MoveDown] = new UserCommandModifiableKeyInput(Keys.Down, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomIn] = new UserCommandModifiableKeyInput(Keys.PageUp, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomOut] = new UserCommandModifiableKeyInput(Keys.PageDown, KeyModifiers.None, KeyEventType.KeyDown, moveFast, moveSlow);
        }
    }
}
