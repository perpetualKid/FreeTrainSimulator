using System;
using System.Reflection;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Orts.Common.Input
{
    public class KeyboardInputGameComponent : GameComponent
    {
        public delegate void KeyEvent(Keys key, KeyModifiers modifiers, GameTime gameTime);

        private const int KeyPressShift = 8;
        private const int KeyDownShift = 13;
        private const int KeyUpShift = 17;

        private KeyboardState currentKeyboardState;
        private KeyboardState previousKeyboardState;
        private KeyboardState inActiveKeyboardState;
        private KeyModifiers previousModifiers;
        private KeyModifiers currentModifiers;
        private Keys[] previousKeys = Array.Empty<Keys>();

        private readonly IInputCapture inputCapture;

        private Action<int, GameTime, KeyEventType, KeyModifiers> inputActionHandler;
        private bool inActive;
        private readonly Action<bool> SetKeyboardActive;

        public KeyboardInputGameComponent(Game game) : base(game)
        {
            inputCapture = game as IInputCapture;

            //with multiple instances (such as seconday window for Dispatcher or another Toolbox instance, Keyboard is not activated despite
            //the window is activated when either just opening new window, or switching between windows other than keyboard
            //hence we simply brutforce set the keyboard to active when the game window is active
            MethodInfo setActiveMethod = typeof(Keyboard).GetMethod("SetActive", BindingFlags.NonPublic | BindingFlags.Static);
            SetKeyboardActive = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), setActiveMethod);
        }

        public ref readonly KeyboardState KeyboardState => ref currentKeyboardState;

        public KeyModifiers KeyModifiers => currentModifiers;

        public static int KeyEventCode(Keys key, KeyModifiers modifiers, KeyEventType keyEventType)
        {
            return keyEventType switch
            {
                KeyEventType.KeyDown => (int)key << KeyDownShift ^ (int)modifiers,
                KeyEventType.KeyPressed => (int)key << KeyPressShift ^ (int)modifiers,
                KeyEventType.KeyReleased => (int)key << KeyUpShift ^ (int)modifiers,
                _ => throw new NotSupportedException(),
            };
        }

        public void AddInputHandler(Action<int, GameTime, KeyEventType, KeyModifiers> inputAction)
        {
            inputActionHandler += inputAction;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Game.IsActive || (inputCapture?.InputCaptured ?? false))
            {
                if (!inActive)
                {
                    inActiveKeyboardState = currentKeyboardState;   // keep current keyboard state
                    currentKeyboardState = default;
                    inActive = true;
                }
                return;
            }

            if (inActive) // restore keyboard state
            {
                currentKeyboardState = inActiveKeyboardState;
                inActive = false;
            }
            SetKeyboardActive(true);

            KeyboardState newState = Keyboard.GetState();

            #region keyboard update
            //if (currentKeyboardState != previousKeyboardState || currentKeyboardState.GetPressedKeyCount() != 0)
            if (currentKeyboardState != newState || newState.GetPressedKeyCount() != 0)
            {
                (currentKeyboardState, previousKeyboardState) = (previousKeyboardState, currentKeyboardState);
                (currentModifiers, previousModifiers) = (previousModifiers, currentModifiers);
                currentKeyboardState = newState;

                currentModifiers = KeyModifiers.None;
                if (currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift))
                    currentModifiers |= KeyModifiers.Shift;
                if (currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl))
                    currentModifiers |= KeyModifiers.Control;
                if (currentKeyboardState.IsKeyDown(Keys.LeftAlt) || currentKeyboardState.IsKeyDown(Keys.LeftAlt))
                    currentModifiers |= KeyModifiers.Alt;

                Keys[] currentKeys = currentKeyboardState.GetPressedKeys();
                foreach (Keys key in currentKeys)
                {
                    //if (key == Keys.LeftShift || key == Keys.RightShift || key == Keys.LeftControl || key == Keys.RightControl || key == Keys.LeftAlt || key == Keys.RightAlt)
                    if ((int)key > 159 && (int)key < 166)
                        continue;
                    if (previousKeyboardState.IsKeyDown(key) && (currentModifiers == previousModifiers))
                    {
                        // Key (still) down
                        int lookup = (int)key << KeyDownShift ^ (int)currentModifiers;
                        inputActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyDown, currentModifiers);
                    }
                    if (previousKeyboardState.IsKeyDown(key) && (currentModifiers != previousModifiers))
                    {
                        // Key Up, state may have changed due to a modifier changed
                        int lookup = (int)key << KeyUpShift ^ (int)previousModifiers;
                        inputActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyReleased, previousModifiers);
                    }
                    if (!previousKeyboardState.IsKeyDown(key) || (currentModifiers != previousModifiers))
                    {
                        //Key just pressed
                        int lookup = (int)key << KeyPressShift ^ (int)currentModifiers;
                        inputActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyPressed, currentModifiers);
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
                    int lookup = (int)key << KeyUpShift ^ (int)previousModifiers;
                    inputActionHandler?.Invoke(lookup, gameTime, KeyEventType.KeyReleased, previousModifiers);
                }
                previousKeys = currentKeys;
            }
            #endregion

            base.Update(gameTime);
        }

        public bool KeyState(Keys key, KeyModifiers modifiers, KeyEventType keyEventType)
        {
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    if (currentKeyboardState.IsKeyDown(key) && modifiers == currentModifiers && previousModifiers == currentModifiers == previousKeyboardState.IsKeyDown(key))
                        return true;
                    break;
                case KeyEventType.KeyPressed:
                    if (currentKeyboardState.IsKeyDown(key) && modifiers == currentModifiers && (previousModifiers != currentModifiers || !previousKeyboardState.IsKeyDown(key)))
                        return true;
                    break;
                case KeyEventType.KeyReleased:
                    if ((!currentKeyboardState.IsKeyDown(key) || previousModifiers != currentModifiers) && (previousModifiers == modifiers && previousKeyboardState.IsKeyDown(key)))
                        return true;
                    break;
                default:
                    throw new NotSupportedException();
            }
            return false;
        }
    }
}
