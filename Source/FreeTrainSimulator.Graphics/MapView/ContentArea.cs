using System;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    public class ContentArea : DrawableGameComponent, IMapRenderer, IMapViewport, IMapHostControl
    {
        private const int zoomAmplifier = 3;

        internal SpriteBatch SpriteBatch { get; }

        internal BasicShapes BasicShapes { get; }

        private readonly FontManagerInstance fontManager;
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly IMapHostEnvironment hostEnvironment;
#pragma warning restore CA2213 // Disposable fields should be disposed

        private readonly IMapRenderingLifetime renderingLifetime;
        private readonly IMapRenderingResources renderingResources;
        private readonly IMapRenderBackend renderBackend;
        private readonly IMapTextRenderer textRenderer;
        private readonly IMapTextCache textCache;
        private readonly IMapViewController controller;
        private readonly MapViewAdapterCore adapterCore;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

        private readonly MapTextTextureCache ownedTextCache;

        public ContentBase Content { get; }

        public double Scale => adapterCore.Scale;
        public PointD CenterPoint => adapterCore.CenterPoint;
        public bool SuppressDrawing { get; internal set; }
        public Point WindowSize => adapterCore.WindowSize;
        internal PointD TopLeftBound => adapterCore.TopLeftBound;
        internal PointD BottomRightBound => adapterCore.BottomRightBound;
        public PointD WorldPosition => adapterCore.WorldPosition;
        public System.Drawing.Font CurrentFont => adapterCore.CurrentFont;
        public System.Drawing.Font ConstantSizeFont => adapterCore.ConstantSizeFont;

        internal ContentArea(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent) :
            base(game)
        {
            ArgumentNullException.ThrowIfNull(game);

            Content = content ?? throw new ArgumentNullException(nameof(content));

            XnaMapAdapterBundle adapterBundle = new XnaMapAdapterBundle(game, content, mouseInputGameComponent);
            SpriteBatch = adapterBundle.SpriteBatch;
            renderingLifetime = adapterBundle.RenderingLifetime;
            renderingResources = adapterBundle.RenderingResources;
            ownedTextCache = adapterBundle.OwnedTextCache;
            textCache = adapterBundle.TextCache;
            textRenderer = adapterBundle.TextRenderer;
            renderBackend = adapterBundle.RenderBackend;
            fontManager = FontManager.Scaled("Arial", System.Drawing.FontStyle.Regular);
            System.Drawing.Font constantSizeFont = fontManager[25];
            hostEnvironment = adapterBundle.HostEnvironment;
            hostEnvironment.RegisterMouseMove(MouseMove);
            BasicShapes = adapterBundle.BasicShapes;
            controller = adapterBundle.Controller;
            adapterCore = new MapViewAdapterCore(fontManager, controller, hostEnvironment, renderBackend, constantSizeFont);
            hostEnvironment.ClientSizeChanged += Window_ClientSizeChanged;
            Enabled = false;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            adapterCore.SyncViewport(Content.Bounds);
        }

        private void RefreshViewportBounds()
        {
            adapterCore.SyncViewport(Content.Bounds);
        }

        public static void UpdateTrackWidthSettings(bool limitTrackWidth)
        {
            MapViewAdapterCore.UpdateTrackWidthSettings(limitTrackWidth);
        }

        public void UpdateColor(ColorSetting setting, Color color, bool fontOutlining)
        {
            adapterCore.UpdateColor(Content, setting, color, fontOutlining);
        }

        public void MouseMove(Point position, Vector2 delta, GameTime gameTime)
        {
            adapterCore.MouseMove(Enabled, position, Content);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            adapterCore.HandleEnabledChanged(Enabled, this, Content.TextureHelperHost);
            base.OnEnabledChanged(sender, args);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            System.Drawing.Font currentFont = CurrentFont;
            adapterCore.RefreshAfterReset(Content, windowSize, screenDelta, ref currentFont);
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            adapterCore.PresetPosition(centerPoint, scale);
            System.Drawing.Font currentFont = CurrentFont;
            bool suppressDrawing = SuppressDrawing;
            adapterCore.RefreshAfterPreset(ref currentFont, ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            adapterCore.SetTrackingPosition(location);
        }

        public void SetTrackingPosition(in PointD location)
        {
            adapterCore.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            adapterCore.UpdateScaleToFit(topLeft, bottomRight);
            RefreshFontsFromController();
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            adapterCore.UpdateScaleAt(scaleAt, steps);
            RefreshFontsFromController();
        }

        public void UpdateScale(int steps)
        {
            adapterCore.UpdateScale(steps);
            RefreshFontsFromController();
        }

        public void UpdateScaleAbsolute(double scale)
        {
            adapterCore.UpdateScaleAbsolute(scale);
            RefreshFontsFromController();
        }

        public void UpdatePosition(in Vector2 delta)
        {
            adapterCore.UpdatePosition(delta);
        }

        public override void Update(GameTime gameTime)
        {
            bool suppressDrawing = SuppressDrawing;
            adapterCore.UpdateFrameState(ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
            base.Update(gameTime);
        }

        #region public control commands
        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            adapterCore.MouseDragging(userCommandArgs);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            adapterCore.MouseWheelAt(userCommandArgs, modifiers);
            RefreshFontsFromController();
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            adapterCore.MouseWheel(userCommandArgs, modifiers);
            RefreshFontsFromController();
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            adapterCore.MoveByKeyLeft(commandArgs);
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            adapterCore.MoveByKeyRight(commandArgs);
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            adapterCore.MoveByKeyUp(commandArgs);
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            adapterCore.MoveByKeyDown(commandArgs);
        }

        public static int ZoomAmplifier(KeyModifiers modifiers)
        {
            int amplifier = zoomAmplifier;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        public static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            return commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand ? ZoomAmplifier(modifiableKeyCommand.AdditionalModifiers) : zoomAmplifier;
        }

        public void ZoomIn(UserCommandArgs commandArgs)
        {
            adapterCore.ZoomIn(commandArgs);
            RefreshFontsFromController();
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            adapterCore.ZoomOut(commandArgs);
            RefreshFontsFromController();
        }

        public void ResetZoomAndLocation(Point windowSize, int screenDelta)
        {
            ResetSize(windowSize, screenDelta);
        }
        #endregion

        public override void Draw(GameTime gameTime)
        {
            adapterCore.DrawContent(Content);
            bool suppressDrawing = SuppressDrawing;
            adapterCore.NotifyFrameRendered(ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
            base.Draw(gameTime);
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            adapterCore.DrawLine(width, color, point, length, angle);
        }

        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            adapterCore.DrawLine(width, color, point1, point2);
        }

        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            adapterCore.DrawDashedLine(width, color, point1, point2);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize)
        {
            adapterCore.DrawArc(width, color, point, radius, angle, arcSize);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            adapterCore.DrawTexture(texture, point, angle, size, flipHorizontal, flipVertical, highlight);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
        {
            adapterCore.DrawTexture(texture, point, angle, size, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            return adapterCore.WorldToScreenCoordinates(worldLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return adapterCore.ScreenToWorldCoordinates(screenLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return adapterCore.WorldToScreenCoordinates(location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return adapterCore.WorldToScreenSize(worldSize, minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return adapterCore.InsideScreenArea(pointPrimitive);
        }

        bool IMapViewport.InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return InsideScreenArea(pointPrimitive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return adapterCore.InsideScreenArea(vectorPrimitive);
        }

        bool IMapViewport.InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return InsideScreenArea(vectorPrimitive);
        }

        private void UpdateFontSize()
        {
            System.Drawing.Font currentFont = CurrentFont;
            adapterCore.RefreshFonts(ref currentFont);
        }

        private void RefreshFontsFromController()
        {
            System.Drawing.Font currentFont = CurrentFont;
            adapterCore.RefreshAfterScaleChange(ref currentFont);
        }

        private void RefreshDrawingFromController()
        {
            bool suppressDrawing = SuppressDrawing;
            adapterCore.RefreshDrawing(ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
        }
        
        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            adapterCore.DrawText(location, color, text, font, scale, angle, horizontalAlignment, verticalAlignment, outlineRenderOptions);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ownedTextCache?.Dispose();
                SpriteBatch?.Dispose();
                hostEnvironment.UnregisterMouseMove(MouseMove);
            }
            base.Dispose(disposing);
        }

        public bool IsEnabled
        {
            get => Enabled;
            set => Enabled = value;
        }
    }
}
