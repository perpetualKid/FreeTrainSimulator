using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;
using Orts.Graphics.Shaders;

namespace Orts.Graphics.Window
{
    public class WindowManager : DrawableGameComponent
    {
        private readonly List<WindowBase> windows = new List<WindowBase>();

        internal Texture2D windowTexture;
        private WindowBase mouseActiveWindow;
        private readonly SpriteBatch spriteBatch;

        public System.Drawing.Font TextFontDefault { get; }

        private WindowManager(Game game) :
            base(game)
        {
            DrawOrder = 100;

            spriteBatch = new SpriteBatch(GraphicsDevice);
            using (FileStream stream = File.OpenRead(".\\Content\\Window.png"))
            {
                windowTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }

            TextFontDefault = FontManager.Instance("Segoe UI", System.Drawing.FontStyle.Regular)[12];
            MaterialManager.Initialize(game.GraphicsDevice);
        }

        public void AddForms()
        {
            windows.Add(new TestWindow(this, new Point(100, 100), "Test"));
            windows.Add(new TestWindow(this, new Point(200, 150), "Another Test"));
        }

        public static WindowManager GetInstance<T>(Game game, UserCommandController<T> userCommandController) where T : Enum
        {
            if (null == userCommandController)
                throw new ArgumentNullException(nameof(userCommandController));

            WindowManager instance = new WindowManager(game);
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, instance.MouseClickedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDown, instance.MouseDownEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, instance.MouseReleasedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, instance.MouseDraggingEvent);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, instance.WindowScrollEvent);
            return instance;
        }

        private void WindowScrollEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        private void MouseDraggingEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerMoveCommandArgs moveCommandArgs)
            {
                if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    mouseActiveWindow.HandleMouseDrag(moveCommandArgs.Position, moveCommandArgs.Delta, keyModifiers);
                }
                else if (windows.LastOrDefault(w => w.Location.Contains(moveCommandArgs.Position)) != null)
                        userCommandArgs.Handled = true;
            }
        }

        private void MouseReleasedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        private void MouseDownEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        private void MouseClickedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                Point mouseDownPosition = pointerCommandArgs.Position;
                mouseActiveWindow = windows.LastOrDefault(w => w.Location.Contains(pointerCommandArgs.Position));
                if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    if (mouseActiveWindow != windows.Last())
                        if (windows.Remove(mouseActiveWindow))
                            windows.Add(mouseActiveWindow);

                    //                mouseActiveWindow?.MousePressed(pointerCommandArgs.Position, keyModifiers);
                }
            }
}

        public override void Initialize()
        {
            foreach (var window in windows)
            {
                window.Initialize();
                window.Layout();
            }
            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            //Matrix XNAView = Matrix.CreateTranslation(-GraphicsDevice.Viewport.Width / 2, -GraphicsDevice.Viewport.Height / 2, 0) *
            //    Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
            //    Matrix.CreateScale(1.0f, -1.0f, 1.0f);
            //// Project into a flat view of the same size as the viewport.
            //Matrix XNAProjection = Matrix.CreateOrthographic(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 1);

            foreach (WindowBase window in windows)
            {
                GraphicsDevice.BlendState = BlendState.NonPremultiplied;
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                window.RenderWindow();

                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;

                window.DrawContent(spriteBatch);
            }
            base.Draw(gameTime);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
