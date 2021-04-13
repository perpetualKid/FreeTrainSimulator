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


        public void Initialize(EnumArray<UserCommandInput, T> allUserCommands, EnumArray<byte, T> userCommands, RailDriverInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            if (null == inputGameComponent)
                throw new ArgumentNullException(nameof(inputGameComponent));

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));

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

            inputGameComponent.AddInputHandler(Trigger);
        }

        private void Trigger(int eventCode, GameTime gameTime, KeyEventType eventType)
        {
            foreach (T command in userCommandsLookup[eventCode])
                userCommandController.Trigger(command, eventType, UserCommandArgs.Empty, gameTime);
        }
    }
}
