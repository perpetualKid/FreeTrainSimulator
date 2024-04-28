
using System;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Orts.Common.Input
{
    public class MouseInputGameComponent : GameComponent
    {
        public delegate void MouseMoveEvent(Point position, Vector2 delta, GameTime gameTime);
        public delegate void MouseButtonEvent(Point position, GameTime gameTime);
        public delegate void MouseWheelEvent(Point position, int delta, GameTime gameTime);

        private MouseState currentMouseState;
        private MouseState previousMouseState;
        private readonly EnumArray<MouseMoveEvent, MouseMovedEventType> mouseMoveEvents = new EnumArray<MouseMoveEvent, MouseMovedEventType>();
        private readonly EnumArray<MouseButtonEvent, MouseButtonEventType> mouseButtonEvents = new EnumArray<MouseButtonEvent, MouseButtonEventType>();
        private readonly EnumArray<MouseWheelEvent, MouseWheelEventType> mouseWheelEvents = new EnumArray<MouseWheelEvent, MouseWheelEventType>();

        private readonly bool isTouchEnabled;

        public MouseInputGameComponent(Game game) : base(game)
        {
            try
            {
                isTouchEnabled = TouchPanel.GetCapabilities().IsConnected;
            }
            catch (NullReferenceException)
            { 
                isTouchEnabled = false;
            }
        }

        public ref readonly MouseState MouseState => ref currentMouseState;

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
            if (!Game.IsActive)
            {
                currentMouseState = default;
                return;
            }
            (currentMouseState, previousMouseState) = (previousMouseState, currentMouseState);
            currentMouseState = Mouse.GetState(Game.Window);
            MouseState otherMouseState = Mouse.GetState();

            if (!Game.GraphicsDevice.PresentationParameters.Bounds.Contains(currentMouseState.Position))
            {
                return;
            }

            void MouseButtonEvent(ButtonState currentButton, ButtonState previousButton, MouseButtonEventType down, MouseButtonEventType pressed, MouseButtonEventType released)
            {
                if (currentButton == ButtonState.Pressed)
                {
                    if (previousButton == ButtonState.Pressed)
                        mouseButtonEvents[down]?.Invoke(currentMouseState.Position, gameTime);
                    else
                        mouseButtonEvents[pressed]?.Invoke(currentMouseState.Position, gameTime);
                }
                else if (previousButton == ButtonState.Pressed)
                    mouseButtonEvents[released]?.Invoke(currentMouseState.Position, gameTime);
            }

            MouseButtonEvent(currentMouseState.LeftButton, previousMouseState.LeftButton, MouseButtonEventType.LeftButtonDown, MouseButtonEventType.LeftButtonPressed, MouseButtonEventType.LeftButtonReleased);
            MouseButtonEvent(currentMouseState.RightButton, previousMouseState.RightButton, MouseButtonEventType.RightButtonDown, MouseButtonEventType.RightButtonPressed, MouseButtonEventType.RightButtonReleased);
            MouseButtonEvent(currentMouseState.MiddleButton, previousMouseState.MiddleButton, MouseButtonEventType.MiddleButtonDown, MouseButtonEventType.MiddleButtonPressed, MouseButtonEventType.MiddleButtonReleased);
            MouseButtonEvent(currentMouseState.XButton1, previousMouseState.XButton1, MouseButtonEventType.XButton1Down, MouseButtonEventType.XButton1Pressed, MouseButtonEventType.XButton1Released);
            MouseButtonEvent(currentMouseState.XButton2, previousMouseState.XButton2, MouseButtonEventType.XButton2Down, MouseButtonEventType.XButton2Pressed, MouseButtonEventType.XButton2Released);

            if (currentMouseState != previousMouseState && previousMouseState != default)
            {
                TouchCollection touchState;
                if (isTouchEnabled && (touchState = TouchPanel.GetState(Game.Window).GetState()).Count > 0 && touchState[0].State != TouchLocationState.Released)
                {
                    if (touchState[0].TryGetPreviousLocation(out TouchLocation previousTouchState))
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMovedLeftButtonDown]?.Invoke(currentMouseState.Position, (touchState[0].Position - previousTouchState.Position), gameTime);
                    }
                }
                else if (currentMouseState.Position != previousMouseState.Position)
                {
                    if (currentMouseState.LeftButton == ButtonState.Pressed)
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMovedLeftButtonDown]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2(), gameTime);
                    }
                    else if (currentMouseState.RightButton == ButtonState.Pressed)
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMovedRightButtonDown]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2(), gameTime);
                    }
                    else
                    {
                        mouseMoveEvents[MouseMovedEventType.MouseMoved]?.Invoke(currentMouseState.Position, (currentMouseState.Position - previousMouseState.Position).ToVector2(), gameTime);
                    }
                }

                int mouseWheelDelta;
                if ((mouseWheelDelta = currentMouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue) != 0)
                    mouseWheelEvents[MouseWheelEventType.MouseWheelChanged]?.Invoke(currentMouseState.Position, mouseWheelDelta, gameTime);
                if ((mouseWheelDelta = currentMouseState.HorizontalScrollWheelValue - previousMouseState.HorizontalScrollWheelValue) != 0)
                    mouseWheelEvents[MouseWheelEventType.MouseHorizontalWheelChanged]?.Invoke(currentMouseState.Position, mouseWheelDelta, gameTime);
            }

            base.Update(gameTime);
        }

    }
}
