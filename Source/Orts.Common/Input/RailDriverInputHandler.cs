using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class RailDriverInputHandler<T> where T : Enum
    {
        private UserCommandController<T> userCommandController;
        private ILookup<int, T> userCommandsLookup;
        RailDriverInputGameComponent inputGameComponent;

        public void Initialize(EnumArray<UserCommandInput, T> allUserCommands, EnumArray<byte, T> userCommands, RailDriverInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));
            this.inputGameComponent = inputGameComponent ?? throw new ArgumentNullException(nameof(inputGameComponent));

            userCommandsLookup = EnumExtension.GetValues<T>().Where(command => userCommands[command] < byte.MaxValue).SelectMany(command =>
            {
                List<(int keyEventCode, T command)> result = new List<(int, T)>();
                UserCommandKeyInput keyInput = allUserCommands[command] as UserCommandKeyInput;
                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                {
                    result.Add((RailDriverInputGameComponent.KeyEventCode(userCommands[command], keyEventType), command));
                }
                return result;
            }).ToLookup(i => i.keyEventCode, c => c.command);

            userCommandController.AddControllerInputEvent(CommandControllerInput.Speed, ShowSpeedValue);
            userCommandController.AddControllerInputEvent(CommandControllerInput.Activate, Activate);
            inputGameComponent.AddInputHandler(Trigger);
        }

        private void Trigger(int eventCode, GameTime gameTime, KeyEventType eventType)
        {
            foreach (T command in userCommandsLookup[eventCode])
                userCommandController.Trigger(command, eventType, UserCommandArgs.Empty, gameTime);
        }

        private void ShowSpeedValue(ControllerCommandArgs controllerCommandArgs)
        {
            if (controllerCommandArgs is ControllerCommandArgs<double> speedArgs)
            {
                inputGameComponent.ShowSpeed(speedArgs.Value);
            }
        }

        private void Activate(ControllerCommandArgs controllerCommandArgs)
        {
            inputGameComponent.Activate();
        }

    }
}
