using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Physics;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class NextStationWindow : WindowBase
    {
        private Label stationPlatform;
        private Label currentDelay;
        private Label currentTime;
        private Label previousStationName;
        private Label previousStationDistance;
        private Label previousStationArriveScheduled;
        private Label previousStationArriveActual;
        private Label previousStationDepartScheduled;
        private Label previousStationDepartActual;
        private Label currentStationName;
        private Label currentStationDistance;
        private Label currentStationArriveScheduled;
        private Label currentStationArriveActual;
        private Label currentStationDepartScheduled;
        private Label nextStationName;
        private Label nextStationDistance;
        private Label nextStationArriveScheduled;
        private Label nextStationDepartScheduled;
        private Label message;

        public NextStationWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Next Station"), relativeLocation, new Point(520, 130), catalog)
        {
//            CloseButton = false;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling).AddLayoutVertical();

            int columnWidth = layout.RemainingWidth / 8;
            ControlLayout line = layout.AddLayoutHorizontalLineOfText();
            line.Add(stationPlatform = new Label(this, columnWidth * 3, line.RemainingHeight, string.Empty));
            line.Add(currentDelay = new Label(this, columnWidth * 4, line.RemainingHeight, string.Empty));
            line.Add(currentTime = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(new Label(this, columnWidth * 3, line.RemainingHeight, Catalog.GetString("Station")));
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Distance"), HorizontalAlignment.Center));
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Arrive"), HorizontalAlignment.Center));
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Actual"), HorizontalAlignment.Center));
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Depart"), HorizontalAlignment.Center));
            line.Add(new Label(this, columnWidth, line.RemainingHeight, Catalog.GetString("Actual"), HorizontalAlignment.Center));

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(previousStationName = new Label(this, columnWidth * 3, line.RemainingHeight, string.Empty));
            line.Add(previousStationDistance = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(previousStationArriveScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(previousStationArriveActual = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(previousStationDepartScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(previousStationDepartActual = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(currentStationName = new Label(this, columnWidth * 3, line.RemainingHeight, string.Empty));
            line.Add(currentStationDistance = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(currentStationArriveScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(currentStationArriveActual = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(currentStationDepartScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));

            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(nextStationName = new Label(this, columnWidth * 3, line.RemainingHeight, string.Empty));
            line.Add(nextStationDistance = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.Add(nextStationArriveScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));
            line.AddSpace(columnWidth, line.RemainingHeight);
            line.Add(nextStationDepartScheduled = new Label(this, columnWidth, line.RemainingHeight, string.Empty, HorizontalAlignment.Center));

            layout.AddHorizontalSeparator();
            line = layout.AddLayoutHorizontalLineOfText();
            line.Add(message = new Label(this, line.RemainingWidth, line.RemainingHeight, string.Empty));

            return layout;
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                Train playerTrain = Simulator.Instance.PlayerLocomotive.Train;
                currentTime.Text = FormatStrings.FormatTime(Simulator.Instance.ClockTime);
                currentDelay.Text = playerTrain?.Delay != null
                    ? Catalog.GetString($"Current Delay: {FormatStrings.FormatDelayTime(playerTrain.Delay.Value)} " + Catalog.GetPluralString("minute", "minutes", (long)playerTrain.Delay.Value.TotalMinutes))
                    : string.Empty;

                if (Simulator.Instance.ActivityRun != null && playerTrain == Simulator.Instance.OriginalPlayerTrain)
                {
                    ActivityTaskPassengerStopAt currentStop = Simulator.Instance.ActivityRun.ActivityTask == null ? Simulator.Instance.ActivityRun.Last as ActivityTaskPassengerStopAt : Simulator.Instance.ActivityRun.ActivityTask as ActivityTaskPassengerStopAt;

                    UpdatePreviousStop(currentStop?.PrevTask as ActivityTaskPassengerStopAt);
                    previousStationDistance.Text = null;
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, previousStationName.Text, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && !float.IsNaN(playerTrain.StationStops[0].DistanceToTrainM))
                    {
                        previousStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                    }

                    UpdateCurrentStop(currentStop);
                    currentStationDistance.Text = null;
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, currentStop.PlatformEnd1.Station, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && !float.IsNaN(playerTrain.StationStops[0].DistanceToTrainM))
                    {
                        currentStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                    }

                    UpdateNextStop(currentStop?.NextTask as ActivityTaskPassengerStopAt);
                    nextStationDistance.Text = null;
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, nextStationName.Text, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && !float.IsNaN(playerTrain.StationStops[0].DistanceToTrainM))
                    {
                        nextStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                    }

                    if (Simulator.Instance.ActivityRun.Completed)
                    {
                        message.Text = Catalog.GetString("Activity completed.");
                    }

                }
            }
            base.Update(gameTime, shouldUpdate);
        }

        private void UpdatePreviousStop(ActivityTaskPassengerStopAt previousStop)
        {
            if (previousStop != null)
            {
                previousStationName.Text = previousStop.PlatformEnd1.Station;
                previousStationArriveScheduled.Text = previousStop.ScheduledArrival.ToString("c");
                previousStationArriveActual.Text = previousStop.ActualArrival?.ToString("c") ?? Catalog.GetString("(missed)");
                previousStationArriveActual.TextColor = GetArrivalColor(previousStop.ScheduledArrival, previousStop.ActualArrival);
                previousStationDepartScheduled.Text = previousStop.ScheduledDeparture.ToString("c");
                previousStationDepartActual.Text = previousStop.ActualDeparture?.ToString("c") ?? Catalog.GetString("(missed)");
                previousStationDepartActual.TextColor = GetDepartColor(previousStop.ScheduledDeparture, previousStop.ActualDeparture);
            }
            else
            {
                previousStationName.Text = null;
                previousStationArriveScheduled.Text = null;
                previousStationArriveActual.Text = null;
                previousStationDepartScheduled.Text = null;
                previousStationDepartActual.Text = null;
                previousStationDistance.Text = null;

            }
        }

        private void UpdateCurrentStop(ActivityTaskPassengerStopAt currentStop)
        {
            if (currentStop != null)
            {
                stationPlatform.Text = currentStop.PlatformEnd1.ItemName;
                currentStationName.Text = currentStop.PlatformEnd1.Station;
                currentStationArriveScheduled.Text = currentStop.ScheduledArrival.ToString("c");
                currentStationArriveActual.Text = currentStop.ActualArrival?.ToString("c");
                currentStationArriveActual.TextColor = GetArrivalColor(currentStop.ScheduledArrival, currentStop.ActualArrival);
                currentStationDepartScheduled.Text = currentStop.ScheduledDeparture.ToString("c");
                message.TextColor = currentStop.DisplayColor;
                message.Text = currentStop.DisplayMessage;
            }
            else
            {
                stationPlatform.Text = null;
                currentStationName.Text = null;
                currentStationArriveScheduled.Text = null;
                currentStationArriveActual.Text = null;
                currentStationDepartScheduled.Text = null;
                message.Text = null;
            }
        }

        private void UpdateNextStop(ActivityTaskPassengerStopAt nextStop)
        {
            if (nextStop != null)
            {
                nextStationName.Text = nextStop.PlatformEnd1.Station;
                nextStationArriveScheduled.Text = nextStop.ScheduledArrival.ToString("c");
                nextStationDepartScheduled.Text = nextStop.ScheduledDeparture.ToString("c");
            }
            else
            {
                nextStationName.Text = null;
                nextStationArriveScheduled.Text = null;
                nextStationDepartScheduled.Text = null;
                nextStationDistance.Text = null;
            }
        }


        private static Color GetArrivalColor(TimeSpan expected, TimeSpan? actual)
        {
            return actual.HasValue && actual.Value <= expected ? Color.LightGreen : Color.LightSalmon;
        }

        private static Color GetDepartColor(TimeSpan expected, TimeSpan? actual)
        {
            return actual.HasValue && actual.Value >= expected ? Color.LightGreen : Color.LightSalmon;
        }

    }
}
