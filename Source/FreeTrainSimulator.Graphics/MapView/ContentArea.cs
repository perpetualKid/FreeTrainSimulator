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
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

        private readonly MapTextTextureCache ownedTextCache;

        public ContentBase Content { get; }

        public double Scale => controller.Scale;
        public PointD CenterPoint => controller.CenterPoint;
        public bool SuppressDrawing { get; internal set; }
        public Point WindowSize => controller.WindowSize;
        internal PointD TopLeftBound => controller.TopLeftBound;
        internal PointD BottomRightBound => controller.BottomRightBound;
        public PointD WorldPosition => controller.WorldPosition;
        public System.Drawing.Font CurrentFont { get; private set; }
        public System.Drawing.Font ConstantSizeFont { get; private set; }

        internal ContentArea(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent) :
            base(game)
        {
            ArgumentNullException.ThrowIfNull(game);

            Content = content ?? throw new ArgumentNullException(nameof(content));
            Enabled = false;
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            renderingLifetime = new XnaMapRenderingLifetime(game);
            renderingResources = new XnaMapRenderingResources(renderingLifetime, SpriteBatch);
            ownedTextCache = new MapTextTextureCache(renderingLifetime.GetTextTextureRenderer());
            textCache = ownedTextCache;
            textRenderer = new XnaMapTextRenderer(textCache, SpriteBatch);
            renderBackend = new XnaMapRenderBackend(SpriteBatch, renderingResources.BasicShapes, textRenderer);
            fontManager = FontManager.Scaled("Arial", System.Drawing.FontStyle.Regular);
            ConstantSizeFont = fontManager[25];
            hostEnvironment = new XnaMapHostEnvironment(game, mouseInputGameComponent);
            hostEnvironment.RegisterMouseMove(MouseMove);
            BasicShapes = renderingResources.BasicShapes;
            controller = new MapViewController(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom));
            controller.SyncViewport(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom), hostEnvironment.ClientSize);
            hostEnvironment.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            controller.SyncViewport(new MapViewportBounds(Content.Bounds.Left, Content.Bounds.Top, Content.Bounds.Right, Content.Bounds.Bottom), hostEnvironment.ClientSize);
        }

        private void RefreshViewportBounds()
        {
            controller.SyncViewport(new MapViewportBounds(Content.Bounds.Left, Content.Bounds.Top, Content.Bounds.Right, Content.Bounds.Bottom), hostEnvironment.ClientSize);
        }

        public static void UpdateTrackWidthSettings(bool limitTrackWidth)
        {
            TrackSegment.UpdateTrackWidthRatio(limitTrackWidth);
            JunctionNode.UpdateTrackWidthRatio(limitTrackWidth);
            SpeedPostTrackItem.UpdateTrackWidthRatio(limitTrackWidth);
        }

        public void UpdateColor(ColorSetting setting, Color color, bool fontOutlining)
        {
            switch (setting)
            {
                case ColorSetting.Background:
                    Content.InsetHost?.UpdateColor(color);
                    break;
                case ColorSetting.RailTrack:
                    WidgetDrawingOptions<TrackSegment>.SetColors(color);
                    break;
                case ColorSetting.RailTrackEnd:
                    WidgetDrawingOptions<EndNode>.SetColors(color);
                    break;
                case ColorSetting.RailTrackJunction:
                    WidgetDrawingOptions<JunctionNode>.SetColors(color);
                    break;
                case ColorSetting.RailTrackCrossing:
                    WidgetDrawingOptions<CrossOverTrackItem>.SetColors(color);
                    break;
                case ColorSetting.RailLevelCrossing:
                    WidgetDrawingOptions<LevelCrossingTrackItem>.SetColors(color);
                    break;
                case ColorSetting.RoadTrack:
                    WidgetDrawingOptions<RoadSegment>.SetColors(color);
                    break;
                case ColorSetting.RoadTrackEnd:
                    WidgetDrawingOptions<RoadEndSegment>.SetColors(color);
                    break;
                case ColorSetting.PathTrack:
                    WidgetDrawingOptions<PathSegment>.SetColors(color);
                    WidgetDrawingOptions<EditorTrainPathSegment>.SetColors(color);
                    WidgetDrawingOptions<EditorTrainPath>.SetColors(color);
                    break;
                case ColorSetting.StationItem:
                    WidgetDrawingOptions<StationNameItem>.SetColors(color);
                    WidgetDrawingOptions<StationNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(3.0f, color, color.ContrastColor()) : null;
                    break;
                case ColorSetting.PlatformItem:
                    WidgetDrawingOptions<PlatformNameItem>.SetColors(color);
                    WidgetDrawingOptions<PlatformNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    WidgetDrawingOptions<PlatformPath>.SetColors(color);
                    color.A = 160;
                    WidgetDrawingOptions<PlatformSegment>.SetColors(color);
                    break;
                case ColorSetting.SidingItem:
                    WidgetDrawingOptions<SidingNameItem>.SetColors(color);
                    WidgetDrawingOptions<SidingNameItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    WidgetDrawingOptions<SidingPath>.SetColors(color);
                    color.A = 160;
                    WidgetDrawingOptions<SidingSegment>.SetColors(color);
                    break;
                case ColorSetting.SpeedPostItem:
                    WidgetDrawingOptions<SpeedPostTrackItem>.SetColors(color);
                    WidgetDrawingOptions<SpeedPostTrackItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color.ContrastColor(), color) : null;
                    break;
                case ColorSetting.MilePostItem:
                    WidgetDrawingOptions<MilePostTrackItem>.SetColors(color);
                    WidgetDrawingOptions<MilePostTrackItem>.OutlineRenderOptions = fontOutlining ? new OutlineRenderOptions(2.0f, color, color.ContrastColor()) : null;
                    break;
            }
        }

        public void MouseMove(Point position, Vector2 delta, GameTime gameTime)
        {
            controller.MouseMove(Enabled, position, Content);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            if (Enabled)
                Content.TextureHelperHost?.Enable(this);
            else
                Content.TextureHelperHost?.Disable();
            base.OnEnabledChanged(sender, args);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            RefreshViewportBounds();
            controller.ResetSize(windowSize, screenDelta, hostEnvironment.PointerPosition);
            RefreshFontsFromController();
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            controller.PresetPosition(centerPoint, scale);
            RefreshFontsFromController();
            RefreshDrawingFromController();
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            controller.SetTrackingPosition(location);
        }

        public void SetTrackingPosition(in PointD location)
        {
            controller.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            controller.UpdateScaleToFit(topLeft, bottomRight);
            RefreshFontsFromController();
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            controller.UpdateScaleAt(scaleAt, steps);
            RefreshFontsFromController();
        }

        public void UpdateScale(int steps)
        {
            controller.UpdateScale(steps);
            RefreshFontsFromController();
        }

        public void UpdateScaleAbsolute(double scale)
        {
            controller.UpdateScaleAbsolute(scale);
            RefreshFontsFromController();
        }

        public void UpdatePosition(in Vector2 delta)
        {
            controller.UpdatePosition(delta);
        }

        public override void Update(GameTime gameTime)
        {
            controller.UpdateFrameState();
            RefreshDrawingFromController();
            base.Update(gameTime);
        }

        #region public control commands
        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            controller.MouseDragging(userCommandArgs);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            controller.MouseWheelAt(userCommandArgs, modifiers);
            RefreshFontsFromController();
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            controller.MouseWheel(userCommandArgs, modifiers);
            RefreshFontsFromController();
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyLeft(commandArgs);
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyRight(commandArgs);
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyUp(commandArgs);
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            controller.MoveByKeyDown(commandArgs);
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
            controller.ZoomIn(commandArgs);
            RefreshFontsFromController();
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            controller.ZoomOut(commandArgs);
            RefreshFontsFromController();
        }

        public void ResetZoomAndLocation(Point windowSize, int screenDelta)
        {
            ResetSize(windowSize, screenDelta);
        }
        #endregion

        public override void Draw(GameTime gameTime)
        {
            renderBackend.BeginFrame();
            Content.Draw(controller.BottomLeftTile, controller.TopRightTile);
            renderBackend.EndFrame();
            controller.NotifyFrameRendered();
            SuppressDrawing = true;
            base.Draw(gameTime);
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            renderBackend.DrawLine(width, color, point, length, angle);
        }

        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderBackend.DrawLine(width, color, point1, point2);
        }

        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderBackend.DrawDashedLine(width, color, point1, point2);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize)
        {
            renderBackend.DrawArc(width, color, point, radius, angle, arcSize);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            renderBackend.DrawTexture(texture, point, angle, size, flipHorizontal, flipVertical, highlight);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
        {
            renderBackend.DrawTexture(texture, point, angle, size, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            return controller.WorldToScreenCoordinates(worldLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return controller.ScreenToWorldCoordinates(screenLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return controller.WorldToScreenCoordinates(location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return controller.WorldToScreenSize(worldSize, minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            ArgumentNullException.ThrowIfNull(pointPrimitive, nameof(pointPrimitive));
            return controller.InsideScreenArea(pointPrimitive.Location);
        }

        bool IMapViewport.InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return InsideScreenArea(pointPrimitive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            ArgumentNullException.ThrowIfNull(vectorPrimitive, nameof(vectorPrimitive));
            return controller.InsideScreenArea(vectorPrimitive.Location, vectorPrimitive.Vector);
        }

        bool IMapViewport.InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return InsideScreenArea(vectorPrimitive);
        }

        private void UpdateFontSize()
        {
            int fontsize = MathHelper.Clamp((int)(25 * Scale), 4, 20);
            if (fontsize != (CurrentFont?.Size ?? 0))
                CurrentFont = fontManager[fontsize];
            TrackItemWidget.SetFont(CurrentFont);
        }

        private void RefreshFontsFromController()
        {
            if (controller.ConsumeScaleChanged())
                UpdateFontSize();
        }

        private void RefreshDrawingFromController()
        {
            if (controller.ConsumeRedrawRequested())
                SuppressDrawing = false;
        }
        
        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            renderBackend.DrawText(WorldToScreenCoordinates(location), color, text, font, scale, angle, horizontalAlignment, verticalAlignment, outlineRenderOptions);
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
