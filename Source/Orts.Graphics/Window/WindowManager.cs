using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Input;
using Orts.Graphics.Shaders;

namespace Orts.Graphics.Window
{
    public class ModalWindowEventArgs : EventArgs
    { 
        public bool ModalWindowOpen { get; private set; }

        public ModalWindowEventArgs(bool modalWindowOpen)
        {
            ModalWindowOpen = modalWindowOpen;
        }
    }

    public class WindowManager : DrawableGameComponent
    {
        [ThreadStatic]
        private static WindowManager instance;
        private readonly List<WindowBase> windows = new List<WindowBase>();
        private WindowBase modalWindow; // if modalWindow is set, no other Window can be activated or interacted with

        internal Texture2D windowTexture;
        private WindowBase mouseActiveWindow;
        private readonly SpriteBatch spriteBatch;

        private Matrix xnaView;
        private Matrix xnaProjection;
        internal ref readonly Matrix XNAView => ref xnaView;
        internal ref readonly Matrix XNAProjection => ref xnaProjection;
        internal readonly PopupWindowShader WindowShader;

        public System.Drawing.Font TextFontDefault { get; }

        //publish some events to allow interaction between XNA WindowManager and outside Window world
        public event EventHandler<ModalWindowEventArgs> OnModalWindow;

        public static Texture2D WhiteTexture;

        private protected WindowManager(Game game) :
            base(game)
        {
            WhiteTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            WhiteTexture.SetData(new[] { Color.White });

            MaterialManager.Initialize(game.GraphicsDevice);
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            DrawOrder = 100;

            spriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO 20211104 needs to move to a TextureManager
            using (FileStream stream = File.OpenRead(".\\Content\\TitleBarWindow.png"))
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

        public static WindowManager Initialize<T>(Game game, UserCommandController<T> userCommandController) where T : Enum
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            if (null == instance)
            {
                instance = new WindowManager(game);
                instance.AddUserCommandEvents(userCommandController);
            }
            return instance;
        }

        public static WindowManager<TWindowType> Initialize<TWindowType, T>(Game game, UserCommandController<T> userCommandController, EnumArray<Type, TWindowType> windows) 
            where T : Enum where TWindowType : Enum
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            if (null == WindowManager<TWindowType>.Instance)
            {
                WindowManager<TWindowType>.Initialize(game, windows);
                WindowManager<TWindowType>.Instance.AddUserCommandEvents(userCommandController);
            }
            return WindowManager<TWindowType>.Instance;
        }

        public static WindowManager GetInstance<T>() where T : Enum
        {
            return instance;
        }

        public static WindowManager<TWindowType> GetInstance<TWindowType, T>() where T : Enum where TWindowType : Enum
        {
            return WindowManager<TWindowType>.Instance;
        }

        protected void AddUserCommandEvents<T>(UserCommandController<T> userCommandController) where T : Enum
        {
            if (null == userCommandController)
                throw new ArgumentNullException(nameof(userCommandController));

            Game.Components.OfType<KeyboardInputGameComponent>().Single().AddInputHandler(EscapeKey);
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, MousePressedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDown, MouseDownEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDraggingEvent);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, WindowScrollEvent);
        }

        private static readonly int escapeKeyCode = KeyboardInputGameComponent.KeyEventCode(Microsoft.Xna.Framework.Input.Keys.Escape, KeyModifiers.None, KeyEventType.KeyPressed);

        private void EscapeKey(int eventCode, GameTime gameTime, KeyEventType eventType, KeyModifiers modifiers)
        {
            if (modalWindow != null && eventCode == escapeKeyCode)
            {
                CloseWindow(modalWindow);
            }
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

        internal bool AddWindow(WindowBase window)
        {
            if (!WindowOpen(window))
            {
                windows.Add(window);
                if (window.Modal)
                {
                    modalWindow = window;
                    OnModalWindow?.Invoke(this, new ModalWindowEventArgs(true));
                }
                return true;
            }
            return false;
        }

        internal bool WindowOpen(WindowBase window)
        {
            return windows.IndexOf(window) > -1;
        }

        internal WindowBase FindWindow(string caption)
        {
            return windows.Where(w => w.Caption == caption).FirstOrDefault();
        }

        internal bool CloseWindow(WindowBase window)
        {
            if (window == modalWindow)
            {
                modalWindow = null;
                OnModalWindow?.Invoke(this, new ModalWindowEventArgs(false));
            }
            return windows.Remove(window);
        }

        private void WindowScrollEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        private void MouseDraggingEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerMoveCommandArgs moveCommandArgs)
            {
                if (modalWindow != null && modalWindow != mouseActiveWindow)
                {
                    userCommandArgs.Handled = true;
                }
                else if (mouseActiveWindow != null)
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
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                if (modalWindow != null && mouseActiveWindow != modalWindow)
                {
                    userCommandArgs.Handled = true;
                }
                else if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    mouseActiveWindow.HandleMouseClick(pointerCommandArgs.Position, keyModifiers);
                }
            }
        }

        private void MouseDownEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
        }

        private void MousePressedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                Point mouseDownPosition = pointerCommandArgs.Position;
                mouseActiveWindow = windows.LastOrDefault(w => w.Borders.Contains(pointerCommandArgs.Position));
                if (modalWindow != null && mouseActiveWindow != modalWindow)
                {
                    userCommandArgs.Handled = true;
                }
                else if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    if (mouseActiveWindow != windows.Last())
                        if (windows.Remove(mouseActiveWindow))
                            windows.Add(mouseActiveWindow);
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
            foreach (WindowBase window in windows)
            {
                WindowShader.SetState(null);
                window.RenderWindow();
                WindowShader.ResetState();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, null, null);
                window.DrawContent(spriteBatch);
                spriteBatch.End();
            }
            base.Draw(gameTime);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }

    public sealed class WindowManager<TWindowType> : WindowManager where TWindowType : Enum
    {
        private readonly EnumArray<WindowBase, TWindowType> windows = new EnumArray<WindowBase, TWindowType>();

        [ThreadStatic]
        internal static WindowManager<TWindowType> Instance;

        internal WindowManager(Game game) :
            base(game)
        {
        }

        internal static void Initialize(Game game, EnumArray<Type, TWindowType> windows)
        {
            if (Instance != null)
                throw new InvalidOperationException($"WindowManager {typeof(WindowManager<TWindowType>)} already initialized.");

            Instance = new WindowManager<TWindowType>(game);
            Instance.Initialize(windows);
        }

        private void Initialize(EnumArray<Type, TWindowType> windows)
        {
            foreach(TWindowType windowType in EnumExtension.GetValues<TWindowType>())
            {
                object o = Activator.CreateInstance(windows[windowType], this);
                this.windows[windowType] = o as WindowBase;
            }
        }

        public WindowBase this[TWindowType window]
        {
            get { return windows[window]; }
        }
    }
}
