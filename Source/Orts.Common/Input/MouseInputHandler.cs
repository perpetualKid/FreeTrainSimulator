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
            if (null == mouseInputGameComponent)
                throw new ArgumentNullException(nameof(mouseInputGameComponent));

            this.userCommandController = userCommandController ?? throw new ArgumentNullException(nameof(userCommandController));
            this.keyboardInputGameComponent = keyboardInputGameComponent;

            mouseInputGameComponent.AddMouseEvent(MouseMovedEventType.MouseMovedLeftButtonDown, MouseDraggedEvent);
            mouseInputGameComponent.AddMouseEvent(MouseWheelEventType.MouseWheelChanged, MouseWheelEvent);
        }


        private void MouseDraggedEvent(Point position, Vector2 delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.PointerDragged, new PointerMoveCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }

        private void MouseWheelEvent(Point position, int delta, GameTime gameTime)
        {
            userCommandController.Trigger(CommonUserCommand.ZoomChanged, new ZoomCommandArgs() { Delta = delta, Position = position }, gameTime, keyboardInputGameComponent?.KeyModifiers ?? KeyModifiers.None);
        }
    }
}
