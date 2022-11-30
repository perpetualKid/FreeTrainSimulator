using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.Info;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class TrackItemsContainer
    {
        public List<(TrackMonitorSignalAspect, float, float)> Signals { get; } = new List<(TrackMonitorSignalAspect, float, float)>();
        public List<(float, float)> Mileposts { get; } = new List<(float, float)>();
        public List<(float, bool)> Switches { get; } = new List<(float, bool)>();
        public List<(float, double, Color)> Speedposts { get; } = new List<(float, double, Color)>();
        public List<(float, int)> Platforms { get; } = new List<(float, int)>();
        public List<(float, bool?)> Authorities { get; } = new List<(float, bool?)>();
        public List<(float, bool, bool)> Reversals { get; } = new List<(float, bool, bool)>();
        public List<(float, bool)> WaitingPoints { get; } = new List<(float, bool)>();

        public TrackItemsContainer(Direction direction)
        {
            Direction = direction;
        }

        public Direction Direction { get; }

        public void Clear()
        {
            Signals.Clear();
            Mileposts.Clear();
            Switches.Clear();
            Speedposts.Clear();
            Platforms.Clear();
            Authorities.Clear();
            Reversals.Clear();
            WaitingPoints.Clear();
        }
    }

    public class TrackMonitorControl : WindowControl
    {
        private const double maxDistance = 5000.0;

        public enum Symbols
        {
            Eye,
            TrainForwardAuto,
            TrainBackwardAuto,
            TrainOnRouteManual,
            TrainOffRouteManual,
            EndOfAuthority,
            OppositeTrainForward,
            OppositeTrainBackward,
            Station,
            Reversal,
            WaitingPoint,
            ArrowForward,
            ArrowBackward,
            ArrowLeft,
            ArrowRight,
            Invalid,
        }

        public enum TrainPositionMode
        {
            None,
            ForwardAuto,
            BothWaysManual,
            ForwardMultiPlayer,
            BackwardMultiPlayer,
            NeutralMultiPlayer,
        }

        private readonly TextTextureResourceHolder textureHolder;
        private Texture2D symbolTexture;
        private Texture2D signalTexture;
        private readonly float scaling;

        //precalculated values
        private readonly int iconAdjustment = 9;
        private readonly int trackItemOffset = 42;
        private readonly int directionArrowOffset = 26;
        private readonly int milepostOffset = 4;
        private readonly int signalOffset = 92;
        private readonly int speedpostOffset = 70;
        private readonly int distanceMarkerOffset = 115;
        private readonly Point trackLeftRail;
        private readonly Point trackRightRail;
        private readonly int iconSize = 24;
        private readonly int aspectSize = 16;
        private readonly bool metric;
        private readonly double maxDistanceDisplay;

        private bool trainSymbolPositionChange;
        private TrainPositionMode controlMode;
        private int trainPositionOffset;
        private double distanceFactor;
        private double markerInterval;
        private int nearestItem;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextTextureResourceHolder textRenderer;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly List<(Texture2D, Point)> distanceMarkers = new List<(Texture2D, Point)>();

        private readonly EnumArray<Rectangle, Symbols> symbols = new EnumArray<Rectangle, Symbols>(new Rectangle[] {
            new Rectangle(0, 144, 24, 24),
            new Rectangle(0, 72, 24, 24),
            new Rectangle(24, 72, 24, 24),
            new Rectangle(24, 96, 24, 24),
            new Rectangle(0, 96, 24, 24),
            new Rectangle(0, 0, 24, 24),
            new Rectangle(24, 120, 24, 24),
            new Rectangle(0, 120, 24, 24),
            new Rectangle(24, 0, 24, 24),
            new Rectangle(0, 24, 24, 24),
            new Rectangle(24, 24, 24, 24),
            new Rectangle(24, 48, 24, 24),
            new Rectangle(0, 48, 24, 24),
            new Rectangle(0, 168, 24, 24),
            new Rectangle(24, 168, 24, 24),
            new Rectangle(24, 144, 24, 24),
        });

        private readonly EnumArray<Rectangle, TrackMonitorSignalAspect> signalAspects = new EnumArray<Rectangle, TrackMonitorSignalAspect>(new Rectangle[] {
            new Rectangle(16, 64, 16, 16),
            new Rectangle(0, 0, 16, 16),
            new Rectangle(16, 0, 16, 16),
            new Rectangle(0, 16, 16, 16),
            new Rectangle(16, 16, 16, 16),
            new Rectangle(0, 32, 16, 16),
            new Rectangle(16, 32, 16, 16),
            new Rectangle(0, 48, 16, 16),
            new Rectangle(16, 48, 16, 16),
            new Rectangle(0, 64, 16, 16),
        });

        public Color SpeedingColor { get; set; }
        public Symbols TrainSymbol { get; set; }
        public Direction CabOrientation { get; set; }
        public MidpointDirection TrainDirection { get; set; }
        public bool TrainOnRoute { get; set; }
        public TrainPositionMode PositionMode { get; set; }

        public TrackItemsContainer ForwardItems { get; } = new TrackItemsContainer(Direction.Forward);
        public TrackItemsContainer BackwardItems { get; } = new TrackItemsContainer(Direction.Backward);

        public TrackMonitorControl(FormBase window, int width, int height, bool metric) :
            base(window, 0, 0, width, height)
        {
            if ((width / Window.Owner.DpiScaling) < 150)
                throw new ArgumentOutOfRangeException(nameof(width), "TrackMonitor width must be 150 or more");
            if ((height / Window.Owner.DpiScaling) < 200)
                throw new ArgumentOutOfRangeException(nameof(height), "TrackMonitor height must be 250 or more");
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            scaling = Window.Owner.DpiScaling;
            iconSize = (int)(iconSize * scaling);
            aspectSize = (int)(aspectSize * scaling);
            trackItemOffset = (int)(trackItemOffset * scaling);
            signalOffset = (int)(signalOffset * scaling);
            milepostOffset = (int)(milepostOffset * scaling);
            speedpostOffset = (int)(speedpostOffset * scaling);
            trackLeftRail = new Point(trackItemOffset + (int)(7 * scaling), 0);
            trackRightRail = new Point(trackItemOffset + (int)(17 * scaling), 0);
            directionArrowOffset = (int)(directionArrowOffset * scaling);
            distanceMarkerOffset = (int)(distanceMarkerOffset * scaling);
            iconAdjustment = (int)(iconAdjustment * scaling);
            this.metric = metric;
            maxDistanceDisplay = Size.Length.FromM(maxDistance, metric); // in displayed units
            textRenderer = TextTextureResourceHolder.Instance(Window.Owner.Game);
            Window.OnWindowOpened += Window_OnWindowOpened;
            Window.OnWindowClosed += Window_OnWindowClosed;
        }

        private void Window_OnWindowClosed(object sender, EventArgs e)
        {
            textRenderer.Refresh -= TextRenderer_Refresh;
        }

        private void Window_OnWindowOpened(object sender, EventArgs e)
        {
            trainSymbolPositionChange = true;
            textRenderer.Refresh += TextRenderer_Refresh;
        }

        private void TextRenderer_Refresh(object sender, EventArgs e)
        {
            trainSymbolPositionChange = true;
        }

        internal override void Initialize()
        {
            symbolTexture = TextureManager.GetTextureStatic(System.IO.Path.Combine(RuntimeInfo.ContentFolder, "TrackMonitorImages.png"), Window.Owner.Game);
            signalTexture = TextureManager.GetTextureStatic(System.IO.Path.Combine(RuntimeInfo.ContentFolder, "SignalAspects.png"), Window.Owner.Game);

            base.Initialize();
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);

            trainSymbolPositionChange |= controlMode != (controlMode = PositionMode);

            if (trainSymbolPositionChange)
            {
                trainPositionOffset = PositionMode switch
                {
                    TrainPositionMode.ForwardAuto or TrainPositionMode.ForwardMultiPlayer => Bounds.Height - (int)(iconSize * 1.5),
                    TrainPositionMode.BackwardMultiPlayer => (int)(iconSize * 0.5),
                    _ => Bounds.Height / 2 - iconSize / 2,
                };

                #region right hand side distance markers
                distanceMarkers.Clear();
                int numberMarkers = controlMode is TrainPositionMode.BothWaysManual or TrainPositionMode.NeutralMultiPlayer ? 3 : 5;

                distanceFactor = ((PositionMode == TrainPositionMode.BackwardMultiPlayer ? (Bounds.Height - (int)(iconSize * 1.5)) : trainPositionOffset) - 18 * scaling) / maxDistance;
                markerInterval = Size.Length.ToM((maxDistanceDisplay / numberMarkers) switch
                {
                    <= 0.6 => 0.5,
                    <= 1.1 => 1.0,
                    _ => 1.5,
                }, metric);

                double distance = markerInterval;

                while (distance < maxDistance)
                {
                    int itemOffset = (int)(distance * distanceFactor);
                    Texture2D distanceTexture = textRenderer.PrepareResource(FormatStrings.FormatDistanceDisplay(distance, metric), Window.Owner.TextFontSmall);
                    if (controlMode != TrainPositionMode.BackwardMultiPlayer)
                        distanceMarkers.Add((distanceTexture, new Point(distanceMarkerOffset, trainPositionOffset - itemOffset - distanceTexture.Height)));
                    if (controlMode is TrainPositionMode.BothWaysManual or TrainPositionMode.BackwardMultiPlayer or TrainPositionMode.NeutralMultiPlayer)
                        distanceMarkers.Add((distanceTexture, new Point(distanceMarkerOffset, trainPositionOffset + iconSize + itemOffset)));
                    distance += markerInterval;
                }
                #endregion

                trainSymbolPositionChange = false;
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            offset += Bounds.Location;

            DrawTrack(spriteBatch, offset);
            DrawEyeLooking(spriteBatch, offset);

            DrawTrainPosition(spriteBatch, offset);
            DrawDirectionArrow(spriteBatch, offset);

            foreach ((Texture2D texture, Point position) in distanceMarkers)
            {
                spriteBatch.Draw(texture, (offset + position).ToVector2(), Color.White);
            }

            if (PositionMode != TrainPositionMode.BackwardMultiPlayer)
                DrawPathItems(spriteBatch, offset, ForwardItems);
            if (PositionMode != TrainPositionMode.ForwardMultiPlayer)
                DrawPathItems(spriteBatch, offset, BackwardItems);
            DrawTrain(spriteBatch, offset);
            base.Draw(spriteBatch, offset);
        }

        protected override void Dispose(bool disposing)
        {
            textureHolder?.Dispose();
            symbolTexture?.Dispose();
            signalTexture?.Dispose();
            base.Dispose(disposing);
        }

        private void DrawTrack(SpriteBatch spriteBatch, Point offset)
        {
            // overall track is 24 wide (size of the pictograms)
            Window.Owner.BasicShapes.DrawLine(scaling, SpeedingColor, (offset + trackLeftRail).ToVector2(), Bounds.Height, MathHelper.PiOver2, spriteBatch);
            Window.Owner.BasicShapes.DrawLine(scaling, SpeedingColor, (offset + trackRightRail).ToVector2(), Bounds.Height, MathHelper.PiOver2, spriteBatch);
        }

        private void DrawEyeLooking(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackItemOffset, offset.Y + (CabOrientation == Direction.Backward ? Bounds.Height - iconSize / 4 * 3 : -iconSize / 4), iconSize, iconSize), symbols[Symbols.Eye], Color.White);
        }

        private void DrawDirectionArrow(SpriteBatch spriteBatch, Point offset)
        {
            switch (TrainDirection)
            {
                case MidpointDirection.Forward:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + directionArrowOffset, offset.Y + trainPositionOffset, iconSize, iconSize), symbols[Symbols.ArrowForward], Color.White);
                    break;
                case MidpointDirection.Reverse:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + directionArrowOffset, offset.Y + trainPositionOffset, iconSize, iconSize), symbols[Symbols.ArrowBackward], Color.White);
                    break;
            }
        }

        private void DrawTrainPosition(SpriteBatch spriteBatch, Point offset)
        {
            switch (PositionMode)
            {
                case TrainPositionMode.ForwardAuto:
                    Window.Owner.BasicShapes.DrawLine(scaling, Color.OrangeRed, (offset + new Point(0, trainPositionOffset + iconSize)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    Window.Owner.BasicShapes.DrawLine(scaling, Color.DarkGray, (offset + new Point(0, trainPositionOffset)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    break;
                case TrainPositionMode.BothWaysManual:
                    Window.Owner.BasicShapes.DrawLine(scaling, Color.DarkGray, (offset + new Point(0, trainPositionOffset + iconSize)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    Window.Owner.BasicShapes.DrawLine(scaling, Color.DarkGray, (offset + new Point(0, trainPositionOffset)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    break;
                default:
                    break;
            }
        }

        private void DrawTrain(SpriteBatch spriteBatch, Point offset)
        {
            Symbols symbol = controlMode switch
            {
                TrainPositionMode.ForwardAuto => Symbols.TrainForwardAuto,
                TrainPositionMode.ForwardMultiPlayer => Symbols.TrainForwardAuto,
                TrainPositionMode.BackwardMultiPlayer => Symbols.TrainBackwardAuto,
                TrainPositionMode.NeutralMultiPlayer => Symbols.TrainOnRouteManual,
                TrainPositionMode.BothWaysManual => TrainOnRoute ? Symbols.TrainOnRouteManual : Symbols.TrainOffRouteManual,
                _ => Symbols.TrainOffRouteManual,
            };
            spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackItemOffset, offset.Y + trainPositionOffset, iconSize, iconSize), symbols[symbol], Color.White);
        }

        //TODO 20221019 this is interims code until TrainPathItems are refactored
        private void DrawPathItems(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            nearestItem = int.MaxValue;

            DrawMileposts(spriteBatch, offset, container);
            DrawPlatforms(spriteBatch, offset, container);
            DrawReversals(spriteBatch, offset, container);
            DrawAuthorities(spriteBatch, offset, container);
            DrawWaitingPoints(spriteBatch, offset, container);
            DrawSpeedposts(spriteBatch, offset, container);
            DrawSwitches(spriteBatch, offset, container);
            DrawSignals(spriteBatch, offset, container);

            if (nearestItem < (markerInterval * 0.9) || (nearestItem > maxDistance && nearestItem < int.MaxValue))
                DrawNearestItemDistanceMarker(spriteBatch, offset, nearestItem, container.Direction);
        }

        private void DrawSwitches(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, bool rightHand) in container.Switches)
            {
                if (distance < nearestItem)
                    nearestItem = (int)distance;

                bool rightHandSymbol = rightHand ^ container.Direction == Direction.Backward;
                int distanceOffset = (int)(distance * distanceFactor) - iconAdjustment;
                int positionOffset = rightHandSymbol ? trackLeftRail.X : trackRightRail.X - iconSize;
                DrawTexture(spriteBatch, offset, positionOffset, distanceOffset, iconSize, iconSize, symbolTexture, rightHandSymbol ? symbols[Symbols.ArrowRight] : symbols[Symbols.ArrowLeft], container.Direction);
            }
        }

        private void DrawSignals(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            container.Signals.Sort(delegate ((TrackMonitorSignalAspect aspect, float distance, float speedLimit) x, (TrackMonitorSignalAspect aspect, float distance, float speedLimit) y)
            {
                return y.distance.CompareTo(x.distance);
            });
            foreach ((TrackMonitorSignalAspect aspect, float distance, float speedLimit) in container.Signals)
            {
                if (distance < nearestItem)
                    nearestItem = (int)distance;
                if (aspect != TrackMonitorSignalAspect.Stop && speedLimit > 0)
                {
                    int distanceOffset = (int)(distance * distanceFactor);
                    Texture2D limitTexture = textRenderer.PrepareResource(FormatStrings.FormatSpeedLimitNoUoM(speedLimit, metric), Window.Owner.TextFontSmall);
                    DrawTexture(spriteBatch, offset, speedpostOffset, distanceOffset, limitTexture, container.Direction, Color.White);
                }
                if (distance < maxDistance)
                {
                    int distanceOffset = (int)(distance * distanceFactor);
                    DrawTexture(spriteBatch, offset, signalOffset, distanceOffset, aspectSize, aspectSize, signalTexture, signalAspects[aspect], container.Direction);
                }
            }
        }

        private void DrawPlatforms(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, int length) in container.Platforms)
            {
                int platformLengthOffset = (int)(Math.Min(iconSize, 2 * length * distanceFactor));
                int itemOffset = (int)(distance * distanceFactor) + (container.Direction == Direction.Forward ? -platformLengthOffset : +platformLengthOffset);
                DrawTexture(spriteBatch, offset, trackItemOffset, itemOffset, iconSize, platformLengthOffset, symbolTexture, symbols[Symbols.Station], container.Direction);
            }
        }

        private void DrawMileposts(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, float milepost) in container.Mileposts)
            {
                if (distance < maxDistance)
                {
                    int distanceOffset = (int)(distance * distanceFactor);
                    Texture2D distanceTexture = textRenderer.PrepareResource($"{milepost}", Window.Owner.TextFontSmall);
                    DrawTexture(spriteBatch, offset, milepostOffset, distanceOffset, distanceTexture, container.Direction);
                }
            }
        }

        private void DrawSpeedposts(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, double limit, Color color) in container.Speedposts)
            {
                if (distance < maxDistance)
                {
                    if (distance < nearestItem)
                        nearestItem = (int)distance;
                    int distanceOffset = (int)(distance * distanceFactor);
                    Texture2D limitTexture = textRenderer.PrepareResource(FormatStrings.FormatSpeedLimitNoUoM(limit, metric), Window.Owner.TextFontSmall);
                    DrawTexture(spriteBatch, offset, speedpostOffset, distanceOffset, limitTexture, container.Direction, color);
                }
            }
        }

        private void DrawAuthorities(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, bool? otherTrain) in container.Authorities)
            {
                if (otherTrain.HasValue && distance < maxDistance)
                {
                    if (distance < nearestItem)
                        nearestItem = (int)distance;
                    if (otherTrain.Value)
                    {
                        Rectangle source = container.Direction == Direction.Forward ? symbols[Symbols.OppositeTrainForward] : symbols[Symbols.OppositeTrainBackward];
                        int distanceOffset = (int)(distance * distanceFactor);
                        DrawTexture(spriteBatch, offset, trackItemOffset, distanceOffset, iconSize, iconSize, symbolTexture, source, container.Direction);
                    }
                    else
                    {
                        int distanceOffset = (int)(distance * distanceFactor) - iconAdjustment;
                        DrawTexture(spriteBatch, offset, trackItemOffset, distanceOffset, iconSize, iconSize, symbolTexture, symbols[Symbols.EndOfAuthority], container.Direction);
                    }
                }
            }
        }

        private void DrawReversals(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, bool valid, bool enabled) in container.Reversals)
            {
                if (distance < maxDistance)
                {
                    if (distance < nearestItem)
                        nearestItem = (int)distance;

                    if (valid)
                    {
                        int distanceOffset = (int)(distance * distanceFactor);
                        DrawTexture(spriteBatch, offset, trackItemOffset, distanceOffset, iconSize, iconSize, symbolTexture, symbols[Symbols.Reversal], container.Direction, enabled ? Color.LightGreen : null);
                    }
                    else
                    {
                        int distanceOffset = (int)(distance * distanceFactor) - iconAdjustment;
                        DrawTexture(spriteBatch, offset, trackItemOffset, distanceOffset, iconSize, iconSize, symbolTexture, symbols[Symbols.Invalid], container.Direction);
                    }
                }
            }
        }

        private void DrawWaitingPoints(SpriteBatch spriteBatch, Point offset, TrackItemsContainer container)
        {
            foreach ((float distance, bool enabled) in container.WaitingPoints)
            {
                if (distance < maxDistance)
                {
                    if (distance < nearestItem)
                        nearestItem = (int)distance;

                    int distanceOffset = (int)(distance * distanceFactor);
                    DrawTexture(spriteBatch, offset, trackItemOffset, distanceOffset, iconSize, iconSize, symbolTexture, symbols[Symbols.WaitingPoint], container.Direction, enabled ? Color.Yellow : Color.Red);
                }
            }
        }

        private void DrawNearestItemDistanceMarker(SpriteBatch spriteBatch, in Point offset, int distance, Direction direction)
        {
            Texture2D distanceTexture = textRenderer.PrepareResource(FormatStrings.FormatDistanceDisplay(distance, metric), Window.Owner.TextFontSmall);
            int distanceOffset = (int)(Math.Clamp(distance, 0, maxDistance * 1.1) * distanceFactor);
            DrawTexture(spriteBatch, offset, distanceMarkerOffset, distanceOffset, distanceTexture, direction);
        }

        private void DrawTexture(SpriteBatch spriteBatch, in Point offset, int x, int y, Texture2D texture, Direction direction, Color? color = null)
        {
            if (direction == Direction.Forward)
                spriteBatch.Draw(texture, (offset + new Point(x, trainPositionOffset - y - texture.Height)).ToVector2(), color ?? Color.White);
            else
                spriteBatch.Draw(texture, (offset + new Point(x, trainPositionOffset + iconSize + y)).ToVector2(), color ?? Color.White);
        }

        private void DrawTexture(SpriteBatch spriteBatch, in Point offset, int x, int y, int targetSizeX, int targetSizeY, Texture2D source, Rectangle sourceArea, Direction direction, Color? color = null)
        {
            if (direction == Direction.Forward)
                spriteBatch.Draw(source, new Rectangle(offset.X + x, offset.Y + trainPositionOffset - y - targetSizeY, targetSizeX, targetSizeY), sourceArea, color ?? Color.White);
            else
                spriteBatch.Draw(source, new Rectangle(offset.X + x, offset.Y + trainPositionOffset + iconSize + y, targetSizeX, targetSizeY), sourceArea, color ?? Color.White);
        }

    }
}
