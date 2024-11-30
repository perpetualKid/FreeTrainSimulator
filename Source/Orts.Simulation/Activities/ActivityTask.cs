// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Models.Imported.State;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Activities
{
    public abstract class ActivityTask : ISaveStateApi<ActivityTaskSaveState>
    {
        public bool? IsCompleted { get; internal set; }
        public ActivityTask PrevTask { get; internal set; }
        public ActivityTask NextTask { get; internal set; }
        public TimeSpan CompletedAt { get; internal set; }
        public string DisplayMessage { get; internal set; }
        public Color DisplayColor { get; internal set; }

        public virtual void NotifyEvent(ActivityEventType EventType)
        {
        }

        public virtual ValueTask<ActivityTaskSaveState> Snapshot()
        {
            return ValueTask.FromResult(new ActivityTaskSaveState()
            {
                Completed = IsCompleted,
                CompletedAt = CompletedAt,
                Message = DisplayMessage,
            });
        }

        public virtual ValueTask Restore(ActivityTaskSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
            IsCompleted = saveState.Completed;
            CompletedAt = saveState.CompletedAt;
            DisplayMessage = saveState.Message;
            return ValueTask.CompletedTask;
        }
    }

    public class ActivityTaskPassengerStopAt : ActivityTask
    {
        public TimeSpan ScheduledArrival { get; private set; }
        public TimeSpan ScheduledDeparture { get; private set; }
        public TimeSpan? ActualArrival { get; private set; }
        public TimeSpan? ActualDeparture { get; private set; }
        public PlatformItem PlatformEnd1 { get; private set; }
        public PlatformItem PlatformEnd2 { get; private set; }

        internal double BoardingS { get; private set; }   // MSTS calls this the Load/Unload time. Cargo gets loaded, but passengers board the train.
        internal double BoardingEndS { get; private set; }

        private int timerChk;
        private bool arrived;
        private bool maydepart;
        private bool logStationStops;
        internal string logStationLogFile;
        private float distanceToNextSignal = -1;
        private Train playerTrain; // Shortcut to player train

        private bool debriefEvalDepartBeforeBoarding;//Debrief Eval

        public ActivityTaskPassengerStopAt(ActivityTask prev, int arrivalTime, int departureTime, PlatformItem platformStart, PlatformItem platformeEnd)
        {
            ScheduledArrival = TimeSpan.FromSeconds(arrivalTime);
            ScheduledDeparture = TimeSpan.FromSeconds(departureTime);
            PlatformEnd1 = platformStart;
            PlatformEnd2 = platformeEnd;
            PrevTask = prev;
            if (prev != null)
                prev.NextTask = this;
            DisplayMessage = "";

            logStationStops = false;
            logStationLogFile = null;
        }

        internal ActivityTaskPassengerStopAt()
        {
        }

        internal void SetLogStationStop(string stationLogFile)
        {
            logStationLogFile = stationLogFile;
            logStationStops = string.IsNullOrEmpty(stationLogFile);
        }

        public bool TrainMissedStation()
        {
            // Check if station is in present train path
            if (playerTrain.StationStops.Count == 0 || playerTrain.TCRoute.ActiveSubPath != playerTrain.StationStops[0].SubrouteIndex ||
                !(playerTrain.ControlMode == TrainControlMode.AutoNode || playerTrain.ControlMode == TrainControlMode.AutoSignal))
            {
                return false;
            }

            return playerTrain.MissedPlatform(200.0f);
        }

        public override void NotifyEvent(ActivityEventType EventType)
        {
            Simulator simulator = Simulator.Instance;
            playerTrain = simulator.OriginalPlayerTrain;
            switch (EventType)
            {
                // The train is stopped.
                case ActivityEventType.TrainStop:
                    if (playerTrain.TrainType != TrainType.AiPlayerHosting && playerTrain.TrainAtStation() ||
                        playerTrain.TrainType == TrainType.AiPlayerHosting && (playerTrain as AITrain).MovementState == AiMovementState.StationStop)
                    {
                        if (simulator.TimetableMode || playerTrain.StationStops.Count == 0)
                        {
                            // If yes, we arrived
                            if (ActualArrival == null)
                            {
                                ActualArrival = TimeSpan.FromSeconds((int)simulator.ClockTime);
                            }

                            arrived = true;

                            // Figure out the boarding time
                            // <CSComment> No midnight checks here? There are some in Train.CalculateDepartTime
                            double plannedBoardingS = (ScheduledDeparture - ScheduledArrival).TotalSeconds;
                            double punctualBoardingS = (ScheduledDeparture - ActualArrival.GetValueOrDefault(ScheduledArrival)).TotalSeconds;
                            double expectedBoardingS = plannedBoardingS > 0 ? plannedBoardingS : PlatformEnd1.PlatformMinWaitingTime;
                            BoardingS = punctualBoardingS;                                     // default is leave on time
                            if (punctualBoardingS < expectedBoardingS)                         // if not enough time for boarding
                            {
                                if (plannedBoardingS > 0 && plannedBoardingS < PlatformEnd1.PlatformMinWaitingTime)
                                { // and tight schedule
                                    BoardingS = plannedBoardingS;                              // leave late with no recovery of time
                                }
                                else
                                {                                                       // generous schedule
                                    BoardingS = Math.Max(
                                        punctualBoardingS,                                     // leave on time
                                        PlatformEnd1.PlatformMinWaitingTime);                  // leave late with some recovery
                                }
                            }
                            // ActArrive is usually same as ClockTime
                            BoardingEndS = simulator.ClockTime + BoardingS;
                            // But not if game starts after scheduled arrival. In which case actual arrival is assumed to be same as schedule arrival.
                            double sinceActArriveS = (TimeSpan.FromSeconds((int)simulator.ClockTime) - ActualArrival.GetValueOrDefault(ScheduledArrival)).TotalSeconds;
                            BoardingEndS -= sinceActArriveS;
                        }
                        else
                        {
                            // <CSComment> MSTS mode - player
                            if (simulator.GameTime < 2)
                            {
                                // If the simulation starts with a scheduled arrive in the past, assume the train arrived on time.
                                if (ScheduledArrival.TotalSeconds < simulator.ClockTime)
                                {
                                    ActualArrival = ScheduledArrival;
                                }
                            }
                            BoardingS = playerTrain.StationStops[0].ComputeStationBoardingTime(simulator.PlayerLocomotive.Train);
                            if (BoardingS > 0 || ((double)(ScheduledDeparture - ScheduledArrival).TotalSeconds > 0 &&
                                playerTrain.PassengerCarsNumber == 1 && playerTrain.Cars.Count > 10))
                            {
                                // accepted station stop because either freight train or passenger train or fake passenger train with passenger car on platform or fake passenger train
                                // with Scheduled Depart > Scheduled Arrive
                                // ActArrive is usually same as ClockTime
                                BoardingEndS = simulator.ClockTime + BoardingS;

                                if (!ActualArrival.HasValue)
                                {
                                    ActualArrival = TimeSpan.FromSeconds((int)simulator.ClockTime);
                                }

                                arrived = true;
                                // But not if game starts after scheduled arrival. In which case actual arrival is assumed to be same as schedule arrival.
                                double sinceActArriveS = (TimeSpan.FromSeconds((int)simulator.ClockTime) - ActualArrival.GetValueOrDefault(ScheduledArrival)).TotalSeconds;
                                BoardingEndS -= sinceActArriveS;
                                double SchDepartS = ScheduledDeparture.TotalSeconds;
                                BoardingEndS = Time.Compare.Latest((int)SchDepartS, (int)BoardingEndS);

                            }
                        }
                        if (playerTrain.NextSignalObjects[Direction.Forward] != null)
                            distanceToNextSignal = playerTrain.NextSignalObjects[Direction.Forward].DistanceTo(playerTrain.FrontTDBTraveller);
                    }
                    break;
                case ActivityEventType.TrainStart:
                    // Train has started, we have things to do if we arrived before
                    if (arrived)
                    {
                        ActualDeparture = TimeSpan.FromSeconds((int)simulator.ClockTime);
                        CompletedAt = ActualDeparture.Value;
                        // Completeness depends on the elapsed waiting time
                        IsCompleted = maydepart;
                        if (playerTrain.TrainType != TrainType.AiPlayerHosting)
                            playerTrain.ClearStation(PlatformEnd1.LinkedPlatformItemId, PlatformEnd2.LinkedPlatformItemId, true);

                        if (logStationStops)
                        {
                            StringBuilder stringBuild = new StringBuilder();
                            char separator = (char)simulator.Settings.DataLoggerSeparator;
                            stringBuild.Append(PlatformEnd1.Station);
                            stringBuild.Append(separator);
                            stringBuild.Append(ScheduledArrival.ToString("c"));
                            stringBuild.Append(separator);
                            stringBuild.Append(ScheduledDeparture.ToString("c"));
                            stringBuild.Append(separator);
                            stringBuild.Append(ActualArrival.HasValue ? ActualArrival.Value.ToString("c") : "-");
                            stringBuild.Append(separator);
                            stringBuild.Append(ActualDeparture.HasValue ? ActualDeparture.Value.ToString("c") : "-");

                            TimeSpan delay = ActualDeparture.HasValue ? (ActualDeparture - ScheduledDeparture).Value : TimeSpan.Zero;
                            stringBuild.Append(separator);
                            stringBuild.Append(delay.ToString("c"));
                            stringBuild.Append(separator);
                            stringBuild.Append(maydepart ? "Completed" : "NotCompleted");
                            stringBuild.Append('\n');
                            File.AppendAllText(logStationLogFile, stringBuild.ToString());
                        }
                    }
                    break;
                case ActivityEventType.Timer:
                    // Waiting at a station
                    if (arrived)
                    {
                        int remaining = (int)Math.Ceiling(BoardingEndS - simulator.ClockTime);

                        if (remaining < 1) DisplayColor = Color.LightGreen;
                        else if (remaining < 11) 
                            DisplayColor = new Color(255, 255, 128);
                        else 
                            DisplayColor = Color.White;

                        if (remaining < 120 && (playerTrain.TrainType != TrainType.AiPlayerHosting))
                        {
                            playerTrain.ClearStation(PlatformEnd1.LinkedPlatformItemId, PlatformEnd2.LinkedPlatformItemId, false);
                        }

                        // Still have to wait
                        if (remaining > 0)
                        {
                            DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completes in {0:D2}:{1:D2}",
                                remaining / 60, remaining % 60);

                            //Debrief Eval
                            if (simulator.PlayerLocomotive.SpeedMpS > 0 && !debriefEvalDepartBeforeBoarding)
                            {
                                Train train = simulator.PlayerLocomotive.Train;
                                debriefEvalDepartBeforeBoarding = true;
                                ActivityEvaluation.Instance.DepartBeforeBoarding++;
                            }
                        }
                        // May depart
                        else if (!maydepart)
                        {
                            // check if signal ahead is cleared - if not, do not allow depart
                            if (distanceToNextSignal >= 0 && distanceToNextSignal < 300 && playerTrain.NextSignalObjects[Direction.Forward] != null &&
                                playerTrain.NextSignalObjects[Direction.Forward].SignalLR(SignalFunction.Normal) == SignalAspectState.Stop
                                && playerTrain.NextSignalObjects[Direction.Forward].OverridePermission != SignalPermission.Granted)
                            {
                                DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. Waiting for signal ahead to clear.");
                            }
                            else
                            {
                                maydepart = true;
                                DisplayMessage = Simulator.Catalog.GetString("Passenger boarding completed. You may depart now.");
                                if (playerTrain.IsActualPlayerTrain)
                                    simulator.SoundNotify = TrainEvent.PermissionToDepart;
                            }

                            debriefEvalDepartBeforeBoarding = false;//reset flag. Debrief Eval

                            // if last task, show closure window
                            // also set times in logfile

                            if (NextTask == null)
                            {
                                if (logStationStops)
                                {
                                    StringBuilder stringBuild = new StringBuilder();
                                    char separator = (char)simulator.Settings.DataLoggerSeparator;
                                    stringBuild.Append(PlatformEnd1.Station);
                                    stringBuild.Append(separator);
                                    stringBuild.Append(ScheduledArrival.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture));
                                    stringBuild.Append(separator);
                                    stringBuild.Append('-');
                                    stringBuild.Append(separator);
                                    stringBuild.Append(ActualArrival.HasValue ? ActualArrival.Value.ToString("c") : "-");
                                    stringBuild.Append(separator);
                                    stringBuild.Append('-');
                                    stringBuild.Append(separator);

                                    TimeSpan delay = ActualArrival.HasValue ? (ActualArrival - ScheduledArrival).Value : TimeSpan.Zero;
                                    stringBuild.Append(delay.ToString("c"));
                                    stringBuild.Append(separator);
                                    stringBuild.Append("Final stop");
                                    stringBuild.Append('\n');
                                    File.AppendAllText(logStationLogFile, stringBuild.ToString());
                                }

                                IsCompleted = true;
                            }
                        }
                    }
                    else
                    {
                        // Checking missed station
                        int tmp = (int)(simulator.ClockTime % 10);
                        if (tmp != timerChk)
                        {
                            if (TrainMissedStation() && (playerTrain.TrainType != TrainType.AiPlayerHosting))
                            {
                                playerTrain.ClearStation(PlatformEnd1.LinkedPlatformItemId, PlatformEnd2.LinkedPlatformItemId, true);
                                IsCompleted = false;

                                if (logStationStops)
                                {
                                    StringBuilder stringBuild = new StringBuilder();
                                    char separator = (char)simulator.Settings.DataLoggerSeparator;
                                    stringBuild.Append(PlatformEnd1.Station);
                                    stringBuild.Append(separator);
                                    stringBuild.Append(ScheduledArrival.ToString("c"));
                                    stringBuild.Append(separator);
                                    stringBuild.Append(ScheduledDeparture.ToString("c"));
                                    stringBuild.Append(separator);
                                    stringBuild.Append('-');
                                    stringBuild.Append(separator);
                                    stringBuild.Append('-');
                                    stringBuild.Append(separator);
                                    stringBuild.Append('-');
                                    stringBuild.Append(separator);
                                    stringBuild.Append("Missed");
                                    stringBuild.Append('\n');
                                    File.AppendAllText(logStationLogFile, stringBuild.ToString());
                                }
                            }
                        }
                    }
                    break;
            }
        }

        public override async ValueTask<ActivityTaskSaveState> Snapshot()
        {
            ActivityTaskSaveState result = await base.Snapshot().ConfigureAwait(false);
            result.ScheduledArrival = ScheduledArrival;
            result.ScheduledDeparture = ScheduledDeparture;
            result.ActualArrival = ActualArrival;
            result.ActualDeparture = ActualDeparture;
            result.PlatformEnd1 = PlatformEnd1.TrackItemId;
            result.PlatformEnd2 = PlatformEnd2.TrackItemId;
            result.BooardingTime = BoardingS;
            result.BooardingTime = BoardingEndS;
            result.TimerCheck = timerChk;
            result.Arrived = arrived;
            result.ReadyToDepart = maydepart;
            result.DistanceToSignal = distanceToNextSignal;
            return result;
        }

        public override async ValueTask Restore([NotNull] ActivityTaskSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ScheduledArrival = saveState.ScheduledArrival;
            ScheduledDeparture = saveState.ScheduledDeparture;
            ActualArrival = saveState.ActualArrival;
            ActualDeparture = saveState.ActualDeparture;
            PlatformEnd1 = RuntimeData.Instance.TrackDB.TrackItems[saveState.PlatformEnd1] as PlatformItem;
            PlatformEnd2 = RuntimeData.Instance.TrackDB.TrackItems[saveState.PlatformEnd2] as PlatformItem;
            BoardingS = saveState.BooardingTime;
            BoardingEndS = saveState.BoardingEndTime;
            timerChk = saveState.TimerCheck;
            arrived = saveState.Arrived;
            maydepart = saveState.ReadyToDepart;
            distanceToNextSignal = saveState.DistanceToSignal;
        }
    }
}
