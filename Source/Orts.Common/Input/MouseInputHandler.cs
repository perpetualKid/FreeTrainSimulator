using System;

using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class MouseInputHandler<T> where T: Enum
    {
        private UserCommandController<T> userCommandController;
        private KeyboardInputGameComponent keyboardInputGameComponent;

        public void Initialize(MouseInputGameComponent mouseInputGameComponent, KeyboardInputGameComponent keyboardInputGameComponent, UserCommandController<T> userCommandController)
        {
            ArgumentNullException.ThrowIfNull(mouseInputGameComponent);

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));
            this.keyboardInputGameComponent = keyboardInputGameComponent;

            mouseInputGameComponent.AddMouseEvent(MouseMovedEventType.MouseMoved, MouseMovedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.LeftButtonPressed, LeftMouseButtonPressedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.LeftButtonDown, LeftMouseButtonDownEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.LeftButtonReleased, LeftMouseButtonReleasedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseMovedEventType.MouseMovedLeftButtonDown, MouseDraggedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.RightButtonPressed, RightMouseButtonPressedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.RightButtonDown, RightMouseButtonDownEvent);
            mouseInputGameComponent.AddMouseEvent(MouseButtonEventType.RightButtonReleased, RightMouseButtonReleasedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseMovedEventType.MouseMovedRightButtonDown, MouseRightDraggedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseWheelEventType.MouseWheelChanged, MouseWheelEvent);
        }

        private void MouseMovedEvent(Point position, Vector2 delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerMoved, new PointerMoveCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void LeftMouseButtonPressedEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerPressed, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void LeftMouseButtonDownEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerDown, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void LeftMouseButtonReleasedEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerReleased, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void MouseDraggedEvent(Point position, Vector2 delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerDragged, new PointerMoveCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void RightMouseButtonPressedEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.AlternatePointerPressed, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void RightMouseButtonDownEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.AlternatePointerDown, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void RightMouseButtonReleasedEvent(Point position, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.AlternatePointerReleased, new PointerCommandArgs() { Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void MouseRightDraggedEvent(Point position, Vector2 delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.AlternatePointerDragged, new PointerMoveCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void MouseWheelEvent(Point position, int delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.VerticalScrollChanged, new ScrollCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }
    }
}
