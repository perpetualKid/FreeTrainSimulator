using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Input
{
    public class RailDriverInputHandler<T> where T : Enum
    {
        private UserCommandController<T> userCommandController;
        private ILookup<int, T> userCommandsLookup;
        private RailDriverInputGameComponent inputGameComponent;

        // assume the Emergency Stop Commands are never remapped
        private const byte EmergencyStopCommandUp = 36;
        private const byte EmergencyStopCommandDown = 37;

        private const byte HornCommandUp = 42;
        private const byte HornCommandDown = 43;

        public void Initialize(EnumArray<byte, T> userCommands, RailDriverInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            ArgumentNullException.ThrowIfNull(userCommands);

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));
            this.inputGameComponent = inputGameComponent ?? throw new ArgumentNullException(nameof(inputGameComponent));

            (T Command, byte CommandValue, bool AvailableForMapping) emergencyCommand = (default, byte.MaxValue, false);
            (T Command, byte CommandValue, bool AvailableForMapping) hornCommand = (default, byte.MaxValue, false);
            foreach (T command in EnumExtension.GetValues<T>())
            {
                byte commandByte = userCommands[command];
                switch (commandByte)
                {
                    case EmergencyStopCommandUp:
                    case EmergencyStopCommandDown:
                        if (emergencyCommand.CommandValue == byte.MaxValue)
                        {
                            emergencyCommand.Command = command;
                            emergencyCommand.CommandValue = commandByte;
                            emergencyCommand.AvailableForMapping = true;
                        }
                        else
                            emergencyCommand.AvailableForMapping = false;
                        break;
                    case HornCommandUp:
                    case HornCommandDown:
                        if (hornCommand.CommandValue == byte.MaxValue)
                        {
                            hornCommand.Command = command;
                            hornCommand.CommandValue = commandByte;
                            hornCommand.AvailableForMapping = true;
                        }
                        else
                            hornCommand.AvailableForMapping = false;
                        break;
                }
            }
            // doing some magic here to assign one Command to two different buttons. This is only avaialble for Emergency and Horn buttons on RailDriver

            List<(int keyEventCode, T command)> additionalCommands = new List<(int, T)>();
            if (emergencyCommand.AvailableForMapping)
            {
                byte command = emergencyCommand.CommandValue == EmergencyStopCommandUp ? EmergencyStopCommandDown : EmergencyStopCommandUp;
                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                    additionalCommands.Add((RailDriverInputGameComponent.KeyEventCode(command, keyEventType), emergencyCommand.Command));
            }
            if (hornCommand.AvailableForMapping)
            {
                byte command = hornCommand.CommandValue == HornCommandUp ? HornCommandDown : HornCommandUp;
                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                    additionalCommands.Add((RailDriverInputGameComponent.KeyEventCode(command, keyEventType), hornCommand.Command));
            }

            userCommandsLookup = EnumExtension.GetValues<T>().Where(command => userCommands[command] < byte.MaxValue).SelectMany(command =>
            {
                List<(int keyEventCode, T command)> result = new List<(int, T)>();
                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                    result.Add((RailDriverInputGameComponent.KeyEventCode(userCommands[command], keyEventType), command));
                return result;
            }).Concat(additionalCommands).ToLookup(i => i.keyEventCode, c => c.command);

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
                case RailDriverHandleEventType.Lights:
                    command = AnalogUserCommand.Light;
                    break;
                case RailDriverHandleEventType.Wipers:
                    command = AnalogUserCommand.Wiper;
                    break;
                case RailDriverHandleEventType.Direction:
                    command = AnalogUserCommand.Direction;
                    break;
                case RailDriverHandleEventType.Throttle:
                    command = AnalogUserCommand.Throttle;
                    break;
                case RailDriverHandleEventType.DynamicBrake:
                    command = AnalogUserCommand.DynamicBrake;
                    break;
                case RailDriverHandleEventType.TrainBrake:
                    command = AnalogUserCommand.TrainBrake;
                    break;
                case RailDriverHandleEventType.EngineBrake:
                    command = AnalogUserCommand.EngineBrake;
                    break;
                case RailDriverHandleEventType.BailOff:
                    command = AnalogUserCommand.BailOff;
                    break;
                case RailDriverHandleEventType.Emergency:
                    command = AnalogUserCommand.Emergency;
                    break;
                case RailDriverHandleEventType.CabActivity:
                    command = AnalogUserCommand.CabActivity;
                    break;
            }
            userCommandController.Trigger(command, commandArgs, gameTime);
        }

        private void ShowSpeedValue(ControllerCommandArgs controllerCommandArgs)
        {
            if (controllerCommandArgs is ControllerCommandArgs<double> speedArgs)
                inputGameComponent.ShowSpeed(speedArgs.Value);
        }

        private void Activate(ControllerCommandArgs controllerCommandArgs)
        {
            inputGameComponent.Activate();
        }

    }
}
