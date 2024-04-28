
using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Input;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
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
            UserCommands[UserCommand.ChangeScreenMode] = new UserCommandKeyInput(Keys.Enter, KeyModifiers.Alt);
            UserCommands[UserCommand.ResetZoomAndLocation] = new UserCommandKeyInput(Keys.R);
            UserCommands[UserCommand.MoveLeft] = new UserCommandModifiableKeyInput(Keys.Left, moveFast, moveSlow);
            UserCommands[UserCommand.MoveRight] = new UserCommandModifiableKeyInput(Keys.Right, moveFast, moveSlow);
            UserCommands[UserCommand.MoveUp] = new UserCommandModifiableKeyInput(Keys.Up, moveFast, moveSlow);
            UserCommands[UserCommand.MoveDown] = new UserCommandModifiableKeyInput(Keys.Down, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomIn] = new UserCommandModifiableKeyInput(Keys.PageUp, moveFast, moveSlow);
            UserCommands[UserCommand.ZoomOut] = new UserCommandModifiableKeyInput(Keys.PageDown, moveFast, moveSlow);
            UserCommands[UserCommand.FollowTrain] = new UserCommandKeyInput(Keys.F);
            UserCommands[UserCommand.DisplayDebugScreen] = new UserCommandModifiableKeyInput(Keys.F5, windowTab);
            UserCommands[UserCommand.DisplaySignalStateWindow] = new UserCommandKeyInput(Keys.F12);
            UserCommands[UserCommand.DisplayHelpWindow] = new UserCommandModifiableKeyInput(Keys.F1, windowTab);
            UserCommands[UserCommand.DebugStep] = new UserCommandKeyInput(Keys.F10);
            UserCommands[UserCommand.DisplaySettingsWindow] = new UserCommandKeyInput(Keys.F2);
            UserCommands[UserCommand.DisplayTrainInfoWindow] = new UserCommandKeyInput(Keys.F4);
        }
    }
}
