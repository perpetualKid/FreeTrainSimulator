
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
                UserCommand.DisplaySettingsWindow => new UserCommandModifiableKeyInput(Keys.F10, windowTab),
                UserCommand.DisplayLogWindow => new UserCommandModifiableKeyInput(Keys.F11, windowTab),
                UserCommand.DisplayTrainPathWindow => new UserCommandModifiableKeyInput(Keys.F8, windowTab),
                UserCommand.PathEditorUndo => new UserCommandKeyInput(Keys.Z, KeyModifiers.Control),
                UserCommand.PathEditorRedo => new UserCommandKeyInput(Keys.Y, KeyModifiers.Control),
                UserCommand.PathEditorAlternateRedo => new UserCommandKeyInput(Keys.Z, KeyModifiers.Control | KeyModifiers.Shift),
                UserCommand.RemoveSelectedViaPoint => new UserCommandKeyInput(Keys.Delete),
                UserCommand.CommitPathPlacement => new UserCommandKeyInput(Keys.Enter),
                UserCommand.NextRouteCandidate => new UserCommandKeyInput(Keys.Tab),
                UserCommand.PreviousRouteCandidate => new UserCommandKeyInput(Keys.Tab, KeyModifiers.Shift),
                UserCommand.AcceptRouteCandidate => new UserCommandKeyInput(Keys.Space),
                _ => throw new System.InvalidCastException(),
            };
        });
    }
}
