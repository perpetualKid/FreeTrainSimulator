using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.MapView.Widgets;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView
{
    public class ContentArea : DrawableGameComponent
    {
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        private const int zoomAmplifier = 3;
        private const int scaleMax = 200;

        private Rectangle bounds;
        private double scaleMin;
        private static readonly Point PointOverTwo = new Point(2, 2);

        private double offsetX, offsetY;
        internal PointD TopLeftBound { get; private set; }
        internal PointD BottomRightBound { get; private set; }

        private Tile bottomLeft;
        private Tile topRight;
        private PointD worldPosition;

        private int screenHeightDelta;  // to account for Menubar/Statusbar height when calculating initial scale and center view

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

        public double Scale { get; private set; }

        public double CenterX => (BottomRightBound.X - TopLeftBound.X) / 2 + TopLeftBound.X;
        public double CenterY => (TopLeftBound.Y - BottomRightBound.Y) / 2 + BottomRightBound.Y;

        public bool SuppressDrawing { get; internal set; }

        public Point WindowSize { get; private set; }

        public ref readonly PointD WorldPosition => ref worldPosition;

        public System.Drawing.Font CurrentFont { get; private set; }
        public System.Drawing.Font ConstantSizeFont { get; private set; }

        public OutlineRenderOptions FontOutlineOptions 
        {
            get => contentText.OutlineRenderOptions;
            set => contentText.OutlineRenderOptions = value; 
        }

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
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            WindowSize = Game.Window.ClientBounds.Size;
            CenterAround(new PointD((TopLeftBound.X + BottomRightBound.X) / 2, (TopLeftBound.Y + BottomRightBound.Y) / 2));
        }

        public void UpdateColor(ColorSetting setting, Color color)
        {
            switch (setting)
            {
                case ColorSetting.Background:
                    insetComponent?.UpdateColor(color);
                    break;
                case ColorSetting.RailTrack:
                    WidgetColorCache.UpdateColor<TrackSegment>(color);
                    break;
                case ColorSetting.RailTrackEnd:
                    WidgetColorCache.UpdateColor<EndNode>(color);
                    break;
                case ColorSetting.RailTrackJunction:
                    WidgetColorCache.UpdateColor<JunctionNode>(color);
                    break;
                case ColorSetting.RailTrackCrossing:
                    WidgetColorCache.UpdateColor<CrossOverTrackItem>(color);
                    break;
                case ColorSetting.RailLevelCrossing:
                    WidgetColorCache.UpdateColor<LevelCrossingTrackItem>(color);
                    break;
                case ColorSetting.RoadTrack:
                    WidgetColorCache.UpdateColor<RoadSegment>(color);
                    break;
                case ColorSetting.RoadTrackEnd:
                    WidgetColorCache.UpdateColor<RoadEndSegment>(color);
                    break;
                case ColorSetting.PathTrack:
                    WidgetColorCache.UpdateColor<PathSegment>(color);
                    WidgetColorCache.UpdateColor<EditorTrainPathSegment>(color);
                    WidgetColorCache.UpdateColor<EditorTrainPath>(color);
                    break;
                case ColorSetting.StationItem:
                    WidgetColorCache.UpdateColor<StationNameItem>(color);
                    break;
                case ColorSetting.PlatformItem:
                    WidgetColorCache.UpdateColor<PlatformTrackItem>(color);
                    WidgetColorCache.UpdateColor<PlatformPath>(color);
                    color.A = 160;
                    WidgetColorCache.UpdateColor<PlatformSegment>(color);
                    break;
                case ColorSetting.SidingItem:
                    WidgetColorCache.UpdateColor<SidingTrackItem>(color);
                    WidgetColorCache.UpdateColor<SidingPath>(color);
                    color.A = 160;
                    WidgetColorCache.UpdateColor<SidingSegment>(color);
                    break;
                case ColorSetting.SpeedPostItem:
                    WidgetColorCache.UpdateColor<SpeedPostTrackItem>(color);
                    break;
            }
        }

        public void MouseMove(Point position, Vector2 delta, GameTime gameTime)
        {
            if (!Enabled)
                return;

            worldPosition = ScreenToWorldCoordinates(position);
            if (Scale > 0.2)
                Content.UpdatePointerLocation(worldPosition, bottomLeft, topRight);
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
            bounds = Content.Bounds;
            base.Initialize();
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            WindowSize = windowSize;
            screenHeightDelta = screenDelta;
            ScaleToFit();
            CenterView();
            worldPosition = ScreenToWorldCoordinates(Mouse.GetState().Position);
        }

        public void PresetPosition(string[] locationDetails)
        {
            if (locationDetails == null || locationDetails.Length != 3)
                return;
            if (double.TryParse(locationDetails[0], out double lon) && double.TryParse(locationDetails[1], out double lat) && double.TryParse(locationDetails[2], out double scale))
            {
                Scale = scale;
                UpdateFontSize();
                CenterAround(new PointD(lon, lat));
            }
            SuppressDrawing = false;
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            CenterAround(PointD.FromWorldLocation(location));
        }

        public void SetTrackingPosition(in PointD location)
        {
            CenterAround(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {            
            double xScale = WindowSize.X / Math.Abs(bottomRight.X - topLeft.X);
            double yScale = (WindowSize.Y - screenHeightDelta) / Math.Abs(topLeft.Y - bottomRight.Y);
            Scale = Math.Min(xScale, yScale) * 0.95;
            UpdateFontSize();
            SetBounds();
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.95 : (steps < 0 ? 0.95 : 1)), Math.Abs(steps));
            if ((scale < scaleMin && steps < 0) || (scale > scaleMax && steps > 0))
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;

            UpdateFontSize();
            SetBounds();
        }

        public void UpdateScale(int steps)
        {
            UpdateScaleAt(WindowSize / PointOverTwo, steps);
        }

        public void UpdateScaleAbsolute(double scale)
        {
            if (scale < scaleMin)
                scale = scaleMin;
            else if (scale > scaleMax)
                scale = scaleMax;
            Scale = scale;

            UpdateFontSize();
        }

        public void UpdatePosition(in Vector2 delta)
        {
            offsetX -= delta.X / Scale;
            offsetY += delta.Y / Scale;

            TopLeftBound = ScreenToWorldCoordinates(Point.Zero);
            BottomRightBound = ScreenToWorldCoordinates(WindowSize);

            if (TopLeftBound.X > bounds.Right)
            {
                offsetX = bounds.Right;
            }
            else if (BottomRightBound.X < bounds.Left)
            {
                offsetX = bounds.Left - (BottomRightBound.X - TopLeftBound.X);
            }

            if (BottomRightBound.Y > bounds.Bottom)
            {
                offsetY = bounds.Bottom;
            }
            else if (TopLeftBound.Y < bounds.Top)
            {
                offsetY = bounds.Top - (TopLeftBound.Y - BottomRightBound.Y);
            }
            SetBounds();
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
            {
                UpdatePosition(mouseMoveCommandArgs.Delta);
            }
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
            {
                UpdateScaleAt(mouseWheelCommandArgs.Position, Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
            }
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
            {
                UpdateScale(Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
            }
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

        private void SetBounds()
        {
            TopLeftBound = ScreenToWorldCoordinates(Point.Zero);
            BottomRightBound = ScreenToWorldCoordinates(WindowSize);

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftBound.X), Tile.TileFromAbs(BottomRightBound.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightBound.X), Tile.TileFromAbs(TopLeftBound.Y));
        }

        private void CenterView()
        {
            offsetX = (bounds.Left + bounds.Right) / 2 - WindowSize.X / 2 / Scale;
            offsetY = (bounds.Top + bounds.Bottom) / 2 - (WindowSize.Y) / 2 / Scale;
            SetBounds();
        }

        private void CenterAround(in PointD centerPoint)
        {
            offsetX = centerPoint.X - WindowSize.X / 2 / Scale;
            offsetY = centerPoint.Y - (WindowSize.Y) / 2 / Scale;
            SetBounds();
        }

        private void ScaleToFit()
        {
            double xScale = (double)WindowSize.X / bounds.Width;
            double yScale = (double)(WindowSize.Y - screenHeightDelta) / bounds.Height;
            Scale = Math.Min(xScale, yScale);
            scaleMin = Scale * 0.75;
            UpdateFontSize();
        }

        public override void Draw(GameTime gameTime)
        {
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Content.Draw(bottomLeft, topRight);
            SpriteBatch.End();
            base.Draw(gameTime);
            SuppressDrawing = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)), (float)(WindowSize.Y - Scale * (y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return new PointD(offsetX + screenLocation.X / Scale, offsetY + (WindowSize.Y - screenLocation.Y) / Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(Scale * (location.X - offsetX)),
                               (float)(WindowSize.Y - Scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            ref readonly PointD location = ref pointPrimitive.Location;
            return location.X > TopLeftBound.X && location.X < BottomRightBound.X && location.Y < TopLeftBound.Y && location.Y > BottomRightBound.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            ref readonly PointD start = ref vectorPrimitive.Location;
            ref readonly PointD end = ref vectorPrimitive.Vector;
            bool outside = (start.X < TopLeftBound.X && end.X < TopLeftBound.X) || (start.X > BottomRightBound.X && end.X > BottomRightBound.X) ||
                (start.Y > TopLeftBound.Y && end.Y > TopLeftBound.Y) || (start.Y < BottomRightBound.Y && end.Y < BottomRightBound.Y);
            return !outside;
        }

        private void UpdateFontSize()
        {
            int fontsize = MathHelper.Clamp((int)(25 * Scale), 4, 20);
            if (fontsize != (CurrentFont?.Size ?? 0))
                CurrentFont = fontManager[fontsize];
            TrackItemWidget.SetFont(CurrentFont);
        }

        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
        {
            contentText.DrawString(WorldToScreenCoordinates(location), color, text, font, scale, angle, horizontalAlignment, verticalAlignment, SpriteEffects.None, SpriteBatch);
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
