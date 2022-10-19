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
            AutoForward,
            ManualBothWays,


        }
        private readonly TextTextureResourceHolder textureHolder;
        private Texture2D symbolTexture;
        private Texture2D signalTexture;
        private readonly float scaling;

        //precalculated values
        private readonly int trackOffset = 50;
        private readonly int arrowOffset = 32;
        private readonly int distanceMarkerOffset = 115;
        private readonly Point trackLeftRail;
        private readonly Point trackRightRail;
        private readonly int iconSize = 24;
        private readonly bool metric;
        private readonly double maxDistanceDisplay;
        private bool trainSymbolPositionChange;
        private TrainControlModeExtended controlMode;
        private int trainPositionOffset;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextTextureResourceHolder textRenderer;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly List<(Texture2D, Point)> distanceMarkers = new List<(Texture2D, Point)>();

        private EnumArray<Rectangle, Symbols> symbols = new EnumArray<Rectangle, Symbols>(new Rectangle[] {
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

        private EnumArray<Rectangle, TrackMonitorSignalAspect> signalAspects = new EnumArray<Rectangle, TrackMonitorSignalAspect>(new Rectangle[] {
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
        public TrainControlModeExtended TrainControlMode { get; set; }
        public bool TrainOnRoute { get; set; }
        public TrainPositionMode PositionMode { get; set; }


        public TrackMonitorControl(WindowBase window, int width, int height, bool metric) :
            base(window, 0, 0, width, height)
        {
            if ((width / Window.Owner.DpiScaling) < 150)
                throw new ArgumentOutOfRangeException(nameof(width), "TrackMonitor width must be 150 or more");
            if ((height / Window.Owner.DpiScaling) < 200)
                throw new ArgumentOutOfRangeException(nameof(height), "TrackMonitor height must be 250 or more");
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            scaling = Window.Owner.DpiScaling;
            iconSize = (int)(iconSize * scaling);
            trackOffset = (int)(trackOffset * scaling);
            trackLeftRail = new Point(trackOffset + (int)(7 * scaling), 0);
            trackRightRail = new Point(trackOffset + (int)(17 * scaling), 0);
            arrowOffset = (int)(arrowOffset * scaling);
            distanceMarkerOffset = (int)(distanceMarkerOffset * scaling);
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

            trainSymbolPositionChange |= controlMode != (controlMode = TrainControlMode);
            if (trainSymbolPositionChange)
            {
                trainPositionOffset = TrainControlMode == TrainControlModeExtended.Auto ? Bounds.Height - (int)(iconSize * 1.5) : Bounds.Height / 2 - iconSize / 2;

                #region right hand side distance markers
                distanceMarkers.Clear();
                int numberMarkers = controlMode == TrainControlModeExtended.Auto ? 5 : 3;

                double distanceFactor = (trainPositionOffset - 18 * scaling) / maxDistance;
                double markerInterval = Size.Length.ToM((maxDistanceDisplay / numberMarkers) switch
                {
                    <= 0.6 => 0.5,
                    <= 1.1 => 1.0,
                    _ => 1.5,
                }, metric);

                double distance = markerInterval;

                while (distance <= maxDistance)
                {
                    int itemOffset = Convert.ToInt32(distance * distanceFactor);
                    string distanceString = FormatStrings.FormatDistanceDisplay(distance, metric);
                    Texture2D distanceTexture = textRenderer.PrepareResource(distanceString, Window.Owner.TextFontSmall);
                    distanceMarkers.Add((distanceTexture, new Point(distanceMarkerOffset, trainPositionOffset - itemOffset - distanceTexture.Height)));
                    if (controlMode != TrainControlModeExtended.Auto)
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

            DrawTrainPosition(spriteBatch, offset, trainPositionOffset);
            DrawDirectionArrow(spriteBatch, offset, trainPositionOffset);
            DrawDistanceMarkers(spriteBatch, offset);
            DrawTrain(spriteBatch, offset, trainPositionOffset);
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
            BasicShapes.DrawLine(scaling, SpeedingColor, (offset + trackLeftRail).ToVector2(), Bounds.Height, MathHelper.PiOver2, spriteBatch);
            BasicShapes.DrawLine(scaling, SpeedingColor, (offset + trackRightRail).ToVector2(), Bounds.Height, MathHelper.PiOver2, spriteBatch);
        }

        private void DrawEyeLooking(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + (CabOrientation == Direction.Backward ? Bounds.Height - iconSize / 4 * 3 : -iconSize / 4), iconSize, iconSize), symbols[Symbols.Eye], Color.White);
        }

        private void DrawDirectionArrow(SpriteBatch spriteBatch, Point offset, int positionOffset)
        {
            switch (TrainDirection)
            {
                case MidpointDirection.Forward:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + arrowOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[Symbols.ArrowForward], Color.White);
                    break;
                case MidpointDirection.Reverse:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + arrowOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[Symbols.ArrowBackward], Color.White);
                    break;
            }
        }

        private void DrawTrainPosition(SpriteBatch spriteBatch, Point offset, int positionOffset)
        {
            switch (PositionMode)
            {
                case TrainPositionMode.AutoForward:
                    BasicShapes.DrawLine(scaling / 1f, Color.OrangeRed, (offset + new Point(0, positionOffset + iconSize)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    BasicShapes.DrawLine(scaling / 1f, Color.DarkGray, (offset + new Point(0, positionOffset)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    break;
                case TrainPositionMode.ManualBothWays:
                    BasicShapes.DrawLine(scaling / 1f, Color.DarkGray, (offset + new Point(0, positionOffset + iconSize)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    BasicShapes.DrawLine(scaling / 1f, Color.DarkGray, (offset + new Point(0, positionOffset)).ToVector2(), Bounds.Width, 0, spriteBatch);
                    break;
                default:
                    break;
            }
        }

        private void DrawTrain(SpriteBatch spriteBatch, Point offset, int positionOffset)
        {
            switch (TrainControlMode)
            {
                case TrainControlModeExtended.Auto:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[Symbols.TrainForwardAuto], Color.White);
                    break;
                case TrainControlModeExtended.MultiPlayer:
                    Symbols symbol = TrainDirection switch
                    {
                        MidpointDirection.Forward => Symbols.TrainForwardAuto,
                        MidpointDirection.Reverse => Symbols.TrainBackwardAuto,
                        _ => Symbols.TrainOnRouteManual,
                    };
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[symbol], Color.White);
                    break;
                case TrainControlModeExtended.Manual:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[TrainOnRoute ? Symbols.TrainOnRouteManual : Symbols.TrainOffRouteManual], Color.White);
                    break;
                case TrainControlModeExtended.Turntable:
                default:
                    spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + positionOffset, iconSize, iconSize), symbols[Symbols.TrainOffRouteManual], Color.White);
                    break;

            }
        }

        private void DrawDistanceMarkers(SpriteBatch spriteBatch, Point offset)
        {
            foreach ((Texture2D texture, Point position) in distanceMarkers)
            {
                spriteBatch.Draw(texture, (offset + position).ToVector2(), Color.White);
            }
        }
    }
}
