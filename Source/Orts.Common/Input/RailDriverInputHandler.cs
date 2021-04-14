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
        private RailDriverInputGameComponent inputGameComponent;

        public void Initialize(EnumArray<byte, T> userCommands, RailDriverInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));
            this.inputGameComponent = inputGameComponent ?? throw new ArgumentNullException(nameof(inputGameComponent));

            userCommandsLookup = EnumExtension.GetValues<T>().Where(command => userCommands[command] < byte.MaxValue).SelectMany(command =>
            {
                List<(int keyEventCode, T command)> result = new List<(int, T)>();
                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                {
                    result.Add((RailDriverInputGameComponent.KeyEventCode(userCommands[command], keyEventType), command));
                }
                return result;
            }).ToLookup(i => i.keyEventCode, c => c.command);

            userCommandController.AddControllerInputEvent(CommandControllerInput.Speed, ShowSpeedValue);
            userCommandController.AddControllerInputEvent(CommandControllerInput.Activate, Activate);
            inputGameComponent.AddInputHandler(TriggerButtonEvent);
            inputGameComponent.AddInputHandler(TriggerHandleEvent);
        }

        private void TriggerButtonEvent(int eventCode, GameTime gameTime, KeyEventType eventType)
        {
            foreach (T command in userCommandsLookup[eventCode])
                userCommandController.Trigger(command, eventType, UserCommandArgs.Empty, gameTime);
        }

        private void TriggerHandleEvent(RailDriverHandleEventType eventType, GameTime gameTime, UserCommandArgs commandArgs)
        {
            AnalogUserCommand command = AnalogUserCommand.None;
            switch (eventType)
            {
                case RailDriverHandleEventType.Lights: command = AnalogUserCommand.Light; break;
                case RailDriverHandleEventType.Wipers: command = AnalogUserCommand.Wiper; break;
                case RailDriverHandleEventType.Direction: command = AnalogUserCommand.Direction; break;
                case RailDriverHandleEventType.Throttle: command = AnalogUserCommand.Throttle; break;
                case RailDriverHandleEventType.DynamicBrake: command = AnalogUserCommand.DynamicBrake; break;
                case RailDriverHandleEventType.TrainBrake: command = AnalogUserCommand.TrainBrake; break;
                case RailDriverHandleEventType.EngineBrake: command = AnalogUserCommand.EngineBrake; break;
                case RailDriverHandleEventType.BailOff: command = AnalogUserCommand.BailOff; break;
                case RailDriverHandleEventType.Emergency: command = AnalogUserCommand.Emergency; break;
            }
            userCommandController.Trigger(command, commandArgs, gameTime);
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
