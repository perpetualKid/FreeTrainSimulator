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
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Shaders;
using Orts.Graphics.Xna;

using static Orts.Common.Native.NativeMethods;

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
        private List<WindowBase> windows = new List<WindowBase>();
        private readonly Stack<WindowBase> modalWindows = new Stack<WindowBase>();

        private readonly Texture2D windowTexture;
        internal Texture2D ScrollbarTexture { get; }

        private WindowBase activeWindow;
        private readonly SpriteBatch spriteBatch;
        private long nextWindowUpdate;

        private float opacityDefault = 0.6f;
        private Matrix xnaView;
        private Matrix xnaProjection;

        internal ref readonly Matrix XNAView => ref xnaView;
        internal ref readonly Matrix XNAProjection => ref xnaProjection;
        internal readonly PopupWindowShader WindowShader;
        private Rectangle clientBounds;
        public ref readonly Rectangle ClientBounds => ref clientBounds;

        public float DpiScaling { get; private set; }
        public System.Drawing.Font TextFontDefault { get; }
        public System.Drawing.Font TextFontDefaultBold { get; }
        public System.Drawing.Font TextFontMonoDefault { get; }
        public System.Drawing.Font TextFontMonoDefaultBold { get; }
        public System.Drawing.Font TextFontSmall { get; }

        public string DefaultFontName { get; } = "Arial";//"Segoe UI"; // Arial renders a better visual experience than Segoe UI
        public int DefaultFontSize { get; } = 13;
        public int SmallFontSize { get; } = 10;
        public string DefaultMonoFontName { get; } = "Courier New";

        //publish some events to allow interaction between Graphcis WindowManager and outside Window world
        public event EventHandler<ModalWindowEventArgs> OnModalWindow;

        public bool SuppressDrawing { get; private set; }

        internal Texture2D WhiteTexture { get; }

        public UserCommandController UserCommandController { get; private set; }

        public float WindowOpacity
        {
            get => WindowShader.Opacity;
            set => WindowShader.Opacity = opacityDefault = value;
        }

        public bool MultiLayerModalWindows { get; set; }

        private protected WindowManager(Game game) :
            base(game)
        {
            try
            {
                DpiScaling = DisplayScalingFactor(Screen.FromControl((Form)Control.FromHandle(game.Window.Handle)));
                clientBounds = Game.Window.ClientBounds;
            }
            catch (InvalidOperationException) //potential cross thread operation if we are in a different thread
            {
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired) // no way to know which window we are, so just trying the first one
                    Application.OpenForms[0].Invoke(() =>
                {
                    DpiScaling = DisplayScalingFactor(Screen.FromControl((Form)Control.FromHandle(game.Window.Handle)));
                    clientBounds = Game.Window.ClientBounds;
                });
            }

            spriteBatch = new SpriteBatch(GraphicsDevice);
            WhiteTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            WhiteTexture.SetData(new[] { Color.White });

            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            DrawOrder = 100;

            windowTexture = TextureManager.GetTextureStatic(Path.Combine(RuntimeInfo.ContentFolder, "NoTitleBarWindow.png"), game);
            ScrollbarTexture = TextureManager.GetTextureStatic(Path.Combine(RuntimeInfo.ContentFolder, "WindowScrollbar.png"), game);

            WindowShader = MaterialManager.Instance(game).EffectShaders[ShaderEffect.PopupWindow] as PopupWindowShader;
            WindowShader.GlassColor = Color.Black;
            WindowShader.Opacity = opacityDefault;
            WindowShader.WindowTexture = windowTexture;

            FontManager.ScalingFactor = DpiScaling;
            TextFontDefault = FontManager.Scaled(DefaultFontName, System.Drawing.FontStyle.Regular)[DefaultFontSize];
            TextFontDefaultBold = FontManager.Scaled(DefaultFontName, System.Drawing.FontStyle.Bold)[DefaultFontSize];
            TextFontMonoDefault = FontManager.Scaled(DefaultMonoFontName, System.Drawing.FontStyle.Regular)[DefaultFontSize];
            TextFontMonoDefaultBold = FontManager.Scaled(DefaultMonoFontName, System.Drawing.FontStyle.Bold)[DefaultFontSize];
            TextFontSmall = FontManager.Scaled(DefaultFontName, System.Drawing.FontStyle.Regular)[SmallFontSize];
            UpdateSize();
        }

        public static WindowManager<TWindowType> Initialize<T, TWindowType>(Game game, UserCommandController<T> userCommandController)
            where T : Enum where TWindowType : Enum
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            WindowManager<TWindowType> result;
            if ((result = game.Services.GetService<WindowManager<TWindowType>>()) == null)
            {
                game.Services.AddService(result = new WindowManager<TWindowType>(game));
                result.UserCommandController = userCommandController;
                result.AddUserCommandEvents(userCommandController);
            }
            return result;
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
            for (int i = 0; i < windows.Count; i++)
            {
                windows[i].UpdateLocation();
            }
        }

        internal bool OpenWindow(WindowBase window)
        {
            if (modalWindows.TryPeek(out activeWindow) && (!window.Modal || !MultiLayerModalWindows))
            {
                return false;
            }

            if (!WindowOpen(window))
            {
                SuppressDrawing = false;
                window.UpdateLocation();
                windows = windows.Append(window).OrderBy(w => w.ZOrder).ToList();
                if (window != activeWindow)
                {
                    activeWindow?.FocusLost();
                    activeWindow = window;
                    window?.FocusSet();
                }
                activeWindow = window;
                if (window.Modal)
                {
                    UserCommandController.SuppressDownLevelEventHandling = true;
                    modalWindows.Push(window);
                    OnModalWindow?.Invoke(this, new ModalWindowEventArgs(true));
                }
                return true;
            }
            return false;
        }

        internal bool CloseWindow(WindowBase window)
        {
            window?.FocusLost();
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow == window)
            {
                SuppressDrawing = false;
                modalWindows.Pop();
                if (modalWindows.Count == 0)
                {
                    if (window == activeWindow)
                        activeWindow = null;
                    UserCommandController.SuppressDownLevelEventHandling = false;
                    OnModalWindow?.Invoke(this, new ModalWindowEventArgs(false));
                }
                else
                {
                    modalWindows.TryPeek(out activeWindow);
                }
            }
#pragma warning restore CA2000 // Dispose objects before losing scope
            activeWindow?.FocusSet();
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
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow != activeWindow)
            {
                SuppressDrawing = false;
                userCommandArgs.Handled = true;
            }
#pragma warning restore CA2000 // Dispose objects before losing scope
            //            UserCommandController.SuppressDownLevelEventHandling = (userCommandArgs is PointerCommandArgs pointerCommandArgs && windows.Where(w => w.Borders.Contains(pointerCommandArgs.Position)).Any());
        }

        private void WindowScrollEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow != activeWindow)
            {
                SuppressDrawing = false;
                userCommandArgs.Handled = true;
            }
            else if (activeWindow != null && userCommandArgs is ScrollCommandArgs scrollCommandArgs)
            {
                userCommandArgs.Handled = activeWindow.HandleMouseScroll(scrollCommandArgs.Position, scrollCommandArgs.Delta, keyModifiers);
            }
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        private void MouseDraggingEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerMoveCommandArgs moveCommandArgs)
            {
                SuppressDrawing = false;
                WindowBase topMostTargetedWindow = windows.LastOrDefault(w => w.Interactive && w.Borders.Contains(moveCommandArgs.Position));
#pragma warning disable CA2000 // Dispose objects before losing scope
                if ((activeWindow != null && modalWindows.Count == 0) || modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow == activeWindow)
                {
                    activeWindow.HandleMouseDrag(moveCommandArgs.Position, moveCommandArgs.Delta, keyModifiers);
                    userCommandArgs.Handled = true;
                }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }

        private void MouseReleasedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
                if (activeWindow != null)
                {
                    userCommandArgs.Handled = true;
                    _ = activeWindow.HandleMouseReleased(pointerCommandArgs.Position, keyModifiers);
                }
                else if (modalWindows.TryPeek(out WindowBase currentModalWindow))
                {
                    userCommandArgs.Handled = true;
                    activeWindow = currentModalWindow;
                    currentModalWindow.FocusSet();
                }
            }
        }

        private void MouseDownEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
#pragma warning disable CA2000 // Dispose objects before losing scope
                if (modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow != activeWindow)
                {
                    userCommandArgs.Handled = true;
                    _ = currentModalWindow.HandleMouseDown(pointerCommandArgs.Position, keyModifiers);
                }
                else if (activeWindow != null)
                {
                    userCommandArgs.Handled = true;
                    if (activeWindow != windows.Last())
                    {
                        List<WindowBase> updatedWindowList = windows;
                        if (updatedWindowList.Remove(activeWindow))
                        {
                            updatedWindowList.Add(activeWindow);
                            windows = updatedWindowList;
                        }
                    }
                    _ = activeWindow.HandleMouseDown(pointerCommandArgs.Position, keyModifiers);
                }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }

        private void MousePressedEvent(UserCommandArgs userCommandArgs, KeyModifiers keyModifiers)
        {
            if (userCommandArgs is PointerCommandArgs pointerCommandArgs)
            {
                SuppressDrawing = false;
                WindowBase topMostTargetedWindow = windows.LastOrDefault(w => w.Interactive && w.Borders.Contains(pointerCommandArgs.Position));
#pragma warning disable CA2000 // Dispose objects before losing scope
                if (topMostTargetedWindow == null || (modalWindows.TryPeek(out WindowBase currentModalWindow) && currentModalWindow != topMostTargetedWindow))
                {
                    activeWindow?.FocusLost();
                    activeWindow = null;
                }
                else if (topMostTargetedWindow != activeWindow)
                {
                    activeWindow?.FocusLost();
                    activeWindow = topMostTargetedWindow;
                    topMostTargetedWindow.FocusSet();
                }
#pragma warning restore CA2000 // Dispose objects before losing scope
                _ = (activeWindow?.HandleMouseClicked(pointerCommandArgs.Position, keyModifiers));
            }
        }

        public override void Initialize()
        {
            BasicShapes.Initialize(spriteBatch);
            BasicShapes.LoadContent(Game.GraphicsDevice);
            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                WindowBase window = windows[i];
                WindowShader.SetState();
                WindowShader.Opacity = window == activeWindow ? opacityDefault * 1.25f : opacityDefault;
                window.WindowDraw();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.Default, null, null);
                window.Draw(spriteBatch);
                spriteBatch.End();
                WindowShader.ResetState();
            }
            base.Draw(gameTime);
            SuppressDrawing = true;
        }

        public override void Update(GameTime gameTime)
        {
            //TODO 20220929 could distribute the shouldUpdate for each Window to be in a different Update Cycle
            bool shouldUpdate = Environment.TickCount64 > nextWindowUpdate;
            if (shouldUpdate)
            {
                nextWindowUpdate = Environment.TickCount64 + 100;
            }

            for (int i = 0; i < windows.Count; i++)
            {
                windows[i].Update(gameTime, shouldUpdate);
            }
            base.Update(gameTime);
        }

        public static float DisplayScalingFactor(Screen screen)
        {
            if (screen == null)
                return 1;
            try
            {
                using (Form testForm = new Form
                {
                    WindowState = FormWindowState.Normal,
                    StartPosition = FormStartPosition.Manual,
                    Left = screen.Bounds.Left,
                    Top = screen.Bounds.Top
                })
                {
                    return (float)Math.Round(GetDpiForWindow(testForm.Handle) / 96.0, 2);
                }
            }
            catch (EntryPointNotFoundException)//running on Windows 7 or other unsupported OS
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr desktop = g.GetHdc();
                        int dpi = GetDeviceCaps(desktop, (int)DeviceCap.LOGPIXELSX);
                        return (float)Math.Round(dpi / 96.0, 2);
                    }
                    finally
                    {
                        g.ReleaseHdc();
                    }
                }
            }
        }

    }

    public sealed class WindowManager<TWindowType> : WindowManager where TWindowType : Enum
    {
        private readonly EnumArray<WindowBase, TWindowType> windows = new EnumArray<WindowBase, TWindowType>();
        private readonly EnumArray<Lazy<WindowBase>, TWindowType> lazyWindows = new EnumArray<Lazy<WindowBase>, TWindowType>();

        internal WindowManager(Game game) :
            base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            foreach (WindowBase window in windows)
            {
                window?.Initialize();
            }
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

        public bool WindowInitialized(TWindowType window) => lazyWindows[window]?.IsValueCreated ?? false;

        public bool WindowOpened(TWindowType window) => (lazyWindows[window]?.IsValueCreated ?? false) && WindowOpen(windows[window]);

        public void SetLazyWindows(TWindowType window, Lazy<WindowBase> lazyWindow)
        {
            lazyWindows[window] = lazyWindow;
        }
    }
}
