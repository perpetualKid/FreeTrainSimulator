using System;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Graphics.Xna;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class TrackMonitorWindow : WindowBase
    {
        private readonly Viewer viewer;
        private Label speedCurrentLabel;
        private Label speedProjectedLabel;
        private Label speedLimitLabel;
        private Label gradientLabel;
        private Label controlModeLabel;
        private TrackMonitorControl trackMonitor;


        public TrackMonitorWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Monitor"), relativeLocation, new Point(160, 320), catalog)
        {
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

            int columnWidth = layout.RemainingWidth / 2;
            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Speed")));
            line.Add(speedCurrentLabel = new Label(this, columnWidth, Owner.TextFontDefault.Height, null, HorizontalAlignment.Right));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Projected")));
            line.Add(speedProjectedLabel = new Label(this, columnWidth, Owner.TextFontDefault.Height, null, HorizontalAlignment.Right));
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth, Owner.TextFontDefault.Height, Catalog.GetString("Limit")));
            line.Add(speedLimitLabel = new Label(this, columnWidth, Owner.TextFontDefault.Height, null, HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(controlModeLabel = new Label(this, (int)(columnWidth / 5 * 7), Owner.TextFontDefault.Height, null));
            line.Add(gradientLabel = new Label(this, (int)(columnWidth / 5 * 3), Owner.TextFontDefault.Height, null, HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();
            layout.Add(trackMonitor = new TrackMonitorControl(this, layout.RemainingWidth, layout.RemainingHeight));
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                MSTSLocomotive playerLocomotive = Simulator.Instance.PlayerLocomotive;

                speedCurrentLabel.Text = $"{FormatStrings.FormatSpeedDisplay(playerLocomotive.SpeedMpS, Simulator.Instance.MetricUnits)}";
                speedCurrentLabel.TextColor = ColorCoding.SpeedingColor(playerLocomotive.AbsSpeedMpS, playerLocomotive.Train.MaxTrainSpeedAllowed);
                speedProjectedLabel.Text = $"{FormatStrings.FormatSpeedDisplay(playerLocomotive.Train.ProjectedSpeedMpS, Simulator.Instance.MetricUnits)}";
                speedLimitLabel.Text = $"{FormatStrings.FormatSpeedLimit(playerLocomotive.Train.MaxTrainSpeedAllowed, Simulator.Instance.MetricUnits)}";

                controlModeLabel.Text = playerLocomotive.Train.ControlMode.GetLocalizedDescription();
                double gradient = Math.Round(playerLocomotive.CurrentElevationPercent, 1);
                if (gradient == 0) // to avoid negative zero string output if gradient after rounding is -0.0
                    gradient = 0;
                gradientLabel.Text = $"{gradient:F1}% {(gradient > 0 ? FormatStrings.Markers.Ascent : gradient < 0 ? FormatStrings.Markers.Descent : string.Empty)}";
                gradientLabel.TextColor = (gradient > 0 ? Color.Yellow : gradient < 0 ? Color.LightSkyBlue : Color.White);

                trackMonitor.SpeedingColor = speedCurrentLabel.TextColor;
                trackMonitor.CabOrientation = playerLocomotive.Flipped ^ playerLocomotive.GetCabFlipped() ? Direction.Backward : Direction.Forward;
                trackMonitor.TrainDirection = playerLocomotive.Train.MUDirection;
                trackMonitor.TrainOnRoute = playerLocomotive.Train.TrainOnPath;
                trackMonitor.TrainControlMode = playerLocomotive.Train.ExtendedControlMode;
            }
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
        }
    }
}
