using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using FreeTrainSimulator.Common;

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
        private class WindowSortComparer : IComparer<FormBase>
        {
            private readonly WindowManager host;

            public WindowSortComparer(WindowManager host)
            {
                this.host = host;
            }

            public int Compare(FormBase x, FormBase y)
            {
                return (x.TopMost || x.Modal) == (y.TopMost || y.Modal) ?
                    x == host.activeWindow ?
                    1 : y == host.activeWindow ? -11 : x.ZOrder.CompareTo(y.ZOrder) :
                    x == host.activeWindow ? 1 : (x.TopMost || x.Modal).CompareTo((y.TopMost || y.Modal));
            }
        }

        private List<FormBase> windows = new List<FormBase>();
        private readonly Stack<WindowBase> modalWindows = new Stack<WindowBase>();
        private readonly WindowSortComparer windowSortComparer;

        private readonly Texture2D windowTexture;
        internal Texture2D ScrollbarTexture { get; }
        internal Dictionary<int, Texture2D> RoundedShadows { get; } = new Dictionary<int, Texture2D>();

        internal BasicShapes BasicShapes { get; }

#pragma warning disable CA2213 // Disposable fields should be disposed
        private WindowBase activeWindow;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly SpriteBatch spriteBatch;
        private long nextWindowUpdate;

        private float opacityDefault = 0.6f;
        private Matrix xnaView;
        private Matrix xnaProjection;

        internal ref readonly Matrix XNAView => ref xnaView;
        internal ref readonly Matrix XNAProjection => ref xnaProjection;
        internal readonly PopupWindowShader WindowShader;
        internal readonly GraphShader GraphShader;
        private Viewport viewport;
        private Point size;

        public ref readonly Viewport Viewport => ref viewport;
        public ref readonly Point Size => ref size;

        public const string DefaultFontName = "Arial";  //"Segoe UI"; // Arial renders a better visual experience than Segoe UI
        public const string DefaultMonoFontName = "Courier New";
        public const int DefaultFontSize = 13;

        public static readonly int KeyRepeatDelay = 1000 / Math.Clamp(SystemInformation.KeyboardSpeed / 4, 2, 30);

        public float DpiScaling { get; private set; }
        public System.Drawing.Font TextFontDefault { get; }
        public System.Drawing.Font TextFontDefaultBold { get; }
        public System.Drawing.Font TextFontMonoDefault { get; }
        public System.Drawing.Font TextFontMonoDefaultBold { get; }
        public System.Drawing.Font TextFontSmall { get; }

        public string FontName { get; } = DefaultFontName;
        public int FontSize { get; } = DefaultFontSize;
        public int SmallFontSize { get; } = (int)(DefaultFontSize / 1.25);
        public string MonoFontName { get; } = DefaultMonoFontName;

        //publish some events to allow interaction between Graphcis WindowManager and outside Window world
        public event EventHandler<ModalWindowEventArgs> OnModalWindow;

        public bool SuppressDrawing { get; private set; }

        internal Texture2D WhiteTexture { get; }

        internal Texture2D BackgroundTexture { get; }

        internal Color BackgroundColor = Color.Black * 0.5f;

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
            }
            catch (InvalidOperationException) //potential cross thread operation if we are in a different thread
            {
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired) // no way to know which window we are, so just trying the first one
                    Application.OpenForms[0].Invoke(() =>
                {
                    DpiScaling = DisplayScalingFactor(Screen.FromControl((Form)Control.FromHandle(game.Window.Handle)));
                });
            }
            viewport = Game.GraphicsDevice.Viewport;
            size = viewport.Bounds.Size;
            windowSortComparer = new WindowSortComparer(this);

            spriteBatch = new SpriteBatch(GraphicsDevice);
            WhiteTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            WhiteTexture.SetData(new[] { Color.White });

            BasicShapes = BasicShapes.Instance(game);
            BackgroundTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            BackgroundTexture.SetData(new[] { BackgroundColor });

            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            game.Window.TextInput += Window_TextInput;
            DrawOrder = 100;

            windowTexture = TextureManager.GetTextureStatic(Path.Combine(RuntimeInfo.ContentFolder, "NoTitleBarWindow.png"), game);
            ScrollbarTexture = TextureManager.GetTextureStatic(Path.Combine(RuntimeInfo.ContentFolder, "WindowScrollbar.png"), game);

            WindowShader = MaterialManager.Instance(game).EffectShaders[ShaderEffect.PopupWindow] as PopupWindowShader;
            WindowShader.GlassColor = Color.Black;
            WindowShader.Opacity = opacityDefault;
            WindowShader.WindowTexture = windowTexture;

            GraphShader = MaterialManager.Instance(game).EffectShaders[ShaderEffect.Diagram] as GraphShader;

            FontManager.ScalingFactor = DpiScaling;
            TextFontDefault = FontManager.Scaled(FontName, System.Drawing.FontStyle.Regular)[FontSize];
            TextFontDefaultBold = FontManager.Scaled(FontName, System.Drawing.FontStyle.Bold)[FontSize];
            TextFontMonoDefault = FontManager.Scaled(MonoFontName, System.Drawing.FontStyle.Regular)[FontSize];
            TextFontMonoDefaultBold = FontManager.Scaled(MonoFontName, System.Drawing.FontStyle.Bold)[FontSize];
            TextFontSmall = FontManager.Scaled(FontName, System.Drawing.FontStyle.Regular)[SmallFontSize];
            UpdateSize();
        }

        public static WindowManager<TWindowType> Initialize<T, TWindowType>(Game game, UserCommandController<T> userCommandController)
            where T : Enum where TWindowType : Enum
        {
            ArgumentNullException.ThrowIfNull(game);

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
            ArgumentNullException.ThrowIfNull(userCommandController);

            userCommandController.AddEvent(CommonUserCommand.PointerMoved, MouseMovedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerPressed, MousePressedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDown, MouseDownEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerReleased, MouseReleasedEvent);
            userCommandController.AddEvent(CommonUserCommand.PointerDragged, MouseDraggingEvent);
            userCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, WindowScrollEvent);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            viewport = Game.GraphicsDevice.Viewport;
            size = viewport.Bounds.Size;
            UpdateSize();
        }

        private void Window_TextInput(object sender, TextInputEventArgs e)
        {
            activeWindow?.ActiveControl?.HandleTextInput(e);
        }

        private void UpdateSize()
        {
            xnaView = Matrix.CreateTranslation(-viewport.Width / 2, -viewport.Height / 2, 0) *
                Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
                Matrix.CreateScale(1.0f, -1.0f, 1.0f);
            // Project into a flat view of the same size as the viewport.
            xnaProjection = Matrix.CreateOrthographic(viewport.Width, viewport.Height, 0, 1);
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] is WindowBase framedWindow)
                    framedWindow.UpdateLocation();
            }
        }

        internal bool OpenWindow(FormBase window)
        {
            if (modalWindows.TryPeek(out _) && (!window.Modal || !MultiLayerModalWindows))
            {
                return false;
            }

            if (!WindowOpen(window))
            {
                SuppressDrawing = false;
                List<FormBase> updatedWindowList = new List<FormBase>(windows)
                    {
                        window
                    };
                updatedWindowList.Sort(windowSortComparer);
                Interlocked.Exchange(ref windows, updatedWindowList);
                if (window is WindowBase framedWindow)
                {
                    framedWindow.UpdateLocation();
                    if (framedWindow != activeWindow)
                    {
                        activeWindow?.FocusLost();
                        activeWindow = framedWindow;
                        framedWindow.FocusSet();
                    }
                    if (window.Modal)
                    {
                        UserCommandController.SuppressDownLevelEventHandling = true;
                        modalWindows.Push(framedWindow);
                        OnModalWindow?.Invoke(this, new ModalWindowEventArgs(true));
                    }
                }
                return true;
            }
            return false;
        }

        internal bool CloseWindow(FormBase window)
        {
            if (window == activeWindow)
            {
                activeWindow = null;
                if (window is WindowBase framedWindow)
                    framedWindow.FocusLost();
            }
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
            List<FormBase> updatedWindowList = new List<FormBase>(windows);
            if (updatedWindowList.Remove(window))
            {
                Interlocked.Exchange(ref windows, updatedWindowList);
                return true;
            }
            return false;
        }

        internal bool WindowOpen(FormBase window)
        {
            return windows.IndexOf(window) > -1;
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
                WindowBase topMostTargetedWindow = windows.LastOrDefault(w => w is WindowBase && w.Interactive && w.Borders.Contains(pointerCommandArgs.Position)) as WindowBase;
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
                    lock (modalWindows)
                    {
                        windows.Sort(windowSortComparer);
                    }
                }
#pragma warning restore CA2000 // Dispose objects before losing scope
                _ = (activeWindow?.HandleMouseClicked(pointerCommandArgs.Position, keyModifiers));
                if (topMostTargetedWindow != null)
                    userCommandArgs.Handled = true;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                WindowShader.SetState();
                FormBase window = windows[i];
                WindowShader.Opacity = window == activeWindow ? opacityDefault * 1.25f : opacityDefault;
                if (window is WindowBase framedWindow)
                {
                    framedWindow.WindowDraw();
                }
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

        protected override void Dispose(bool disposing)
        {
            windowTexture?.Dispose();
            WhiteTexture?.Dispose();
            BackgroundTexture?.Dispose();
            ScrollbarTexture.Dispose();
            foreach (Texture2D texture in RoundedShadows.Values)
                texture?.Dispose();
            spriteBatch?.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed class WindowManager<TWindowType> : WindowManager where TWindowType : Enum
    {
        private readonly EnumArray<FormBase, TWindowType> windows = new EnumArray<FormBase, TWindowType>();
        private readonly EnumArray<Lazy<FormBase>, TWindowType> lazyWindows = new EnumArray<Lazy<FormBase>, TWindowType>();

        internal WindowManager(Game game) :
            base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            foreach (FormBase window in windows)
            {
                window?.Initialize();
            }
        }

        public FormBase this[TWindowType window]
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

        public void SetLazyWindows(TWindowType window, Lazy<FormBase> lazyWindow)
        {
            lazyWindows[window] = lazyWindow;
        }

        protected override void Dispose(bool disposing)
        {
            foreach (TWindowType windowType in EnumExtension.GetValues<TWindowType>())
            {
                if (WindowInitialized(windowType))
                {
                    windows[windowType]?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
