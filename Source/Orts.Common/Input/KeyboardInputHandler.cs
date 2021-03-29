using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class KeyboardInputHandler<T> where T : Enum
    {
        private readonly Dictionary<int, T> userCommands = new Dictionary<int, T>();
        private readonly Dictionary<int, (T, KeyModifiers)> modifyableUserCommands = new Dictionary<int, (T, KeyModifiers)>();
        private UserCommandController<T> userCommandController;

        public void Initialize(EnumArray<UserCommandInput, T> userCommands, KeyboardInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            if (null == userCommands)
                throw new ArgumentNullException(nameof(userCommands));

            if (null == inputGameComponent)
                throw new ArgumentNullException(nameof(inputGameComponent));

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));

            foreach (T command in EnumExtension.GetValues<T>())
            {
                if (userCommands[command] is UserCommandKeyInput input)
                {
                    this.userCommands.Add(KeyboardInputGameComponent.KeyEventCode(input.Key, input.Modifiers, input.KeyEventType), command);
                    if (input is UserCommandModifiableKeyInput modifiableKeyInput)
                    {
                        if (modifiableKeyInput.IgnoreShift)
                            modifyableUserCommands.Add(KeyboardInputGameComponent.KeyEventCode(input.Key, input.Modifiers | KeyModifiers.Shift, input.KeyEventType), (command, input.Modifiers));
                        if (modifiableKeyInput.IgnoreControl)
                            modifyableUserCommands.Add(KeyboardInputGameComponent.KeyEventCode(input.Key, input.Modifiers | KeyModifiers.Control, input.KeyEventType), (command, input.Modifiers));
                        if (modifiableKeyInput.IgnoreAlt)
                            modifyableUserCommands.Add(KeyboardInputGameComponent.KeyEventCode(input.Key, input.Modifiers | KeyModifiers.Alt, input.KeyEventType), (command, input.Modifiers));
                    }
                }
            }

            inputGameComponent.AddInputHandler(Trigger);

        }

        public void Trigger(int eventCode, GameTime gameTime, KeyModifiers modifiers)
        {
            if (userCommands.TryGetValue(eventCode, out T userCommand))
            {
                userCommandController.Trigger(userCommand, gameTime);
            }
            else if (modifyableUserCommands.TryGetValue(eventCode, out (T T, KeyModifiers KeyModifiers) modifyableUserCommand))
            {
                KeyModifiers additionalModifiers = modifyableUserCommand.KeyModifiers ^ modifiers;
                userCommandController.Trigger(modifyableUserCommand.T, gameTime, additionalModifiers);
            }
        }
    }
}
