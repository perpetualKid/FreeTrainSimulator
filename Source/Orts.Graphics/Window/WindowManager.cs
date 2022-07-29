using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Graphics.Shaders;
using Orts.Graphics.Window.Controls.Layout;

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
        private List<WindowBase> windows = new List<WindowBase>();
        private WindowBase modalWindow; // if modalWindow is set, no other Window can be activated or interacted with

        private readonly Texture2D windowTexture;
        internal Texture2D ScrollbarTexture { get; }

        private WindowBase mouseActiveWindow;
        private readonly SpriteBatch spriteBatch;

        private const float opacityDefault = 0.6f;
        private Matrix xnaView;
        private Matrix xnaProjection;
        internal ref readonly Matrix XNAView => ref xnaView;
        internal ref readonly Matrix XNAProjection => ref xnaProjection;
        internal readonly PopupWindowShader WindowShader;
        private Rectangle clientBounds;
        internal ref readonly Rectangle ClientBounds => ref clientBounds;
        public float DpiScaling { get; private set; }
        public System.Drawing.Font TextFontDefault { get; }
        public System.Drawing.Font TextFontDefaultBold { get; }

        public string DefaultFont { get; } = "Segoe UI";
        public int DefaultFontSize { get; } = 12;

        //publish some events to allow interaction between XNA WindowManager and outside Window world
        public event EventHandler<ModalWindowEventArgs> OnModalWindow;

        public bool SuppressDrawing { get; private set; }

        internal Texture2D WhiteTexture { get; }

        public UserCommandController UserCommandController { get; private set; }

        private protected WindowManager(Game game) :
            base(game)
        {
            try
            {
                DpiScaling = SystemInfo.DisplayScalingFactor(Screen.FromControl((Form)Control.FromHandle(game.Window.Handle)));
                clientBounds = Game.Window.ClientBounds;
            }
            catch (InvalidOperationException) //potential cross thread operation if we are in a different thread
            {
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired) // no way to know which window we are, so just trying the first one
                    Application.OpenForms[0].Invoke(() =>
                {
                    DpiScaling = SystemInfo.DisplayScalingFactor(Screen.FromControl((Form)Control.FromHandle(game.Window.Handle)));
                    clientBounds = Game.Window.ClientBounds;
                });
            }

            WhiteTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            WhiteTexture.SetData(new[] { Color.White });

            MaterialManager.Initialize(game.GraphicsDevice);
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            DrawOrder = 100;

            spriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO 20211104 needs to move to a TextureManager
            using (FileStream stream = File.OpenRead(Path.Combine(RuntimeInfo.ContentFolder, "NoTitleBarWindow.png")))
            {
                windowTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }
            using (FileStream stream = File.OpenRead(Path.Combine(RuntimeInfo.ContentFolder, "WindowScrollbar.png")))
            {
                ScrollbarTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }

            WindowShader = MaterialManager.Instance.EffectShaders[ShaderEffect.PopupWindow] as PopupWindowShader;
            WindowShader.GlassColor = Color.Black;
            WindowShader.Opacity = opacityDefault;
            WindowShader.WindowTexture = windowTexture;

            TextFontDefault = FontManager.Scaled(DefaultFont, System.Drawing.FontStyle.Regular)[DefaultFontSize];
            TextFontDefaultBold = FontManager.Scaled(DefaultFont, System.Drawing.FontStyle.Bold)[DefaultFontSize];

            UpdateSize();
        }

        public static WindowManager Initialize<T>(Game game, UserCommandController<T> userCommandController) where T : Enum
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            if (null == instance)
            {
                instance = new WindowManager(game)
                {
                    UserCommandController = userCommandController
                };
                instance.AddUserCommandEvents(userCommandController);
            }
            return instance;
        }

        public static WindowManager<TWindowType> Initialize<T, TWindowType>(Game game, UserCommandController<T> userCommandController)
            where T : Enum where TWindowType : Enum
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            if (null == WindowManager<TWindowType>.Instance)
            {
                WindowManager<TWindowType>.Initialize(game);
                WindowManager<TWindowType>.Instance.UserCommandController = userCommandController;
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

            userCommandController.AddEvent(CommonUserCommand.PointerMoved, MouseMovedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, MousePressedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDown, MouseDownEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDraggingEvent);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, WindowScrollEvent);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            clientBounds = Game.Window.ClientBounds;
            UpdateSize();
        }

        private void UpdateSize()
        {
            xnaView = Matrix.CreateTranslation(-Game.GraphicsDevice.Viewport.Width / 2, -Game.GraphicsDevice.Viewport.Height / 2, 0) *
                Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
                Matrix.CreateScale(1.0f, -1.0f, 1.0f);
            // Project into a flat view of the same size as the viewport.
            xnaProjection = Matrix.CreateOrthographic(Game.GraphicsDevice.Viewport.Width, Game.GraphicsDevice.Viewport.Height, 0, 1);
            foreach (WindowBase window in windows)
                window.UpdateLocation();
        }

        internal bool OpenWindow(WindowBase window)
        {
            if (modalWindow != null)
            {
                mouseActiveWindow = modalWindow;
                return false;
            }

            if (!WindowOpen(window))
            {
                SuppressDrawing = false;
                window.UpdateLocation();
                windows = windows.Append(window).OrderBy(w => w.ZOrder).ToList();
                if (window != mouseActiveWindow)
                {
                    mouseActiveWindow?.FocusLost();
                    mouseActiveWindow = window;
                    window?.FocusSet();
                }
                mouseActiveWindow = window;
                if (window.Modal)
                {
                    UserCommandController.SuppressDownLevelEventHandling = true;
                    modalWindow = window;
                    OnModalWindow?.Invoke(this, new ModalWindowEventArgs(true));
                }
                return true;
            }
            return false;
        }

        internal bool CloseWindow(WindowBase window)
        {
            if (window == modalWindow)
            {
                UserCommandController.SuppressDownLevelEventHandling = false;
                SuppressDrawing = false;
                modalWindow = null;
                OnModalWindow?.Invoke(this, new ModalWindowEventArgs(false));
            }
            if (mouseActiveWindow == window)
                mouseActiveWindow = null;
            List<WindowBase> updatedWindowList = windows;
            if (updatedWindowList.Remove(window))
            {
                windows = updatedWindowList;
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

        private void MouseMovedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (modalWindow != null && modalWindow != mouseActiveWindow)
            {
                SuppressDrawing = false;
                userCommandArgs.Handled = true;
            }
            //            UserCommandController.SuppressDownLevelEventHandling = (userCommandArgs is PointerCommandArgs pointerCommandArgs && windows.Where(w => w.Borders.Contains(pointerCommandArgs.Position)).Any());
        }

        private void WindowScrollEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (modalWindow != null && modalWindow != mouseActiveWindow)
            {
                SuppressDrawing = false;
                userCommandArgs.Handled = true;
            }
            else if (mouseActiveWindow != null && userCommandArgs is ScrollCommandArgs scrollCommandArgs)
            {
                userCommandArgs.Handled = mouseActiveWindow.HandleMouseScroll(scrollCommandArgs.Position, scrollCommandArgs.Delta, keyModifiers);
            }
        }

        private void MouseDraggingEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerMoveCommandArgs moveCommandArgs)
            {
                SuppressDrawing = false;
                if (modalWindow != null && modalWindow != mouseActiveWindow)
                {
                    userCommandArgs.Handled = true;
                }
                else if (mouseActiveWindow != null)
                {
                    mouseActiveWindow.HandleMouseDrag(moveCommandArgs.Position, moveCommandArgs.Delta, keyModifiers);
                    userCommandArgs.Handled = true;
                }
            }
        }

        private void MouseReleasedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
                if (modalWindow != null && mouseActiveWindow != modalWindow)
                {
                    userCommandArgs.Handled = true;
                }
                else if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    _ = mouseActiveWindow.HandleMouseReleased(pointerCommandArgs.Position, keyModifiers);
                }
            }
        }

        private void MouseDownEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
                if (modalWindow != null && mouseActiveWindow != modalWindow)
                {
                    userCommandArgs.Handled = true;
                    _ = modalWindow.HandleMouseDown(pointerCommandArgs.Position, keyModifiers);
                }
                else if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    if (mouseActiveWindow != windows.Last())
                    {
                        List<WindowBase> updatedWindowList = windows;
                        if (updatedWindowList.Remove(mouseActiveWindow))
                        {
                            updatedWindowList.Add(mouseActiveWindow);
                            windows = updatedWindowList;
                        }
                    }
                    _ = mouseActiveWindow.HandleMouseDown(pointerCommandArgs.Position, keyModifiers);
                }
            }
        }

        private void MousePressedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
                WindowBase activeWindow = windows.LastOrDefault(w => w.Interactive && w.Borders.Contains(pointerCommandArgs.Position));
                if (activeWindow != mouseActiveWindow)
                {
                    mouseActiveWindow?.FocusLost();
                    mouseActiveWindow = activeWindow;
                    activeWindow?.FocusSet();
                }
                if (mouseActiveWindow != null)
                {
                    userCommandArgs.Handled = true;
                    if (modalWindow == null && mouseActiveWindow != windows.Last())
                    {
                        List<WindowBase> updatedWindowList = windows;
                        if (updatedWindowList.Remove(mouseActiveWindow))
                        {
                            updatedWindowList.Add(mouseActiveWindow);
                            windows = updatedWindowList;
                        }
                    }
                    else if (modalWindow != null && mouseActiveWindow != modalWindow)
                    {
                        mouseActiveWindow.FocusLost();
                        mouseActiveWindow = null;
                    }
                    _ = (mouseActiveWindow?.HandleMouseClicked(pointerCommandArgs.Position, keyModifiers));
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            foreach (WindowBase window in windows)
            {
                WindowShader.SetState(null);
                WindowShader.Opacity = window == mouseActiveWindow ? opacityDefault * 1.2f : opacityDefault;
                window.WindowDraw();
                WindowShader.ResetState();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null);
                window.Draw(spriteBatch);
                spriteBatch.End();
            }
            base.Draw(gameTime);
            SuppressDrawing = true;
        }

        public override void Update(GameTime gameTime)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                windows[i].Update(gameTime);
            }
            base.Update(gameTime);
        }
    }

    public sealed class WindowManager<TWindowType> : WindowManager where TWindowType : Enum
    {
        private readonly EnumArray<WindowBase, TWindowType> windows = new EnumArray<WindowBase, TWindowType>();
        private readonly EnumArray<Lazy<WindowBase>, TWindowType> lazyWindows = new EnumArray<Lazy<WindowBase>, TWindowType>();

        [ThreadStatic]
        internal static WindowManager<TWindowType> Instance;

        internal WindowManager(Game game) :
            base(game)
        {
        }

        internal static void Initialize(Game game)
        {
            if (Instance != null)
                throw new InvalidOperationException($"WindowManager {typeof(WindowManager<TWindowType>)} already initialized.");

            Instance = new WindowManager<TWindowType>(game);
        }

        public override void Initialize()
        {
            foreach (WindowBase window in windows)
            {
                window?.Initialize();
            }
            base.Initialize();
        }

        public WindowBase this[TWindowType window]
        {
            get
            {
                if (windows[window] == null)
                {
                    lazyWindows[window].Value.Initialize();
                    windows[window] = lazyWindows[window].Value;
                }
                return windows[window];
            }
            set => windows[window] = value;
        }

        public bool WindowInitialized(TWindowType window) => lazyWindows[window]?.IsValueCreated ?? true;

        public bool WindowOpened(TWindowType window) => (lazyWindows[window]?.IsValueCreated ?? false) && WindowOpen(windows[window]);

        public void SetLazyWindows(TWindowType window, Lazy<WindowBase> lazyWindow)
        {
            lazyWindows[window] = lazyWindow;
        }
    }
}
