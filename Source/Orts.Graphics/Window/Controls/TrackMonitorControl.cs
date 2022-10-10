using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class TrackMonitorControl : WindowControl
    {
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

        private readonly TextTextureResourceHolder textureHolder;
        private Texture2D symbolTexture;
        private Texture2D signalTexture;
        private readonly float scaling;

        //precalculated values
        private readonly int trackOffset = 50;
        private readonly int arrowOffset = 32;
        private readonly Point trackLeftRail;
        private readonly Point trackRightRail;
        private readonly int iconSize = 24;

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


        public TrackMonitorControl(WindowBase window, int width, int height) :
            base(window, 0, 0, width, height)
        {
            if ((width / Window.Owner.DpiScaling) < 150)
                throw new ArgumentOutOfRangeException(nameof(width), "TrackMonitor width must be 150 or more");
            if ((height / Window.Owner.DpiScaling) < 200)
                throw new ArgumentOutOfRangeException(nameof(height), "TrackMonitor height must be 250 or more");
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game, 30);
            scaling = Window.Owner.DpiScaling;
            iconSize = (int)(iconSize * scaling);
            trackOffset = (int)(trackOffset * scaling);
            trackLeftRail = new Point(trackOffset + (int)(7 * scaling), 0);
            trackRightRail = new Point(trackOffset + (int)(17 * scaling), 0);
            arrowOffset = (int)(arrowOffset * scaling);
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
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            offset += Bounds.Location;

            DrawTrack(spriteBatch, offset);
            DrawEyeLooking(spriteBatch, offset);
            int trainPositionOffset = TrainControlMode == TrainControlModeExtended.Auto ? Bounds.Height - (int)(iconSize * 1.5) : Bounds.Height / 2 - iconSize / 2;

            DrawTrainPosition(spriteBatch, offset, trainPositionOffset);
            DrawDirectionArrow(spriteBatch, offset, trainPositionOffset);
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
            Color color = Color.DarkGray;
            if (TrainControlMode != TrainControlModeExtended.Auto)
                BasicShapes.DrawLine(scaling / 2f, color, (offset + new Point(0, positionOffset + iconSize)).ToVector2(), Bounds.Width, 0, spriteBatch);
            BasicShapes.DrawLine(scaling / 2f, color, (offset + new Point(0, positionOffset)).ToVector2(), Bounds.Width, 0, spriteBatch);

            spriteBatch.Draw(symbolTexture, new Rectangle(offset.X + trackOffset, offset.Y + positionOffset - iconSize *2, iconSize, iconSize), symbols[Symbols.Station], Color.White);
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
    }
}
