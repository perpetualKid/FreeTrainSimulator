using System;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using Orts.Common.Input;
using Orts.Common.Position;
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
        internal PointD TopLeftArea { get; private set; }
        internal PointD BottomRightArea { get; private set; }

        private Tile bottomLeft;
        private Tile topRight;
        private PointD worldPosition;

        private int screenHeightDelta;  // to account for Menubar/Statusbar height when calculating initial scale and center view

        private readonly SpriteBatch spriteBatch;
        internal SpriteBatch SpriteBatch => spriteBatch;

        private readonly FontManagerInstance fontManager;
        private System.Drawing.Font currentFont;

        private double previousScale;
        private PointD previousTopLeft, previousBottomRight;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private MouseInputGameComponent inputComponent;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public ContentBase Content { get; }

        public double Scale { get; private set; }

        public double CenterX => (BottomRightArea.X - TopLeftArea.X) / 2 + TopLeftArea.X;
        public double CenterY => (TopLeftArea.Y - BottomRightArea.Y) / 2 + BottomRightArea.Y;

        public bool SuppressDrawing { get; private set; }

        public Point WindowSize { get; private set; }

        public ref readonly PointD WorldPosition => ref worldPosition;

        internal ContentArea(Game game, ContentBase content) :
            base(game)
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            Content = content ?? throw new ArgumentNullException(nameof(content));
            Enabled = false;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            fontManager = FontManager.Exact("Segoe UI", System.Drawing.FontStyle.Regular);
            inputComponent = game.Components.OfType<MouseInputGameComponent>().Single();
            inputComponent.AddMouseEvent(MouseMovedEventType.MouseMoved, MouseMove);

            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            WindowSize = Game.Window.ClientBounds.Size;
            CenterAround(new PointD((TopLeftArea.X + BottomRightArea.X) / 2, (TopLeftArea.Y + BottomRightArea.Y) / 2));
        }

        public void MouseMove(Point position, Vector2 delta, GameTime gameTime)
        {
            if (!Enabled)
                return;

            worldPosition = ScreenToWorldCoordinates(position);
            if (Scale > 0.5)
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
                CenterAround(new PointD(lon, lat));
            }
            SuppressDrawing = false;
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            CenterAround(PointD.FromWorldLocation(location));
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.95 : (steps < 0 ? 0.95 : 1)), Math.Abs(steps));
            if ((scale < scaleMin && steps < 0) || (scale > scaleMax && steps > 0))
                return;
            offsetX += scaleAt.X * (scale / Scale - 1.0) / scale;
            offsetY += (WindowSize.Y - scaleAt.Y) * (scale / Scale - 1.0) / scale;
            Scale = scale;

            SetBounds();
        }

        public void UpdateScale(int steps)
        {
            UpdateScaleAt(WindowSize / PointOverTwo, steps);
        }

        public void UpdateScaleAbsolut(double scale)
        {
            if (scale < scaleMin)
                scale = scaleMin;
            else if (scale > scaleMax)
                scale = scaleMax;
            Scale = scale;
        }

        public void UpdatePosition(in Vector2 delta)
        {
            offsetX -= delta.X / Scale;
            offsetY += delta.Y / Scale;

            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            if (TopLeftArea.X > bounds.Right)
            {
                offsetX = bounds.Right;
            }
            else if (BottomRightArea.X < bounds.Left)
            {
                offsetX = bounds.Left - (BottomRightArea.X - TopLeftArea.X);
            }

            if (BottomRightArea.Y > bounds.Bottom)
            {
                offsetY = bounds.Bottom;
            }
            else if (TopLeftArea.Y < bounds.Top)
            {
                offsetY = bounds.Top - (TopLeftArea.Y - BottomRightArea.Y);
            }
            SetBounds();
        }

        public override void Update(GameTime gameTime)
        {
            if (Scale == previousScale && TopLeftArea == previousTopLeft && BottomRightArea == previousBottomRight)
            {
                SuppressDrawing = true;
            }
            else
            {
                previousScale = Scale;
                previousTopLeft = TopLeftArea;
                previousBottomRight = BottomRightArea;
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

        private DateTime nextUpdate;
        private void Zoom(int steps)
        {
            if (DateTime.UtcNow > nextUpdate)
            {
                UpdateScale(steps);
                nextUpdate = DateTime.UtcNow.AddMilliseconds(30);
            }
        }

        public void ResetZoomAndLocation(Point windowSize, int screenDelta)
        {
            ResetSize(windowSize, screenDelta);
        }
        #endregion

        private void SetBounds()
        {
            TopLeftArea = ScreenToWorldCoordinates(Point.Zero);
            BottomRightArea = ScreenToWorldCoordinates(WindowSize);

            bottomLeft = new Tile(Tile.TileFromAbs(TopLeftArea.X), Tile.TileFromAbs(BottomRightArea.Y));
            topRight = new Tile(Tile.TileFromAbs(BottomRightArea.X), Tile.TileFromAbs(TopLeftArea.Y));
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
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            DrawContent();
            spriteBatch.End();
            base.Draw(gameTime);
            SuppressDrawing = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - Scale * (y - offsetY)));
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
        internal bool InsideScreenArea(PointWidget pointWidget)
        {
            ref readonly PointD location = ref pointWidget.Location;
            return location.X > TopLeftArea.X && location.X < BottomRightArea.X && location.Y < TopLeftArea.Y && location.Y > BottomRightArea.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool InsideScreenArea(VectorWidget vectorWidget)
        {
            ref readonly PointD start = ref vectorWidget.Location;
            ref readonly PointD end = ref vectorWidget.Vector;
            bool outside = (start.X < TopLeftArea.X && end.X < TopLeftArea.X) || (start.X > BottomRightArea.X && end.X > BottomRightArea.X) ||
                (start.Y > TopLeftArea.Y && end.Y > TopLeftArea.Y) || (start.Y < BottomRightArea.Y && end.Y < BottomRightArea.Y);
            return !outside;
        }

        private void DrawContent()
        {
            int fontsize = MathHelper.Clamp((int)(25 * Scale), 1, 25);
            if (fontsize != (currentFont?.Size ?? 0))
                currentFont = fontManager[fontsize];
            TrackItemBase.SetFont(currentFont);

            Content.Draw(bottomLeft, topRight);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                inputComponent?.RemoveMouseEvent(MouseMovedEventType.MouseMoved, MouseMove);
                inputComponent = null;

            }
            base.Dispose(disposing);
        }
    }
}
