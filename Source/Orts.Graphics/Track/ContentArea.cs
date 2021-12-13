using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Input;
using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.Track.Widgets;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Track
{
    public class ContentArea : DrawableGameComponent, INameValueInformationProvider
    {
        private Rectangle bounds;
        private double maxScale;
        private static readonly Point PointOverTwo = new Point(2, 2);

                private double offsetX, offsetY;
        internal PointD TopLeftArea { get; private set; }
        internal PointD BottomRightArea { get; private set; }

        private Tile bottomLeft;
        private Tile topRight;

        private int screenHeightDelta;  // to account for Menubar/Statusbar height when calculating initial scale and center view

        private readonly SpriteBatch spriteBatch;
        internal SpriteBatch SpriteBatch => spriteBatch;

        private readonly FontManagerInstance fontManager;
        private System.Drawing.Font currentFont;

        private double previousScale;
        private PointD previousTopLeft, previousBottomRight;
        private TrackViewerViewSettings viewSettings;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private MouseInputGameComponent inputComponent;
#pragma warning restore CA2213 // Disposable fields should be disposed

        #region nearest items
        private GridTile nearestGridTile;
        private TrackItemBase nearestTrackItem;
        private TrackSegment nearestTrackSegment;
        private RoadSegment nearesRoadSegment;
        #endregion

        public TrackContent TrackContent { get; }

        public double Scale { get; private set; }

        public double CenterX => (BottomRightArea.X - TopLeftArea.X) / 2 + TopLeftArea.X;
        public double CenterY => (TopLeftArea.Y - BottomRightArea.Y) / 2 + BottomRightArea.Y;

        public bool SuppressDrawing { get; private set; }

        public Point WindowSize { get; private set; }

        public string RouteName { get; }

        public NameValueCollection DebugInfo { get; } = new NameValueCollection();

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public ContentArea(Game game, string routeName, TrackContent trackContent, EnumArray<string, ColorSetting> colorPreferences, TrackViewerViewSettings viewSettings) :
            base(game)
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));
            if (null == colorPreferences)
                throw new ArgumentNullException(nameof(colorPreferences));

            Enabled = false;
            RouteName = routeName;
            TrackContent = trackContent ?? throw new ArgumentNullException(nameof(trackContent));
            bounds = trackContent.Bounds;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            fontManager = FontManager.Exact("Segoe UI", System.Drawing.FontStyle.Regular);
            inputComponent = game.Components.OfType<MouseInputGameComponent>().Single();
            inputComponent.AddMouseEvent(MouseMovedEventType.MouseMoved, MouseMove);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]));
            }
            this.viewSettings = viewSettings;
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
            DebugInfo["Route Name"] = routeName;
            DebugInfo["Metric Scale"] = trackContent.UseMetricUnits.ToString();
            DebugInfo["Track Segments"] = $"{trackContent.TrackSegments.Count}";
            DebugInfo["Track Nodes"] = $"{trackContent.TrackNodeSegments.Count}";
            DebugInfo["Track Items"] = $"{trackContent.TrackItems.Count}";
            DebugInfo["Track End Segments"] = $"{trackContent.TrackEndSegments.Count}";
            DebugInfo["Road Segments"] = $"{trackContent.RoadSegments.Count}";
            DebugInfo["Road End Segments"] = $"{trackContent.RoadEndSegments.Count}";
            DebugInfo["Junction Segments"] = $"{trackContent.JunctionSegments.Count}";
            DebugInfo["Tiles"] = $"{trackContent.Tiles.Count}";
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

            PointD worldPosition = ScreenToWorldCoordinates(position);
            FindNearestItems(worldPosition);
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

        private void FindNearestItems(PointD position)
        {
            IEnumerable<ITileCoordinate<Tile>> result = TrackContent.Tiles.FindNearest(position, bottomLeft, topRight);
            if (result.First() != nearestGridTile)
            {
                nearestGridTile = result.First() as GridTile;
            }
            if (Scale < 1)
            {
                return;
            }
            double distance = double.MaxValue;
            foreach (TrackItemBase trackItem in TrackContent.TrackItems[nearestGridTile.Tile])
            {
                double itemDistance = trackItem.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestTrackItem = trackItem;
                    distance = itemDistance;
                }
            }
            distance = double.MaxValue;
            foreach (TrackSegment trackSegment in TrackContent.TrackSegments[nearestGridTile.Tile])
            {
                double itemDistance = position.DistanceToLineSegmentSquared(trackSegment.Location, trackSegment.Vector);
                if (itemDistance < distance)
                {
                    nearestTrackSegment = trackSegment;
                    distance = itemDistance;
                }
            }
            distance = double.MaxValue;
            foreach (RoadSegment trackSegment in TrackContent.RoadSegments[nearestGridTile.Tile])
            {
                double itemDistance = position.DistanceToLineSegmentSquared(trackSegment.Location, trackSegment.Vector);
                if (itemDistance < distance)
                {
                    nearesRoadSegment = trackSegment;
                    distance = itemDistance;
                }
            }

        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            WindowSize = windowSize;
            screenHeightDelta = screenDelta;
            ScaleToFit();
            CenterView();
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

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            double scale = Scale * Math.Pow((steps > 0 ? 1 / 0.95 : (steps < 0 ? 0.95 : 1)), Math.Abs(steps));
            if ((scale < maxScale && steps < 0) || (scale > 200 && steps > 0))
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

        public void UpdateColor(ColorSetting setting, Color color)
        {
            switch (setting)
            {
                case ColorSetting.Background:
                    Game.Components.OfType<InsetComponent>().FirstOrDefault()?.UpdateColor(color);
                    break;
                case ColorSetting.RailTrack:
                    PointWidget.UpdateColor<TrackSegment>(color);
                    break;
                case ColorSetting.RailTrackEnd:
                    PointWidget.UpdateColor<TrackEndSegment>(color);
                    break;
                case ColorSetting.RailTrackJunction:
                    PointWidget.UpdateColor<JunctionSegment>(color);
                    break;
                case ColorSetting.RailTrackCrossing:
                    PointWidget.UpdateColor<CrossOverTrackItem>(color);
                    break;
                case ColorSetting.RailLevelCrossing:
                    PointWidget.UpdateColor<LevelCrossingTrackItem>(color);
                    break;
                case ColorSetting.RoadTrack:
                    PointWidget.UpdateColor<RoadSegment>(color);
                    break;
                case ColorSetting.RoadTrackEnd:
                    PointWidget.UpdateColor<RoadEndSegment>(color);
                    break;
                case ColorSetting.PlatformItem:
                    PointWidget.UpdateColor<PlatformTrackItem>(color);
                    break;
                case ColorSetting.SidingItem:
                    PointWidget.UpdateColor<SidingTrackItem>(color);
                    break;
                case ColorSetting.SpeedPostItem:
                    PointWidget.UpdateColor<SpeedPostTrackItem>(color);
                    break;
            }
        }

        public void UpdateItemVisiblity(TrackViewerViewSettings viewSettings)
        {
            this.viewSettings = viewSettings;
        }

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
            maxScale = Scale * 0.75;
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
        internal Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            double x = worldLocation.TileX * WorldLocation.TileSize + worldLocation.Location.X;
            double y = worldLocation.TileZ * WorldLocation.TileSize + worldLocation.Location.Z;
            return new Vector2((float)(Scale * (x - offsetX)),
                               (float)(WindowSize.Y - Scale * (y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return new PointD(offsetX + screenLocation.X / Scale, offsetY + (WindowSize.Y - screenLocation.Y) / Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return new Vector2((float)(Scale * (location.X - offsetX)),
                               (float)(WindowSize.Y - Scale * (location.Y - offsetY)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return Math.Max((float)Math.Ceiling(worldSize * Scale), minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(PointWidget pointWidget)
        {
            ref readonly PointD location = ref pointWidget.Location;
            return location.X > TopLeftArea.X && location.X < BottomRightArea.X && location.Y < TopLeftArea.Y && location.Y > BottomRightArea.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsideScreenArea(VectorWidget vectorWidget)
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

            if ((viewSettings & TrackViewerViewSettings.Grid) == TrackViewerViewSettings.Grid)
            {
                foreach (GridTile tile in TrackContent.Tiles.BoundingBox(bottomLeft, topRight))
                {
                    tile.Draw(this);
                }
                nearestGridTile?.Draw(this, ColorVariation.Complement);
            }
            if ((viewSettings & TrackViewerViewSettings.Tracks) == TrackViewerViewSettings.Tracks)
            {
                foreach (TrackSegment segment in TrackContent.TrackSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (InsideScreenArea(segment))
                        segment.Draw(this);
                }
                if (nearestTrackSegment != null)
                {
                    foreach (TrackSegment segment in TrackContent.TrackNodeSegments[nearestTrackSegment.TrackNodeIndex])
                    {
                        segment.Draw(this, ColorVariation.ComplementHighlight);
                    }
                    nearestTrackSegment.Draw(this, ColorVariation.Complement);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.EndsNodes) == TrackViewerViewSettings.EndsNodes)
            {
                foreach (TrackEndSegment endNode in TrackContent.TrackEndSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (InsideScreenArea(endNode))
                        endNode.Draw(this);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.JunctionNodes) == TrackViewerViewSettings.JunctionNodes)
            {
                foreach (JunctionSegment junctionNode in TrackContent.JunctionSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (InsideScreenArea(junctionNode))
                        junctionNode.Draw(this);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.Roads) == TrackViewerViewSettings.Roads)
            {
                foreach (RoadSegment segment in TrackContent.RoadSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (InsideScreenArea(segment))
                        segment.Draw(this);
                }
                if (nearesRoadSegment != null)
                {
                    foreach (RoadSegment segment in TrackContent.RoadTrackNodeSegments[nearesRoadSegment.TrackNodeIndex])
                    {
                        segment.Draw(this, ColorVariation.ComplementHighlight);
                    }
                    nearesRoadSegment.Draw(this, ColorVariation.Complement);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.RoadEndNodes) == TrackViewerViewSettings.RoadEndNodes)
            {
                foreach (RoadEndSegment endNode in TrackContent.RoadEndSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (InsideScreenArea(endNode))
                        endNode.Draw(this);
                }
            }
            foreach (TrackItemBase trackItem in TrackContent.TrackItems.BoundingBox(bottomLeft, topRight))
            {
                if (trackItem.ShouldDraw(viewSettings) && InsideScreenArea(trackItem))
                    trackItem.Draw(this);
            }
            if (nearestTrackItem?.ShouldDraw(viewSettings) ?? false)
                nearestTrackItem.Draw(this, ColorVariation.Highlight);
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
