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

        private Matrix xnaView;
        private Matrix xnaProjection;
        internal ref readonly Matrix XNAView => ref xnaView;
        internal ref readonly Matrix XNAProjection => ref xnaProjection;
        internal readonly PopupWindowShader WindowShader;

        public System.Drawing.Font TextFontDefault { get; }

        private WindowManager(Game game) :
            base(game)
        {
            MaterialManager.Initialize(game.GraphicsDevice);
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            DrawOrder = 100;

            spriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO 20211104 needs to move to a TextureManager
            using (FileStream stream = File.OpenRead(".\\Content\\Window.png"))
            {
                windowTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }

            WindowShader = MaterialManager.Instance.EffectShaders[ShaderEffect.PopupWindow] as PopupWindowShader;
            WindowShader.GlassColor = Color.Black;
            WindowShader.Opacity = 0.6f;
            WindowShader.WindowTexture = windowTexture;

            TextFontDefault = FontManager.Instance("Segoe UI", System.Drawing.FontStyle.Regular)[12];

            UpdateSize();
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            UpdateSize();
        }

        private void UpdateSize()
        {
            xnaView = Matrix.CreateTranslation(-Game.GraphicsDevice.Viewport.Width / 2, -Game.GraphicsDevice.Viewport.Height / 2, 0) *
                Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
                Matrix.CreateScale(1.0f, -1.0f, 1.0f);
            // Project into a flat view of the same size as the viewport.
            xnaProjection = Matrix.CreateOrthographic(Game.GraphicsDevice.Viewport.Width, Game.GraphicsDevice.Viewport.Height, 0, 1);
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
            if (null == game)
                throw new ArgumentNullException(nameof(game));

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
                else if (windows.LastOrDefault(w => w.Borders.Contains(moveCommandArgs.Position)) != null)
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
                mouseActiveWindow = windows.LastOrDefault(w => w.Borders.Contains(pointerCommandArgs.Position));
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
            foreach (WindowBase window in windows)
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
                WindowShader.SetState(null);
                window.RenderWindow();
                WindowShader.ResetState();
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
