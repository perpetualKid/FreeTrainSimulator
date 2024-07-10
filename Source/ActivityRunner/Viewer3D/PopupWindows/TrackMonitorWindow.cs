using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Window.Controls.Layout;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Simulation;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal sealed class TrackMonitorWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Label speedCurrentLabel;
        private Label speedProjectedLabel;
        private Label speedLimitLabel;
        private Label gradientLabel;
        private Label controlModeLabel;
        private TrackMonitorControl trackMonitor;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public TrackMonitorWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Track Monitor"), relativeLocation, new Point(160, 360), catalog)
        {
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
            line.Add(controlModeLabel = new Label(this, (int)(columnWidth / 5.0 * 6), Owner.TextFontDefault.Height, null));
            line.Add(gradientLabel = new Label(this, (int)(columnWidth / 5.0 * 4), Owner.TextFontDefault.Height, null, HorizontalAlignment.Right));
            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            columnWidth = layout.RemainingWidth / 8;
            line.Add(new Label(this, (int)(columnWidth * 3.5), Owner.TextFontSmall.Height, Catalog.GetString("Milepost"), Owner.TextFontSmall));
            line.Add(new Label(this, (int)(columnWidth * 2), Owner.TextFontSmall.Height, Catalog.GetString("Limit"), Owner.TextFontSmall));
            line.Add(new Label(this, 0, 0, (int)(columnWidth * 2.5), Owner.TextFontSmall.Height, Catalog.GetString("Distance"), HorizontalAlignment.Right, Owner.TextFontSmall, Color.White));
            layout.AddHorizontalSeparator();
            layout.Add(trackMonitor = new TrackMonitorControl(this, layout.RemainingWidth, layout.RemainingHeight, Simulator.Instance.Route.MilepostUnitsMetric));
            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
            if (shouldUpdate)
            {
                MSTSLocomotive playerLocomotive = Simulator.Instance.PlayerLocomotive;

                speedCurrentLabel.Text = $"{FormatStrings.FormatSpeedDisplay(playerLocomotive.AbsSpeedMpS, Simulator.Instance.MetricUnits)}";
                speedCurrentLabel.TextColor = ColorCoding.SpeedingColor(playerLocomotive.AbsSpeedMpS, playerLocomotive.Train.MaxTrainSpeedAllowed);
                speedProjectedLabel.Text = $"{FormatStrings.FormatSpeedDisplay(Math.Abs(playerLocomotive.Train.ProjectedSpeedMpS), Simulator.Instance.MetricUnits)}";
                speedLimitLabel.Text = $"{FormatStrings.FormatSpeedLimit(playerLocomotive.Train.MaxTrainSpeedAllowed, Simulator.Instance.MetricUnits)}";

                TrainInfo current = playerLocomotive.Train.GetTrainInfo();

                controlModeLabel.Text = playerLocomotive.Train.ControlMode switch
                {
                    TrainControlMode.AutoNode => FormatStrings.JoinIfNotEmpty(':', playerLocomotive.Train.ControlMode.GetLocalizedDescription(), current.ObjectInfoForward.Where((item) => item.ItemType == TrainPathItemType.Authority).FirstOrDefault()?.AuthorityType.GetLocalizedDescription()),
                    TrainControlMode.OutOfControl => $"{playerLocomotive.Train.ControlMode.GetLocalizedDescription()}: {current.ObjectInfoForward.First().OutOfControlReason.GetLocalizedDescription()}",
                    _ => playerLocomotive.Train.ControlMode.GetLocalizedDescription(),
                };

                double gradient = Math.Round(playerLocomotive.CurrentElevationPercent, 1);
                if (gradient == 0) // to avoid negative zero string output if gradient after rounding is -0.0
                    gradient = 0;
                gradientLabel.Text = $"{gradient:F1}% {(gradient > 0 ? FormatStrings.Markers.Ascent : gradient < 0 ? FormatStrings.Markers.Descent : string.Empty)}";
                gradientLabel.TextColor = (gradient > 0 ? Color.Yellow : gradient < 0 ? Color.LightSkyBlue : Color.White);

                trackMonitor.SpeedingColor = speedCurrentLabel.TextColor;
                trackMonitor.CabOrientation = playerLocomotive.Flipped ^ playerLocomotive.GetCabFlipped() ? Direction.Backward : Direction.Forward;
                trackMonitor.TrainDirection = playerLocomotive.Train.MUDirection;
                trackMonitor.TrainOnRoute = playerLocomotive.Train.TrainOnPath;

                if (MultiPlayerManager.IsMultiPlayer())
                {
                    trackMonitor.PositionMode = playerLocomotive.Direction switch
                    {
                        MidpointDirection.Forward => TrackMonitorControl.TrainPositionMode.ForwardMultiPlayer,
                        MidpointDirection.Reverse => TrackMonitorControl.TrainPositionMode.BackwardMultiPlayer,
                        _ => TrackMonitorControl.TrainPositionMode.NeutralMultiPlayer,
                    };
                }
                else if (playerLocomotive.Train.ControlMode is TrainControlMode.AutoNode or TrainControlMode.AutoSignal)
                {
                    trackMonitor.PositionMode = (current.ObjectInfoBackward?.Count > 0 && current.ObjectInfoBackward[0].ItemType == TrainPathItemType.Authority &&
                        current.ObjectInfoBackward[0].AuthorityType == EndAuthorityType.NoPathReserved) ? TrackMonitorControl.TrainPositionMode.ForwardAuto : TrackMonitorControl.TrainPositionMode.BothWaysManual;
                }
                else if (playerLocomotive.Train.ControlMode == TrainControlMode.TurnTable)
                {
                    trackMonitor.PositionMode = TrackMonitorControl.TrainPositionMode.None;
                    return;
                }
                else
                {
                    trackMonitor.PositionMode = TrackMonitorControl.TrainPositionMode.BothWaysManual;
                }

                trackMonitor.BeginAddItems();
                AddTrainPathItems(Direction.Forward, current.ObjectInfoForward);
                AddTrainPathItems(Direction.Backward, current.ObjectInfoBackward);
                trackMonitor.EndAddItems();
            }
        }

        private void AddTrainPathItems(Direction direction, List<TrainPathItem> source)
        {
            foreach (TrainPathItem item in source)
            {
                if (item.ItemType == TrainPathItemType.Signal)
                {
                    trackMonitor.AddSignal(direction, (item.SignalState, item.DistanceToTrainM, item.AllowedSpeedMpS));
                }
                else if (item.ItemType == TrainPathItemType.Milepost)
                {
                    trackMonitor.AddMilepost(direction, (item.DistanceToTrainM, item.Miles));
                }
                else if (item.ItemType == TrainPathItemType.FacingSwitch)
                {
                    trackMonitor.AddSwitch(direction, (item.DistanceToTrainM, item.SwitchDivertsRight));
                }
                else if (item.ItemType == TrainPathItemType.Speedpost)
                {
                    Color color = item.SpeedObjectType == SpeedItemType.Standard ? (item.IsWarning ? Color.Yellow : Color.White) :
                    (item.SpeedObjectType == SpeedItemType.TemporaryRestrictionStart ? Color.Red : Color.LightGreen);
                    trackMonitor.AddSpeedpost(direction, (item.DistanceToTrainM, item.AllowedSpeedMpS > 200 ? Simulator.Instance.Route.SpeedLimit : item.AllowedSpeedMpS, color));
                }
                else if (item.ItemType == TrainPathItemType.Station)
                {
                    trackMonitor.AddPlatform(direction, (item.DistanceToTrainM, item.StationPlatformLength));
                }
                else if (item.ItemType == TrainPathItemType.Authority)
                {
                    trackMonitor.AddAuthority(direction, (item.DistanceToTrainM, item.AuthorityType == EndAuthorityType.TrainAhead ? true : item.AuthorityType is EndAuthorityType.EndOfAuthority or EndAuthorityType.EndOfPath or EndAuthorityType.EndOfTrack or EndAuthorityType.ReservedSwitch or EndAuthorityType.Loop ? false : null));
                }
                else if (item.ItemType == TrainPathItemType.Reversal)
                {
                    trackMonitor.AddReversal(direction, (item.DistanceToTrainM, item.Valid, item.Enabled));
                }
                else if (item.ItemType == TrainPathItemType.WaitingPoint)
                {
                    trackMonitor.AddWaitingPoint(direction, (item.DistanceToTrainM, item.Enabled));
                }
            }
        }
    }
}
