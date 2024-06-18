using System;
using System.Collections.Generic;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Physics;
using Orts.Simulation.Timetables;
using Orts.Simulation.Track;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class NextStationWindow : WindowBase
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
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
#pragma warning restore CA2213 // Disposable fields should be disposed

        public NextStationWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) :
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Next Station"), relativeLocation, new Point(520, 130), catalog)
        {
            CloseButton = false;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            layout = base.Layout(layout, headerScaling);

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
                currentDelay.Text = playerTrain?.Delay != null && playerTrain.Delay.Value.TotalSeconds > 10 
                    ? Catalog.GetString($"Current Delay: {FormatStrings.FormatDelayTime(playerTrain.Delay.Value)} " + Catalog.GetPluralString("minute", "minutes", (long)playerTrain.Delay.Value.TotalMinutes))
                    : string.Empty;

                // timetable information
                if (playerTrain.CheckStations)
                {
                    TTTrain timetableTrain = playerTrain as TTTrain;

                    if (timetableTrain.ControlMode == TrainControlMode.Inactive || timetableTrain.MovementState == AiMovementState.Static)
                    {
                        // no info available
                        UpdatePreviousStop(null);
                        UpdateCurrentStop(null);
                        UpdateNextStop(null);

                        bool validMessage = false;

                        if (timetableTrain.NeedAttach != null && timetableTrain.NeedAttach.TryGetValue(-1, out List<int> attachTrains))
                        {
                            TTTrain otherTrain = timetableTrain.GetOtherTTTrainByNumber(attachTrains[0]);
                            if (otherTrain == null && Simulator.Instance.AutoGenDictionary.TryGetValue(attachTrains[0], out Simulation.AIs.AITrain aiTrain))
                            {
                                otherTrain = aiTrain as TTTrain;
                            }

                            if (otherTrain == null)
                            {
                                message.Text = Catalog.GetString("Waiting for train to attach");
                                message.TextColor = Color.Orange;
                                validMessage = true;
                            }
                            else
                            {
                                message.Text = $"{Catalog.GetString("Waiting for train to attach : ")}{otherTrain.Name}";
                                message.TextColor = Color.Orange;
                                validMessage = true;
                            }
                        }

                        if (!validMessage && timetableTrain.NeedTrainTransfer.Count > 0)
                        {
                            foreach (TrackCircuitSection occSection in timetableTrain.OccupiedTrack)
                            {
                                if (timetableTrain.NeedTrainTransfer.ContainsKey(occSection.Index))
                                {

                                    message.Text = Catalog.GetString("Waiting for transfer");
                                    message.TextColor = Color.Orange;
                                    break;
                                }
                            }
                        }

                        if (!validMessage)
                        {
                            message.TextColor = Color.White;

                            if (timetableTrain.ActivateTime.HasValue)
                            {
                                message.Text = timetableTrain.ControlMode == TrainControlMode.Inactive
                                    ? Catalog.GetString("Train inactive.")
                                    : timetableTrain.MovementState == AiMovementState.Static
                                        ? Catalog.GetString("Train static.")
                                        : Catalog.GetString("Train not active.");

                                // set activation message or time
                                if (timetableTrain.TriggeredActivationRequired)
                                {
                                    message.Text += Catalog.GetString(" Activated by other train.");
                                }
                                else
                                {
                                    message.Text += $"{message.Text}{Catalog.GetString(" Activation time : ")}{TimeSpan.FromSeconds(timetableTrain.ActivateTime.Value)}";
                                }
                            }
                            else
                            {
                                message.Text = Catalog.GetString("Train has terminated.");
                            }
                        }
                    }
                    else
                    {
                        // previous stop
                        if (timetableTrain.PreviousStop == null)
                        {
                            UpdatePreviousStop(null);
                        }
                        else
                        {
                            previousStationName.Text = timetableTrain.PreviousStop.PlatformItem.Name;
                            TimeSpan arrival = TimeSpan.FromSeconds(timetableTrain.PreviousStop.ArrivalTime);
                            previousStationArriveScheduled.Text = arrival.ToString("c");
                            if (timetableTrain.PreviousStop.ActualArrival >= 0)
                            {
                                TimeSpan actualArrival = TimeSpan.FromSeconds(timetableTrain.PreviousStop.ActualArrival);
                                previousStationArriveActual.Text = actualArrival.ToString("c");
                                previousStationArriveActual.TextColor = ColorCoding.ArrivalColor(arrival, actualArrival);
                                TimeSpan actualDeparture = TimeSpan.FromSeconds(timetableTrain.PreviousStop.ActualDepart);
                                previousStationDepartActual.Text = actualDeparture.ToString("c");
                                previousStationDepartActual.TextColor = ColorCoding.DepartureColor(arrival, actualDeparture);
                            }
                            else
                            {
                                previousStationArriveActual.Text = Catalog.GetString("(missed)");
                                previousStationArriveActual.TextColor = Color.LightSalmon;
                                previousStationDepartActual.Text = null;
                            }
                            previousStationDepartScheduled.Text = TimeSpan.FromSeconds(timetableTrain.PreviousStop.DepartTime).ToString("c");
                            previousStationDistance.Text = null;
                        }

                        if (timetableTrain.StationStops == null || timetableTrain.StationStops.Count == 0)
                        {
                            UpdateCurrentStop(null);
                            UpdateNextStop(null);

                            message.Text = Catalog.GetString("No more stations.");
                            message.TextColor = Color.White;
                        }
                        else
                        {
                            TimeSpan arrival = TimeSpan.FromSeconds(timetableTrain.StationStops[0].ArrivalTime);
                            currentStationName.Text = timetableTrain.StationStops[0].PlatformItem.Name;
                            currentStationArriveScheduled.Text = arrival.ToString("c");
                            if (timetableTrain.StationStops[0].ActualArrival >= 0)
                            {
                                TimeSpan actualArrival = TimeSpan.FromSeconds(timetableTrain.StationStops[0].ActualArrival);
                                currentStationArriveActual.Text = actualArrival.ToString("c");
                                currentStationArriveActual.TextColor = ColorCoding.ArrivalColor(arrival, actualArrival);

                            }
                            else
                            {
                                currentStationArriveActual.Text = null;
                            }
                            currentStationDepartScheduled.Text = TimeSpan.FromSeconds(timetableTrain.StationStops[0].DepartTime).ToString("c");
                            currentStationDistance.Text = FormatStrings.FormatDistanceDisplay(timetableTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                            message.Text = timetableTrain.DisplayMessage;
                            message.TextColor = timetableTrain.DisplayColor;

                            if (timetableTrain.StationStops.Count >= 2)
                            {
                                nextStationName.Text = timetableTrain.StationStops[1].PlatformItem.Name;
                                nextStationArriveScheduled.Text = TimeSpan.FromSeconds(timetableTrain.StationStops[1].ArrivalTime).ToString("c");
                                nextStationDepartScheduled.Text = TimeSpan.FromSeconds(timetableTrain.StationStops[1].DepartTime).ToString("c");
                                nextStationDistance.Text = null;
                            }
                            else
                            {
                                UpdateNextStop(null);
                            }
                        }

                        // check transfer details
                        bool transferValid = false;
                        string transferMessage = string.Empty;

                        if (timetableTrain.TransferStationDetails?.Count > 0 && timetableTrain.StationStops?.Count > 0)
                        {
                            if (timetableTrain.TransferStationDetails.TryGetValue(timetableTrain.StationStops[0].PlatformReference, out TransferInfo transfer))
                            {
                                transferMessage = Catalog.GetString($"Transfer units at next station with train {transfer.TrainName}");
                                transferValid = true;
                            }
                        }
                        else if (timetableTrain.TransferTrainDetails?.Count > 0)
                        {
                            foreach (KeyValuePair<int, List<TransferInfo>> transferDetails in timetableTrain.TransferTrainDetails)
                            {
                                TransferInfo transfer = transferDetails.Value[0];
                                transferMessage = Catalog.GetString($"Transfer units with train {transfer.TrainName}");
                                transferValid = true;
                                break;  // only show first
                            }
                        }

                        // attach details
                        if (timetableTrain.AttachDetails != null)
                        {
                            bool attachDetailsValid = false;

                            // attach is not at station - details valid
                            if (timetableTrain.AttachDetails.StationPlatformReference < 0)
                            {
                                attachDetailsValid = true;
                            }
                            // no further stations - details valid
                            if (timetableTrain.StationStops == null || timetableTrain.StationStops.Count == 0)
                            {
                                attachDetailsValid = true;
                            }
                            // attach is at next station - details valid
                            else if (timetableTrain.AttachDetails.StationPlatformReference == timetableTrain.StationStops[0].PlatformReference)
                            {
                                attachDetailsValid = true;
                            }

                            if (attachDetailsValid)
                            {
                                if (timetableTrain.AttachDetails.Valid)
                                {
                                    message.Text = Catalog.GetString($"Train is to attach to : {timetableTrain.AttachDetails.TrainName}");
                                    message.TextColor = Color.Orange;
                                }
                                else
                                {
                                    message.Text = Catalog.GetString($"Train is to attach to : {timetableTrain.AttachDetails.TrainName}; other train not yet ready");
                                    message.TextColor = Color.Orange;
                                }
                            }
                        }
                        // general details
                        else if (timetableTrain.PickUpStaticOnForms)
                        {
                            message.Text = Catalog.GetString("Train is to pickup train at end of path");
                            message.TextColor = Color.Orange;
                        }
                        else if (timetableTrain.NeedPickUp)
                        {
                            message.Text = Catalog.GetString("Pick up train ahead");
                            message.TextColor = Color.Orange;
                        }
                        else if (transferValid)
                        {
                            message.Text = transferMessage;
                            message.TextColor = Color.Orange;
                        }
                        else if (timetableTrain.NeedTransfer)
                        {
                            message.Text = Catalog.GetString("Transfer units with train ahead");
                            message.TextColor = Color.Orange;
                        }
                    }
                }
                // activity mode - switched train
                else if (!Simulator.Instance.TimetableMode && playerTrain != Simulator.Instance.OriginalPlayerTrain)
                {
                    // train name
                    stationPlatform.Text = playerTrain.Name;
                    if (playerTrain.ControlMode == TrainControlMode.Inactive)
                    {
                        // no info available
                        UpdatePreviousStop(null);
                        UpdateCurrentStop(null);
                        UpdateNextStop(null);

                        message.Text = Catalog.GetString("Train not active.");
                        message.TextColor = Color.White;

                        if (playerTrain is TTTrain timetableTrain && timetableTrain.ActivateTime.HasValue)
                        {
                            message.Text += $"{Catalog.GetString(" Activation time : ")}{TimeSpan.FromSeconds(timetableTrain.ActivateTime.Value):c}";
                        }
                    }
                    else
                    {
                        // previous stop
                        if (playerTrain.PreviousStop == null)
                        {
                            UpdatePreviousStop(null);
                        }
                        else
                        {
                            previousStationName.Text = playerTrain.PreviousStop.PlatformItem.Name;
                            TimeSpan arrival = TimeSpan.FromSeconds(playerTrain.PreviousStop.ArrivalTime);
                            previousStationArriveScheduled.Text = arrival.ToString("c");
                            if (playerTrain.PreviousStop.ActualArrival >= 0)
                            {
                                TimeSpan actualArrival = TimeSpan.FromSeconds(playerTrain.PreviousStop.ActualArrival);
                                previousStationArriveActual.Text = actualArrival.ToString("c");
                                previousStationArriveActual.TextColor = ColorCoding.ArrivalColor(arrival, actualArrival);
                                TimeSpan actualDeparture = TimeSpan.FromSeconds(playerTrain.PreviousStop.ActualDepart);
                                previousStationDepartActual.Text = actualDeparture.ToString("c");
                                previousStationDepartActual.TextColor = ColorCoding.DepartureColor(arrival, actualDeparture);
                            }
                            else
                            {
                                previousStationArriveActual.Text = Catalog.GetString("(missed)");
                                previousStationArriveActual.TextColor = Color.LightSalmon;
                                previousStationDepartActual.Text = null;
                            }
                            previousStationDepartScheduled.Text = TimeSpan.FromSeconds(playerTrain.PreviousStop.DepartTime).ToString("c");
                            previousStationDepartScheduled.Text = null;
                        }

                        if (playerTrain.StationStops == null || playerTrain.StationStops.Count == 0)
                        {
                            UpdateCurrentStop(null);
                            UpdateNextStop(null);
                            message.Text = Catalog.GetString("No more stations.");
                        }
                        else
                        {
                            currentStationName.Text = playerTrain.StationStops[0].PlatformItem.Name;
                            TimeSpan arrival = TimeSpan.FromSeconds(playerTrain.StationStops[0].ArrivalTime);
                            currentStationArriveScheduled.Text = arrival.ToString("c");
                            if (playerTrain.StationStops[0].ActualArrival >= 0)
                            {
                                TimeSpan actualArrival = TimeSpan.FromSeconds(playerTrain.StationStops[0].ActualArrival);
                                currentStationArriveActual.Text = actualArrival.ToString("c");
                                currentStationArriveActual.TextColor = ColorCoding.ArrivalColor(arrival, actualArrival);

                            }
                            else
                            {
                                currentStationArriveActual.Text = null;
                            }
                            currentStationDepartScheduled.Text = TimeSpan.FromSeconds(playerTrain.StationStops[0].DepartTime).ToString("c");
                            currentStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                            message.Text = playerTrain.DisplayMessage;
                            message.TextColor = playerTrain.DisplayColor;

                            if (playerTrain.StationStops.Count >= 2)
                            {
                                nextStationName.Text = playerTrain.StationStops[1].PlatformItem.Name;
                                nextStationArriveScheduled.Text = TimeSpan.FromSeconds(playerTrain.StationStops[1].ArrivalTime).ToString("c");
                                nextStationDepartScheduled.Text = TimeSpan.FromSeconds(playerTrain.StationStops[1].DepartTime).ToString("c");
                                nextStationDistance.Text = null;
                            }
                            else
                            {
                                UpdateNextStop(null);
                            }
                        }
                    }
                }
                // activity information
                else if (Simulator.Instance.ActivityRun != null && playerTrain == Simulator.Instance.OriginalPlayerTrain)
                {
                    ActivityTaskPassengerStopAt currentStop = Simulator.Instance.ActivityRun.ActivityTask == null ? Simulator.Instance.ActivityRun.Last as ActivityTaskPassengerStopAt : Simulator.Instance.ActivityRun.ActivityTask as ActivityTaskPassengerStopAt;

                    UpdatePreviousStop(currentStop?.PrevTask as ActivityTaskPassengerStopAt);
                    previousStationDistance.Text = null;
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, previousStationName.Text, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && playerTrain.StationStops[0].DistanceToTrainM < float.MaxValue)
                    {
                        previousStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                    }

                    UpdateCurrentStop(currentStop);
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, currentStop.PlatformEnd1.Station, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && playerTrain.StationStops[0].DistanceToTrainM < float.MaxValue)
                    {
                        currentStationDistance.Text = FormatStrings.FormatDistanceDisplay(playerTrain.StationStops[0].DistanceToTrainM, Simulator.Instance.Route.MilepostUnitsMetric);
                    }
                    else
                        currentStationDistance.Text = null;

                    UpdateNextStop(currentStop?.NextTask as ActivityTaskPassengerStopAt);
                    nextStationDistance.Text = null;
                    if (playerTrain.StationStops.Count > 0 && string.Equals(playerTrain.StationStops[0].PlatformItem?.Name, nextStationName.Text, StringComparison.OrdinalIgnoreCase) &&
                        playerTrain.StationStops[0].DistanceToTrainM > 0 && playerTrain.StationStops[0].DistanceToTrainM < float.MaxValue)
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
                previousStationArriveActual.TextColor = ColorCoding.ArrivalColor(previousStop.ScheduledArrival, previousStop.ActualArrival);
                previousStationDepartScheduled.Text = previousStop.ScheduledDeparture.ToString("c");
                previousStationDepartActual.Text = previousStop.ActualDeparture?.ToString("c") ?? Catalog.GetString("(missed)");
                previousStationDepartActual.TextColor = ColorCoding.DepartureColor(previousStop.ScheduledDeparture, previousStop.ActualDeparture);
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
                currentStationArriveActual.TextColor = ColorCoding.ArrivalColor(currentStop.ScheduledArrival, currentStop.ActualArrival);
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
    }
}
