
using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Toolbox.Settings
{
    public static class InputSettings
    {
        public static EnumArray<UserCommandInput, UserCommand> UserCommands { get; } = new EnumArray<UserCommandInput, UserCommand>((UserCommand userCommand) =>
        {
            UserCommandModifierInput moveSlow = new UserCommandModifierInput(KeyModifiers.Control);
            UserCommandModifierInput moveFast = new UserCommandModifierInput(KeyModifiers.Shift);
            UserCommandModifierInput windowTab = new UserCommandModifierInput(KeyModifiers.Shift);
            return userCommand switch
            {
                // All UserCommandModifierInput commands go here.
                UserCommand.Cancel => new UserCommandKeyInput(Keys.Escape),
                UserCommand.PrintScreen => new UserCommandKeyInput(Keys.PrintScreen),
                UserCommand.NewInstance => new UserCommandKeyInput(Keys.F),
                UserCommand.ChangeScreenMode => new UserCommandKeyInput(Keys.Enter, KeyModifiers.Alt),
                UserCommand.QuitWindow => new UserCommandKeyInput(Keys.Q),
                UserCommand.ResetZoomAndLocation => new UserCommandKeyInput(Keys.R),
                UserCommand.MoveLeft => new UserCommandModifiableKeyInput(Keys.Left, moveFast, moveSlow),
                UserCommand.MoveRight => new UserCommandModifiableKeyInput(Keys.Right, moveFast, moveSlow),
                UserCommand.MoveUp => new UserCommandModifiableKeyInput(Keys.Up, moveFast, moveSlow),
                UserCommand.MoveDown => new UserCommandModifiableKeyInput(Keys.Down, moveFast, moveSlow),
                UserCommand.ZoomIn => new UserCommandModifiableKeyInput(Keys.PageUp, moveFast, moveSlow),
                UserCommand.ZoomOut => new UserCommandModifiableKeyInput(Keys.PageDown, moveFast, moveSlow),
                UserCommand.DisplayDebugScreen => new UserCommandModifiableKeyInput(Keys.F5, windowTab),
                UserCommand.DisplayLocationWindow => new UserCommandModifiableKeyInput(Keys.F12, windowTab),
                UserCommand.DisplayHelpWindow => new UserCommandModifiableKeyInput(Keys.F1, windowTab),
                UserCommand.DisplayTrackNodeInfoWindow => new UserCommandModifiableKeyInput(Keys.F4, windowTab),
                UserCommand.DisplayTrackItemInfoWindow => new UserCommandModifiableKeyInput(Keys.F3, windowTab),
                UserCommand.DisplaySettingsWindow => new UserCommandModifiableKeyInput(Keys.F10, windowTab),
                UserCommand.DisplayLogWindow => new UserCommandModifiableKeyInput(Keys.F11, windowTab),
                UserCommand.DisplayTrainPathWindow => new UserCommandModifiableKeyInput(Keys.F8, windowTab),
                _ => throw new System.InvalidCastException(),
            };
        });
    }
}
