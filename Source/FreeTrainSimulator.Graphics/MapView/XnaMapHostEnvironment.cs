using System;
using System.Linq;

using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostEnvironment : IMapHostEnvironment
    {
        private readonly Game game;
        private readonly MouseInputGameComponent inputComponent;

        public XnaMapHostEnvironment(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            inputComponent = game.Components.OfType<MouseInputGameComponent>().Single();
            game.Window.ClientSizeChanged += GameWindow_ClientSizeChanged;
        }

        public Point ClientSize => game.Window.ClientBounds.Size;

        public Point PointerPosition => Microsoft.Xna.Framework.Input.Mouse.GetState().Position;

        public event EventHandler ClientSizeChanged;

        public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
        {
            inputComponent.AddMouseEvent(MouseMovedEventType.MouseMoved, handler);
        }

        public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
        {
            inputComponent.RemoveMouseEvent(MouseMovedEventType.MouseMoved, handler);
        }

        private void GameWindow_ClientSizeChanged(object sender, EventArgs e)
        {
            ClientSizeChanged?.Invoke(this, e);
        }
    }
}
