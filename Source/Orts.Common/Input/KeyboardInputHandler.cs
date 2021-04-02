using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class KeyboardInputHandler<T> where T : Enum
    {
        private ILookup<int, T> userCommandsLookup;
        private ILookup<int, (T, KeyModifiers)> modifyableCommandsLookup;
        private UserCommandController<T> userCommandController;

        public void Initialize(EnumArray<UserCommandInput, T> userCommands, KeyboardInputGameComponent inputGameComponent, UserCommandController<T> userCommandController)
        {
            if (null == userCommands)
                throw new ArgumentNullException(nameof(userCommands));

            if (null == inputGameComponent)
                throw new ArgumentNullException(nameof(inputGameComponent));

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));

            userCommandsLookup = EnumExtension.GetValues<T>().Where(command => userCommands[command] is UserCommandKeyInput).SelectMany(command =>
            {
                List<(int keyEventCode, T command)> result = new List<(int, T)>();
                UserCommandKeyInput keyInput = userCommands[command] as UserCommandKeyInput;
                if (Enum.IsDefined(typeof(KeyEventType), keyInput.KeyEventType))
                    result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers, keyInput.KeyEventType), command));
                else
                {
                    if ((keyInput.KeyEventType & KeyEventType.KeyPressed) == KeyEventType.KeyPressed)
                        result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers, KeyEventType.KeyPressed), command));
                    if ((keyInput.KeyEventType & KeyEventType.KeyReleased) == KeyEventType.KeyReleased)
                        result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers, KeyEventType.KeyReleased), command));
                    if ((keyInput.KeyEventType & KeyEventType.KeyDown) == KeyEventType.KeyDown)
                        result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers, KeyEventType.KeyDown), command));
                }
                return result;
            }).ToLookup(i => i.keyEventCode, c => c.command);

            modifyableCommandsLookup = EnumExtension.GetValues<T>().Where(command => userCommands[command] is UserCommandModifiableKeyInput).SelectMany(command =>
            {
                UserCommandModifiableKeyInput keyInput = userCommands[command] as UserCommandModifiableKeyInput;
                List<(int keyEventCode, T command, KeyModifiers modifiers)> result = new List<(int, T, KeyModifiers)>();

                foreach (KeyEventType keyEventType in EnumExtension.GetValues<KeyEventType>())
                {
                    if ((keyInput.KeyEventType & keyEventType) == keyEventType)
                    {
                        if (keyInput.IgnoreShift)
                            result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers | KeyModifiers.Shift, keyEventType), command, keyInput.Modifiers));
                        if (keyInput.IgnoreControl)
                            result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers | KeyModifiers.Control, keyEventType), command, keyInput.Modifiers));
                        if (keyInput.IgnoreAlt)
                            result.Add((KeyboardInputGameComponent.KeyEventCode(keyInput.Key, keyInput.Modifiers | KeyModifiers.Alt, keyEventType), command, keyInput.Modifiers));
                    }
                }
                return result;

            }).ToLookup(i => i.keyEventCode, c => (c.command, c.modifiers));

            inputGameComponent.AddInputHandler(Trigger);

        }

        public void Trigger(int eventCode, GameTime gameTime, KeyEventType eventType, KeyModifiers modifiers)
        {
            foreach (T command in userCommandsLookup[eventCode])
                userCommandController.Trigger(command, new KeyCommandArgs() { KeyEventType = eventType }, gameTime);

            foreach ((T T, KeyModifiers KeyModifiers) modifyableUserCommand in modifyableCommandsLookup[eventCode])
            {
                KeyModifiers additionalModifiers = modifyableUserCommand.KeyModifiers ^ modifiers;
                userCommandController.Trigger(modifyableUserCommand.T, new ModifiableKeyCommandArgs() { KeyEventType = eventType, AddtionalModifiers = additionalModifiers }, gameTime) ;
            }
        }
    }
}
