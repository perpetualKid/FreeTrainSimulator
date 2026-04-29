using System;
using System.Linq;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FreeTrainSimulator.Graphics.MapView
{
    public class ContentArea : DrawableGameComponent
    {
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        private const int zoomAmplifier = 3;
        private const int scaleMax = 200;

        private readonly MapViewportState viewport;
        private PointD worldPosition;

        internal SpriteBatch SpriteBatch { get; }

        internal BasicShapes BasicShapes { get; }

        private readonly FontManagerInstance fontManager;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextShape contentText;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private double previousScale;
        private PointD previousTopLeft, previousBottomRight;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private MouseInputGameComponent inputComponent;
        private readonly InsetComponent insetComponent;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public ContentBase Content { get; }

        public double Scale => viewport.Scale;
        public PointD CenterPoint => viewport.CenterPoint;

        public bool SuppressDrawing { get; internal set; }

        public Point WindowSize => new Point(viewport.WindowSize.Width, viewport.WindowSize.Height);

        internal PointD TopLeftBound => viewport.TopLeftBound;
        internal PointD BottomRightBound => viewport.BottomRightBound;

        public ref readonly PointD WorldPosition => ref worldPosition;

        public System.Drawing.Font CurrentFont { get; private set; }
        public System.Drawing.Font ConstantSizeFont { get; private set; }

        internal ContentArea(Game game, ContentBase content) :
            base(game)
        {
            ArgumentNullException.ThrowIfNull(game);

            Content = content ?? throw new ArgumentNullException(nameof(content));
            Enabled = false;
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            fontManager = FontManager.Scaled("Arial", System.Drawing.FontStyle.Regular);
            ConstantSizeFont = fontManager[25];
            inputComponent = game.Components.OfType<MouseInputGameComponent>().Single();
            inputComponent.AddMouseEvent(MouseMovedEventType.MouseMoved, MouseMove);
            insetComponent = game.Components.OfType<InsetComponent>().FirstOrDefault();
            contentText = TextShape.Instance(Game, SpriteBatch);
            BasicShapes = BasicShapes.Instance(Game);
            viewport = new MapViewportState(new MapViewportBounds(content.Bounds.Left, content.Bounds.Top, content.Bounds.Right, content.Bounds.Bottom));
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            viewport.UpdateWindowSize(new MapViewportSize(Game.Window.ClientBounds.Size.X, Game.Window.ClientBounds.Size.Y));
        }

        private void RefreshViewportBounds()
        {
            viewport.UpdateBounds(new MapViewportBounds(Content.Bounds.Left, Content.Bounds.Top, Content.Bounds.Right, Content.Bounds.Bottom));
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
                    insetComponent?.UpdateColor(color);
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
            if (!Enabled)
                return;

            worldPosition = ScreenToWorldCoordinates(position);
            if (Scale > 0.2)
                Content.UpdatePointerLocation(worldPosition, viewport.BottomLeftTile, viewport.TopRightTile);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            foreach (TextureContentComponent component in Game.Components.OfType<TextureContentComponent>())
            {
                if (Enabled)
                    component.Enable(this);
                else
                    component.Disable();
            }
            base.OnEnabledChanged(sender, args);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            RefreshViewportBounds();
            viewport.ResetSize(new MapViewportSize(windowSize.X, windowSize.Y), screenDelta);
            UpdateFontSize();
            worldPosition = ScreenToWorldCoordinates(Mouse.GetState().Position);
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            if (centerPoint != PointD.None)
            {
                viewport.PresetPosition(centerPoint, scale);
                UpdateFontSize();
            }
            SuppressDrawing = false;
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            viewport.SetTrackingPosition(PointD.FromWorldLocation(location));
        }

        public void SetTrackingPosition(in PointD location)
        {
            viewport.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            viewport.UpdateScaleToFit(topLeft, bottomRight);
            UpdateFontSize();
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            viewport.UpdateScaleAt(scaleAt.X, scaleAt.Y, steps, scaleMax);
            UpdateFontSize();
        }

        public void UpdateScale(int steps)
        {
            viewport.UpdateScale(steps, scaleMax);
            UpdateFontSize();
        }

        public void UpdateScaleAbsolute(double scale)
        {
            viewport.UpdateScaleAbsolute(scale, scaleMax);
            UpdateFontSize();
        }

        public void UpdatePosition(in Vector2 delta)
        {
            viewport.UpdatePosition(delta.X, delta.Y);
        }

        public override void Update(GameTime gameTime)
        {
            if (Scale != previousScale || TopLeftBound != previousTopLeft || BottomRightBound != previousBottomRight)
            {
                previousScale = Scale;
                previousTopLeft = TopLeftBound;
                previousBottomRight = BottomRightBound;
                SuppressDrawing = false;
            }
            base.Update(gameTime);
        }

        #region public control commands
        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerMoveCommandArgs mouseMoveCommandArgs)
                UpdatePosition(mouseMoveCommandArgs.Delta);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
                UpdateScaleAt(mouseWheelCommandArgs.Position, Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
                UpdateScale(Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveLeft * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveRight * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveUp * MovementAmplifier(commandArgs));
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            UpdatePosition(moveDown * MovementAmplifier(commandArgs));
        }

        private static int MovementAmplifier(UserCommandArgs commandArgs)
        {
            int amplifier = 5;
            if (commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand)
            {
                if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                    amplifier = 1;
                else if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    amplifier = 10;
            }
            return amplifier;
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
            Zoom(ZoomAmplifier(commandArgs));
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            Zoom(-ZoomAmplifier(commandArgs));
        }

        private long nextUpdate;
        private void Zoom(int steps)
        {
            if (Environment.TickCount64 > nextUpdate)
            {
                UpdateScale(steps);
                nextUpdate = Environment.TickCount64 + 30;
            }
        }

        public void ResetZoomAndLocation(Point windowSize, int screenDelta)
        {
            ResetSize(windowSize, screenDelta);
        }
        #endregion

        public override void Draw(GameTime gameTime)
        {
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Content.Draw(viewport.BottomLeftTile, viewport.TopRightTile);
            SpriteBatch.End();
            base.Draw(gameTime);
            SuppressDrawing = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = (worldLocation.TileX * WorldLocation.TileSize) + worldLocation.Location.X;
            double y = (worldLocation.TileZ * WorldLocation.TileSize) + worldLocation.Location.Z;
            return WorldToScreenCoordinates(new PointD(x, y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return viewport.ScreenToWorldCoordinates(screenLocation.X, screenLocation.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            PointD screenLocation = viewport.WorldToScreenCoordinates(location);
            return new Vector2((float)screenLocation.X, (float)screenLocation.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return viewport.WorldToScreenSize(worldSize, minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return viewport.InsideScreenArea(pointPrimitive.Location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return viewport.InsideScreenArea(vectorPrimitive.Location, vectorPrimitive.Vector);
        }

        private void UpdateFontSize()
        {
            int fontsize = MathHelper.Clamp((int)(25 * Scale), 4, 20);
            if (fontsize != (CurrentFont?.Size ?? 0))
                CurrentFont = fontManager[fontsize];
            TrackItemWidget.SetFont(CurrentFont);
        }

        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            contentText.DrawString(WorldToScreenCoordinates(location), color, text, font, scale, angle, horizontalAlignment, verticalAlignment, SpriteEffects.None, SpriteBatch, outlineRenderOptions);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SpriteBatch?.Dispose();
                inputComponent?.RemoveMouseEvent(MouseMovedEventType.MouseMoved, MouseMove);
                inputComponent = null;
            }
            base.Dispose(disposing);
        }
    }
}
