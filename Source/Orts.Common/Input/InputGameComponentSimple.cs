﻿using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Orts.Common.Input
{
    public class InputGameComponentSimple : GameComponent
    {

        public enum MouseMovedEventType
        {
            MouseMoved,
            MouseMovedLeftButtonDown,
            MouseMovedRightButtonDown,
        }

        public enum MouseWheelEventType
        {
            MouseWheelChanged,
            MouseHorizontalWheelChanged,
        }

        public enum MouseButtonEventType
        {
            LeftButtonPressed,
            LeftButtonDown,
            LeftButtonReleased,
            RightButtonPressed,
            RightButtonDown,
            RightButtonReleased,
            MiddleButtonPressed,
            MiddleButtonDown,
            MiddleButtonReleased,
            XButton1Pressed,
            XButton1Down,
            XButton1Released,
            XButton2Pressed,
            XButton2Down,
            XButton2Released,
        }

        public delegate void MouseMoveEvent(Point position, Vector2 delta);
        public delegate void MouseButtonEvent(Point position);
        public delegate void MouseWheelEvent(Point position, int delta);
        public delegate void KeyEvent(Keys key, KeyModifiers modifiers, GameTime gameTime);

        internal const int KeyPressShift = 8;
        internal const int KeyDownShift = 13;
        internal const int KeyUpShift = 17;

        private KeyboardState currentKeyboardState;
        private KeyboardState previousKeyboardState;
        private KeyModifiers previousModifiers;
        private KeyModifiers currentModifiers;
        private Keys[] previousKeys = Array.Empty<Keys>();
        private readonly Dictionary<int, KeyEvent> keyEvents = new Dictionary<int, KeyEvent>();

        private MouseState currentMouseState;
        private MouseState previousMouseState;
        private readonly EnumArray<MouseMoveEvent, MouseMovedEventType> mouseMoveEvents = new EnumArray<MouseMoveEvent, MouseMovedEventType>();
        private readonly EnumArray<MouseButtonEvent, MouseButtonEventType> mouseButtonEvents = new EnumArray<MouseButtonEvent, MouseButtonEventType>();
        private readonly EnumArray<MouseWheelEvent, MouseWheelEventType> mouseWheelEvents = new EnumArray<MouseWheelEvent, MouseWheelEventType>();

        private readonly IInputCapture inputCapture;

        private Action<int, GameTime, KeyModifiers> inputActionHandler;

        public InputGameComponentSimple(Game game) : base(game)
        {
            inputCapture = game as IInputCapture;
        }

        public ref readonly KeyboardState KeyboardState => ref currentKeyboardState;
        public ref readonly MouseState MouseState => ref currentMouseState;

        public static int KeyEventCode(Keys key, KeyModifiers modifiers, KeyEventType keyEventType)
        {
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    return (int)key << KeyDownShift ^ (int)modifiers;
                case KeyEventType.KeyPressed:
                    return (int)key << KeyPressShift ^ (int)modifiers;
                case KeyEventType.KeyReleased:
                    return (int)key << KeyUpShift ^ (int)modifiers;
                default:
                    throw new NotSupportedException();
            }
        }

        public void AddInputHandler(Action<int, GameTime, KeyModifiers> inputAction)
        {
            inputActionHandler += inputAction;
        }

        public void AddKeyEvent(Keys key, KeyModifiers modifiers, KeyEventType keyEventType, KeyEvent eventHandler)
        {
            int lookupCode;
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    lookupCode = (int)key << KeyDownShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyPressed:
                    lookupCode = (int)key << KeyPressShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyReleased:
                    lookupCode = (int)key << KeyUpShift ^ (int)modifiers;
                    break;
                default:
                    throw new NotSupportedException();
            }
            if (keyEvents.ContainsKey(lookupCode))
                keyEvents[lookupCode] += eventHandler;
            else
                keyEvents[lookupCode] = eventHandler;
        }

        public void RemoveKeyEvent(Keys key, KeyModifiers modifiers, KeyEventType keyEventType, KeyEvent eventHandler)
        {
            int lookupCode;
            switch (keyEventType)
            {
                case KeyEventType.KeyDown:
                    lookupCode = (int)key << KeyDownShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyPressed:
                    lookupCode = (int)key << KeyPressShift ^ (int)modifiers;
                    break;
                case KeyEventType.KeyReleased:
                    lookupCode = (int)key << KeyUpShift ^ (int)modifiers;
                    break;
                default:
                    throw new NotSupportedException();
            }
            if (keyEvents.ContainsKey(lookupCode))
                keyEvents[lookupCode] -= eventHandler;
        }

        public void AddMouseEvent(MouseMovedEventType mouseEventType, MouseMoveEvent eventHandler)
        {
            mouseMoveEvents[mouseEventType] += eventHandler;
        }

        public void RemoveMouseEvent(MouseMovedEventType mouseEventType, MouseMoveEvent eventHandler)
        {
            mouseMoveEvents[mouseEventType] -= eventHandler;
        }

        public void AddMouseEvent(MouseButtonEventType mouseEventType, MouseButtonEvent eventHandler)
        {
            mouseButtonEvents[mouseEventType] += eventHandler;
        }

        public void RemoveMouseEvent(MouseButtonEventType mouseEventType, MouseButtonEvent eventHandler)
        {
            mouseButtonEvents[mouseEventType] -= eventHandler;
        }

        public void AddMouseEvent(MouseWheelEventType mouseEventType, MouseWheelEvent eventHandler)
        {
            mouseWheelEvents[mouseEventType] += eventHandler;
        }

        public void RemoveMouseEvent(MouseWheelEventType mouseEventType, MouseWheelEvent eventHandler)
        {
            mouseWheelEvents[mouseEventType] -= eventHandler;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Game.IsActive || (inputCapture?.InputCaptured ?? false))
            {
                currentKeyboardState = default;
                currentMouseState = default;
                return;
            }
            (currentKeyboardState, previousKeyboardState) = (previousKeyboardState, currentKeyboardState);
            (currentMouseState, previousMouseState) = (previousMouseState, currentMouseState);
            (currentModifiers, previousModifiers) = (previousModifiers, currentModifiers);
            currentKeyboardState = Keyboard.GetState();
            currentMouseState = Mouse.GetState(Game.Window);


            #region keyboard update
            if (currentKeyboardState != previousKeyboardState || currentKeyboardState.GetPressedKeyCount() != 0)
            {
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
                        inputActionHandler.Invoke(lookup, gameTime, currentModifiers);
                    }
                    if (previousKeyboardState.IsKeyDown(key) && (currentModifiers != previousModifiers))
                    {
                        // Key Up, state may have changed due to a modifier changed
                        int lookup = (int)key << KeyUpShift ^ (int)previousModifiers;
                        inputActionHandler.Invoke(lookup, gameTime, previousModifiers);
                    }
                    if (!previousKeyboardState.IsKeyDown(key) || (currentModifiers != previousModifiers))
                    {
                        //Key just pressed
                        int lookup = (int)key << KeyPressShift ^ (int)currentModifiers;
                        inputActionHandler.Invoke(lookup, gameTime, currentModifiers);
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
                    inputActionHandler.Invoke(lookup, gameTime, previousModifiers);
                }
                previousKeys = currentKeys;
            }
            #endregion

            #region mouse updates
            if (currentMouseState != previousMouseState && previousMouseState != default)
            {
                if (currentMouseState.Position != previousMouseState.Position)
                {
                    if (currentMouseState.LeftButton == ButtonState.Pressed)
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMovedLeftButtonDown]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2());
                    }
                    else if (currentMouseState.LeftButton == ButtonState.Pressed)
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMovedRightButtonDown]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2());
                    }
                    else
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMoved]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2());
                    }
                }

                int mouseWheelDelta;
                if ((mouseWheelDelta = currentMouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue) != 0)
                    mouseWheelEvents[MouseWheelEventType.MouseWheelChanged]?.Invoke(currentMouseState.Position, mouseWheelDelta);
                if ((mouseWheelDelta = currentMouseState.HorizontalScrollWheelValue - previousMouseState.HorizontalScrollWheelValue) != 0)
                    mouseWheelEvents[MouseWheelEventType.MouseHorizontalWheelChanged]?.Invoke(currentMouseState.Position, mouseWheelDelta);

                void MouseButtonEvent(ButtonState currentButton, ButtonState previousButton, MouseButtonEventType down, MouseButtonEventType pressed, MouseButtonEventType released)
                {
                    if (currentButton == ButtonState.Pressed)
                    {
                        if (previousButton == ButtonState.Pressed)
                            mouseButtonEvents[down]?.Invoke(currentMouseState.Position);
                        else
                            mouseButtonEvents[pressed]?.Invoke(currentMouseState.Position);
                    }
                    else if (previousButton == ButtonState.Pressed)
                        mouseButtonEvents[released]?.Invoke(currentMouseState.Position);
                }

                MouseButtonEvent(currentMouseState.LeftButton, previousMouseState.LeftButton, MouseButtonEventType.LeftButtonDown, MouseButtonEventType.LeftButtonPressed, MouseButtonEventType.LeftButtonReleased);
                MouseButtonEvent(currentMouseState.RightButton, previousMouseState.RightButton, MouseButtonEventType.RightButtonDown, MouseButtonEventType.RightButtonPressed, MouseButtonEventType.RightButtonReleased);
                MouseButtonEvent(currentMouseState.MiddleButton, previousMouseState.MiddleButton, MouseButtonEventType.MiddleButtonDown, MouseButtonEventType.MiddleButtonPressed, MouseButtonEventType.MiddleButtonReleased);
                MouseButtonEvent(currentMouseState.XButton1, previousMouseState.XButton1, MouseButtonEventType.XButton1Down, MouseButtonEventType.XButton1Pressed, MouseButtonEventType.XButton1Released);
                MouseButtonEvent(currentMouseState.XButton2, previousMouseState.XButton2, MouseButtonEventType.XButton2Down, MouseButtonEventType.XButton2Pressed, MouseButtonEventType.XButton2Released);
            }
            else
            {
                if (currentMouseState.LeftButton == ButtonState.Pressed)
                    mouseButtonEvents[MouseButtonEventType.LeftButtonDown]?.Invoke(currentMouseState.Position);
                if (currentMouseState.RightButton == ButtonState.Pressed)
                    mouseButtonEvents[MouseButtonEventType.RightButtonDown]?.Invoke(currentMouseState.Position);
                if (currentMouseState.MiddleButton == ButtonState.Pressed)
                    mouseButtonEvents[MouseButtonEventType.MiddleButtonDown]?.Invoke(currentMouseState.Position);
                if (currentMouseState.XButton1 == ButtonState.Pressed)
                    mouseButtonEvents[MouseButtonEventType.XButton1Down]?.Invoke(currentMouseState.Position);
                if (currentMouseState.XButton2 == ButtonState.Pressed)
                    mouseButtonEvents[MouseButtonEventType.XButton2Down]?.Invoke(currentMouseState.Position);
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
