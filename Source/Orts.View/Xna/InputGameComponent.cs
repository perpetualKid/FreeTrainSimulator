using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Orts.Common;
using Orts.Common.Input;

using SharpDX.Direct3D11;

namespace Orts.View.Xna
{
    public class InputGameComponent : GameComponent
    {
        public enum KeyEventType
        {
            /// <summary>
            /// Key just pressed down
            /// </summary>
            KeyPressed = keyPressShift,
            /// <summary>
            /// Key held down
            /// </summary>
            KeyDown = keyDownShift,
            /// <summary>
            /// Key released
            /// </summary>
            KeyReleased = keyUpShift,

        }

        private const int keyPressShift = 8;
        private const int keyDownShift = 13;
        private const int keyUpShift = 17;

        private KeyboardState currentState;
        private KeyboardState previousState;
        private KeyModifiers previousModifiers;
        private Keys[] previousKeys = Array.Empty<Keys>();
        private readonly Dictionary<int, Action> keyEvents = new Dictionary<int, Action>();

        public InputGameComponent(Game game) : base(game)
        {
            AddKeyEvent(Keys.G, KeyModifiers.Control | KeyModifiers.Alt, KeyEventType.KeyPressed, new Action(() => Debug.WriteLine("Key G + Control-Alt pressed")));
            AddKeyEvent(Keys.PrintScreen, KeyModifiers.None, KeyEventType.KeyPressed, new Action(() => Debug.WriteLine("PrintScreen")));
        }

        public void AddKeyEvent(Keys key, KeyModifiers modifiers, KeyEventType keyEventType, Action action)
        {
            int lookupCode;
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    lookupCode = (int)key << keyDownShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyPressed:
                    lookupCode = (int)key << keyPressShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyReleased:
                    lookupCode = (int)key << keyUpShift ^ (int)modifiers;
                    break;
                default:
                    throw new NotSupportedException();
            }
            keyEvents.Add(lookupCode, action);
        }

        public override void Update(GameTime gameTime)
        {
            if (!Game.IsActive)
                return;
            (currentState, previousState) = (previousState, currentState);
            currentState = Keyboard.GetState();
         //   Debug.WriteLine($"{currentState}", Game.Window.Title);

            if (currentState == previousState && currentState.GetPressedKeyCount() == 0)
                return;

            KeyModifiers modifiers = KeyModifiers.None;
            if (currentState.IsKeyDown(Keys.LeftShift) || currentState.IsKeyDown(Keys.RightShift))
                modifiers |= KeyModifiers.Shift;
            if (currentState.IsKeyDown(Keys.LeftControl) || currentState.IsKeyDown(Keys.RightControl))
                modifiers |= KeyModifiers.Control;
            if (currentState.IsKeyDown(Keys.LeftAlt) || currentState.IsKeyDown(Keys.LeftAlt))
                modifiers |= KeyModifiers.Alt;

            Keys[] currentKeys = currentState.GetPressedKeys();
            foreach (Keys key in currentKeys)
            {
                //if (key == Keys.LeftShift || key == Keys.RightShift || key == Keys.LeftControl || key == Keys.RightControl || key == Keys.LeftAlt || key == Keys.RightAlt)
                if ((int)key > 159 && (int)key < 166)
                    continue;
                Debug.WriteLine($"{key} - {modifiers}", Game.Window.Title);
                if (previousState.IsKeyDown(key) && (modifiers == previousModifiers))
                {
                    // Key (still) down
                    int lookup = (int)key << keyDownShift ^ (int)modifiers;
                    if (keyEvents.TryGetValue(lookup, out Action action))
                    {
                        action.Invoke();
                    }
                }
                if (previousState.IsKeyDown(key) && (modifiers != previousModifiers))
                {
                    // Key Up, state may have changed due to a modifier changed
                    int lookup = (int)key << keyUpShift ^ (int)modifiers;
                    if (keyEvents.TryGetValue(lookup, out Action action))
                    {
                        action.Invoke();
                    }
                }
                if (!previousState.IsKeyDown(key) || (modifiers != previousModifiers))
                {
                    //Key just pressed
                    int lookup = (int)key << keyPressShift ^ (int)modifiers;
                    if (keyEvents.TryGetValue(lookup, out Action action))
                    {
                        action.Invoke();
                    }
                }
                int previousIndex = Array.IndexOf(previousKeys, key);//not  great, but considering this is mostly very few (<5) acceptable
                if (previousIndex > -1)
                    previousKeys[previousIndex] = Keys.None;
            }
            foreach (Keys key in previousKeys)
            {
                if (key == Keys.None)
                    continue;
                //if (key == Keys.LeftShift || key == Keys.RightShift || key == Keys.LeftControl || key == Keys.RightControl || key == Keys.LeftAlt || key == Keys.RightAlt)
                if ((int)key > 159 && (int)key < 166)
                    continue;
                // Key Up, not in current set of Keys Downs
                int lookup = (int)key << keyUpShift ^ (int)modifiers;
                if (keyEvents.TryGetValue(lookup, out Action action))
                {
                    action.Invoke();
                }
            }
            previousModifiers = modifiers;
            previousKeys = currentKeys;
            base.Update(gameTime);
        }

    }
}
