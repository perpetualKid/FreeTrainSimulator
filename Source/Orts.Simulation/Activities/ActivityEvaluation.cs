using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Scripting.Api;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Activities
{
    public class ActivityEvaluation : ISaveStateApi<ActivityEvaluationState>
    {
        private double distanceTravelled;
        private double autoPilotInitialTime;
        private double autoPilotTime;
        private bool autoPilotTimerRunning;
        private bool overSpeedRunning;
        private bool fullTrainBrakeBelow8kmhRunning;
        private double overSpeedInitialTime;
        private double overSpeedTime;
        private static ActivityEvaluation instance;

        private ActivityEvaluation()
        { }

        public static ActivityEvaluation Instance
        {
            get
            {
                if (null == instance)
                    instance = new ActivityEvaluation();
                return instance;
            }
        }

        public void Update()
        {
            if (ActivityCompleted)
                return;

            bool overSpeedNow = Math.Abs(Simulator.Instance.PlayerLocomotive.Train.SpeedMpS) >
                Math.Min(Simulator.Instance.PlayerLocomotive.Train.AllowedMaxSpeedMpS, Simulator.Instance.PlayerLocomotive.Train.TrainMaxSpeedMpS) + 1.67; //3.8MpH/6.kmh

            if (overSpeedNow && !overSpeedRunning)//Debrief Eval
            {
                overSpeedRunning = true;
                overSpeedInitialTime = Simulator.Instance.ClockTime;
            }
            else if (overSpeedRunning && !overSpeedNow)//Debrief Eval
            {
                overSpeedRunning = false;
                overSpeedTime += Simulator.Instance.ClockTime - overSpeedInitialTime;
                OverSpeed++;
            }
            MSTSLocomotive lead = Simulator.Instance.PlayerLocomotive.Train.LeadLocomotive;
            const double kmh8mps = 8 / 3.6;
            if (!fullTrainBrakeBelow8kmhRunning && BrakeController.IsEmergencyState(lead.TrainBrakeController.State) && lead.IsPlayerTrain && lead.AbsSpeedMpS < kmh8mps)
            {
                FullTrainBrakeUnder8kmh++;
                fullTrainBrakeBelow8kmhRunning = true;
            }
            else if (fullTrainBrakeBelow8kmhRunning && !BrakeController.IsEmergencyState(lead.TrainBrakeController.State))
            {
                fullTrainBrakeBelow8kmhRunning = false;
            }
            if (Simulator.Instance.ActivityRun?.Completed ?? false)
            {
                Report();
                ActivityCompleted = true;
            }
        }

        public bool ActivityCompleted { get; private set; }

        public int CouplerBreaks { get; set; }

        public int TravellingTooFast { get; set; }

        public int TrainOverTurned { get; set; }

        public int SnappedBrakeHose { get; set; }

        public double DistanceTravelled
        {
            get => distanceTravelled + Simulator.Instance.PlayerLocomotive.DistanceTravelled;
            set
            {
                distanceTravelled = value;
            }
        }

        public int FullTrainBrakeUnder8kmh { get; set; }

        public int FullBrakeAbove16kmh { get; set; }

        public int OverSpeedCoupling { get; set; }

        public int EmergencyButtonStopped { get; set; }

        public int EmergencyButtonMoving { get; set; }

        public void StartAutoPilotTime()
        {
            if (!autoPilotTimerRunning)
                autoPilotInitialTime = Simulator.Instance.ClockTime;
            autoPilotTimerRunning = true;
        }

        public void StopAutoPilotTime()
        {
            if (autoPilotTimerRunning)
                autoPilotTime += Simulator.Instance.ClockTime - autoPilotInitialTime;
            autoPilotTimerRunning = false;
        }

        public double AutoPilotTime
        {
            get => autoPilotTimerRunning ? Simulator.Instance.ClockTime - autoPilotInitialTime + autoPilotTime : autoPilotTime;
        }

        public int OverSpeed { get; private set; }

        public double OverSpeedTime
        {
            get => overSpeedRunning ? Simulator.Instance.ClockTime - overSpeedInitialTime + overSpeedTime : overSpeedTime;
        }

        public int DepartBeforeBoarding { get; set; }

        public ValueTask<ActivityEvaluationState> Snapshot()
        {
            return ValueTask.FromResult(new ActivityEvaluationState()
            {
                CouplerBreaks = CouplerBreaks,
                TravellingTooFast = TravellingTooFast,
                TrainOverTurned = TrainOverTurned,
                BrakedHoseSnapped = SnappedBrakeHose,
                DistanceTravelled = DistanceTravelled,
                FullTrainBrakeUnder8kmh = FullTrainBrakeUnder8kmh,
                FullBrakeAbove16kmh = FullBrakeAbove16kmh,
                OverSpeedCoupling = OverSpeedCoupling,
                EmergencyButtonStopped = EmergencyButtonStopped,
                EmergencyButtonMoving = EmergencyButtonMoving,
                OverSpeed = OverSpeed,
                OverSpeedInitialTime = overSpeedInitialTime,
                OverSpeedTime = overSpeedTime,
                DepartBeforeBoarding = DepartBeforeBoarding,
                AutoPilotRunning = autoPilotTimerRunning,
                AutoPilotInitialTime = autoPilotInitialTime,
                AutoPilotTime = autoPilotTime,
            });
        }

        public ValueTask Restore(ActivityEvaluationState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            instance = new ActivityEvaluation
            {
                CouplerBreaks = saveState.CouplerBreaks,
                TravellingTooFast = saveState.TravellingTooFast,
                TrainOverTurned = saveState.TrainOverTurned,
                SnappedBrakeHose = saveState.BrakedHoseSnapped,
                distanceTravelled = saveState.DistanceTravelled,
                FullTrainBrakeUnder8kmh = saveState.FullTrainBrakeUnder8kmh,
                FullBrakeAbove16kmh = saveState.FullBrakeAbove16kmh,
                OverSpeedCoupling = saveState.OverSpeedCoupling,
                EmergencyButtonStopped = saveState.EmergencyButtonStopped,
                EmergencyButtonMoving = saveState.EmergencyButtonMoving,
                autoPilotTimerRunning = saveState.AutoPilotRunning,
                autoPilotInitialTime = saveState.AutoPilotInitialTime,
                autoPilotTime = saveState.AutoPilotTime,
                OverSpeed = saveState.OverSpeed,
                overSpeedInitialTime = saveState.OverSpeedInitialTime,
                overSpeedTime = saveState.OverSpeedTime,
                DepartBeforeBoarding = saveState.DepartBeforeBoarding,
            };
            return ValueTask.CompletedTask;
        }

        public string ReportFileName { get; private set; }

        public string ReportText { get; private set; }

        public void Report()
        {
            ReportFileName = Path.Combine(RuntimeInfo.UserDataFolder, Simulator.Instance.SaveFileName + ".eval.txt");
            StringBuilder builder = new StringBuilder();
            Simulator simulator = Simulator.Instance;

            builder.AppendLine(CultureInfo.InvariantCulture, $"This is a Debrief Eval for {RuntimeInfo.ProductName}");
            builder.AppendLine(LoggingUtil.SeparatorLine);
            builder.AppendLine(CultureInfo.InvariantCulture, $"{"Version",-14}= {VersionInfo.Version}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"{"Code Version",-14}= {VersionInfo.CodeVersion}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"{"Debrief File",-14}= {ReportFileName.Replace(System.Environment.UserName, "********", StringComparison.OrdinalIgnoreCase)}");
            builder.AppendLine(LoggingUtil.SeparatorLine);
            builder.AppendLine();
            builder.AppendLine(CultureInfo.InvariantCulture, $"{string.Empty,10}Evalulation Debrief");
            builder.AppendLine(CultureInfo.InvariantCulture, $"{string.Empty,10}*******************");
            builder.AppendLine();

            builder.AppendLine("0-Information:");
            //Activity
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Route",-26}= {simulator.RouteModel.Name}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Activity",-26}= {simulator.ActivityModel?.Name}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Difficulty",-26}= {simulator.ActivityModel?.Difficulty}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Start Time",-26}= {simulator.ActivityModel?.StartTime}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Estimated Time",-26}= {simulator.ActivityModel?.Duration}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Elapsed Time",-26}= {FormatStrings.FormatTime(simulator.ClockTime - simulator.ActivityModel.StartTime.ToTimeSpan().TotalSeconds)}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Autopilot Time",-26}= {FormatStrings.FormatTime(AutoPilotTime)}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Distance Travelled",-26}= {FormatStrings.FormatDistanceDisplay(DistanceTravelled, simulator.MetricUnits)}");

            float dieselBurned = 0;
            float waterBurnedPerc = 0;
            float coalBurnedPerc = 0;
            float coalBurned = 0;
            EnumArray<int, EngineType> locomotiveTypes = new EnumArray<int, EngineType>();
            foreach (TrainCar car in Simulator.Instance.PlayerLocomotive.Train.Cars)
            {
                switch (car.EngineType)
                {
                    case EngineType.Electric:
                        locomotiveTypes[EngineType.Electric]++;
                        break;
                    case EngineType.Diesel:
                        dieselBurned += (car is MSTSDieselLocomotive dieselLocomotive) ? dieselLocomotive.MaxDieselLevelL - dieselLocomotive.DieselLevelL : 0;
                        locomotiveTypes[EngineType.Diesel]++;
                        break;
                    case EngineType.Steam:
                        if (car.AuxWagonType == AuxWagonType.Engine && car is MSTSSteamLocomotive steamLocomotive)
                        {
                            coalBurned += steamLocomotive.MaxTenderCoalMassKG - steamLocomotive.TenderCoalMassKG;
                            coalBurnedPerc = 1 - (steamLocomotive.TenderCoalMassKG / steamLocomotive.MaxTenderCoalMassKG);
                            waterBurnedPerc = 1 - (steamLocomotive.CombinedTenderWaterVolumeUKG / steamLocomotive.MaxTotalCombinedWaterVolumeUKG);
                        }
                        locomotiveTypes[EngineType.Steam]++;
                        break;
                }
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Consist engine",-26}= {(locomotiveTypes[EngineType.Steam] > 0 ? locomotiveTypes[EngineType.Steam] + " " + EngineType.Steam.GetDescription() + ", " : "")}{(locomotiveTypes[EngineType.Diesel] > 0 ? locomotiveTypes[EngineType.Diesel] + " " + EngineType.Diesel.GetDescription() + ", " : "")}{(locomotiveTypes[EngineType.Electric] > 0 ? locomotiveTypes[EngineType.Electric] + " " + EngineType.Electric.GetDescription() + ", " : "")}");
            builder.Remove(builder.Length - 2, 2);
            builder.AppendLine();

            if (locomotiveTypes[EngineType.Steam] > 0)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Burned Coal",-26}= {FormatStrings.FormatMass(coalBurned, simulator.MetricUnits)} ({coalBurnedPerc:0.##}%)");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Water consumption",-26}= {waterBurnedPerc:0.##}%");
            }
            if (locomotiveTypes[EngineType.Diesel] > 0)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Burned Diesel",-26}= {FormatStrings.FormatFuelVolume(dieselBurned, simulator.MetricUnits, simulator.UserSettings.MeasurementUnit == MeasurementUnit.UK)}");
            }
            builder.AppendLine();

            builder.AppendLine("1-Station Arrival, Departure, Passing Evaluation:");
            if (simulator.ActivityRun != null)
            {
                List<PassengerStopTask> stopTasks = new List<PassengerStopTask>();
                double stationPenalty = 0;
                foreach (ActivityTaskPassengerStopAt stopTask in simulator.ActivityRun.Tasks.OfType<ActivityTaskPassengerStopAt>())
                {
                    stopTasks.Add(new PassengerStopTask()
                    {
                        StationName = stopTask.PlatformEnd1.Station,
                        ScheduledArrival = stopTask.ScheduledArrival.ToString(),
                        ActualArrival = stopTask.ActualArrival.HasValue ? stopTask.ActualArrival.Value.ToString() : null,
                        ScheduledDeparture = stopTask.ScheduledDeparture.ToString(),
                        ActualDeparture = stopTask.ActualDeparture.HasValue ? stopTask.ActualDeparture.Value.ToString() : null,
                        StopMissed = !(stopTask.ActualArrival.HasValue || !stopTask.ActualDeparture.HasValue) && stopTask.IsCompleted.HasValue && stopTask.NextTask != null,
                    });
                }

                if (stopTasks.Count > 0)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Station Arrival",-26}= {stopTasks.Count}");
                    if (simulator.PlayerLocomotive.Train.Delay != null)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Delay",-26}= {simulator.PlayerLocomotive.Train.Delay.Value}");
                        //Delayed. -0.2 per second. 
                        stationPenalty += 0.2 * (long)simulator.PlayerLocomotive.Train.Delay.Value.TotalSeconds;//second

                    }
                    int missedStationStops;
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Missed station stops",-26}= {missedStationStops = stopTasks.Where((stopTask) => stopTask.StopMissed).Count()} {(missedStationStops == 1 ? "Station" : "Stations")}");
                    foreach (PassengerStopTask item in stopTasks.Where((stopTask) => stopTask.StopMissed))
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"{item.StationName}, ");
                    }
                    if (missedStationStops > 0)
                    {
                        builder.Remove(builder.Length - 2, 2);
                        builder.AppendLine();
                        //Missed station stops. -20.
                        stationPenalty += missedStationStops * 20;
                    }
                    builder.AppendLine($"  {"Departure before"}");
                    builder.AppendLine(CultureInfo.InvariantCulture, $"    {"boarding completed",-24}= {DepartBeforeBoarding}");
                    //Station departure before passenger boarding completed. -80.                                
                    stationPenalty += DepartBeforeBoarding * 80;
                }
                else
                {
                    builder.AppendLine($"{"  No Station stops."}");
                }
                //Station Arrival, Departure, Passing Evaluation. Overall Rating.
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Overall rating total",-26}= {(stopTasks.Count > 0 ? $"{Convert.ToInt16(stationPenalty)}" : "0")}");
                builder.AppendLine();

                builder.AppendLine("2-Work Orders:");
                List<ActivityEvent> workOrderTasks = new List<ActivityEvent>();
                int workOrderTasksDone = 0;
                foreach (EventWrapper eventWrapper in Simulator.Instance.ActivityRun.EventList ?? Enumerable.Empty<EventWrapper>())
                {
                    if (eventWrapper.ActivityEvent is ActionActivityEvent eventAction)
                    {
                        string activityName = eventAction.Type switch
                        {
                            EventType.AssembleTrain => "Assemble Train",
                            EventType.AssembleTrainAtLocation => "Assemble Train At Location",
                            EventType.DropOffWagonsAtLocation => "Drop Off",
                            EventType.PickUpPassengers => "Pick Up",
                            EventType.PickUpWagons => "Pick Up",
                            _ => null,
                        };
                        if (null != activityName)
                        {
                            ActivityEvent activityEvent = new ActivityEvent()
                            { ActivityName = activityName, };

                            if (eventAction.WorkOrderWagons != null)
                            {
                                foreach (WorkOrderWagon wagonItem in eventAction.WorkOrderWagons)
                                {
                                    int sidingId = eventAction.Type == EventType.AssembleTrainAtLocation || eventAction.Type == EventType.DropOffWagonsAtLocation
                                        ? eventAction.SidingId : wagonItem.SidingId;
                                    string location = RuntimeData.Instance.TrackDB.TrackItems.OfType<SidingItem>().Where((siding) => siding.TrackItemId == sidingId).FirstOrDefault()?.ItemName;

                                    if (activityEvent.ActivityLocation != location)
                                        activityEvent.ActivityLocation = location;
                                    // Status column
                                    if (eventWrapper.TimesTriggered == 1)
                                    {
                                        activityEvent.ActivityStatus = "Done";
                                        workOrderTasksDone++;
                                    }
                                }
                            }
                            workOrderTasks.Add(activityEvent);
                        }
                    }
                }
                if (workOrderTasks.Count > 0)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Task",-30}{"Location",-30}{"Status"}");
                    builder.Append('-', 68);
                    builder.AppendLine();
                    foreach (ActivityEvent eventTask in workOrderTasks)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"  {eventTask.ActivityName,-30}{eventTask.ActivityLocation,-30}{eventTask.ActivityStatus}");
                    }
                    //Coupling Over Speed > 1.5 MpS (5.4Kmh 3.3Mph)
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Coupling speed limits",-26}= {OverSpeedCoupling}");
                }
                else
                {
                    builder.AppendLine($"{"  No Tasks."}");
                }
                //Work orders. 100. Overall Rating.
                int workOrderPenalty = workOrderTasks.Count > 0 ? (100 / workOrderTasks.Count * workOrderTasksDone) - (OverSpeedCoupling * 5) : 0;
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Overall rating total",-26}= {workOrderPenalty}");
                builder.AppendLine();

                builder.AppendLine("3-Speed Evaluation:");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Over Speed",-26}= {OverSpeed}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Over Speed (Time)",-26}= {FormatStrings.FormatTime(OverSpeedTime)}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Overall rating total",-26}= {Convert.ToInt16(100 - OverSpeedTime * 1.5)}");
                builder.AppendLine();

                builder.AppendLine("4-Freight Durability/Passenger Comfort Evaluation:");
                builder.AppendLine(simulator.UserSettings.CurveDependentSpeedLimits ? $"  {"Curve speeds exceeded",-26}= {TravellingTooFast}" : "  Curve dependent speed limit (Disabled)");
                builder.AppendLine(simulator.UserSettings.CurveDependentSpeedLimits ? $"  {"Hose breaks",-26}= {SnappedBrakeHose}" : "  Curve dependent speed limit (Disabled)");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {(simulator.UserSettings.CouplersBreak ? "Coupler breaks" : "Coupler overloaded"),-26}= {CouplerBreaks}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Train Overturned",-26}= {TrainOverTurned}");
                int curveSpeedPenalty = 100 - (TravellingTooFast + SnappedBrakeHose + CouplerBreaks + TrainOverTurned);
                curveSpeedPenalty = curveSpeedPenalty > 100 ? 100 : curveSpeedPenalty;
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Overall rating total",-26}= {Convert.ToInt16(curveSpeedPenalty)}");
                builder.AppendLine();

                builder.AppendLine("5-Emergency/Penalty Actions Evaluation:");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Full Train Brake"}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {"below 5MPH/8KMH",-24}= {FullTrainBrakeUnder8kmh}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Emergency applications"}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {"while moving",-24}= {EmergencyButtonMoving}");
                builder.AppendLine($"  {"Emergency applications"}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {"while stopped",-24}= {EmergencyButtonStopped}");
                builder.AppendLine($"  {"Alerter applications"}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    {"above 10MPH/16KMH",-24}= {FullBrakeAbove16kmh}");
                int emergencyPenalty = (100 - (FullTrainBrakeUnder8kmh + EmergencyButtonMoving + EmergencyButtonStopped + FullBrakeAbove16kmh));
                emergencyPenalty = emergencyPenalty > 100 ? 100 : emergencyPenalty;
                builder.AppendLine(CultureInfo.InvariantCulture, $"  {"Overall rating total",-26}= {Convert.ToInt16(emergencyPenalty)}");
                builder.AppendLine();
                builder.AppendLine(CultureInfo.InvariantCulture, $"{string.Empty,10}Rating & Stars");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{string.Empty,10}**************");
                builder.AppendLine();

                builder.AppendLine(CultureInfo.InvariantCulture, $"{"1-Station Arrival, Departure, Passing Evaluation",-50}= {DrawStar((int)stationPenalty)}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{"2-Work Orders",-50}= {DrawStar(workOrderPenalty)}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{"3-Speed Evaluation",-50}= {DrawStar(Convert.ToInt16(100 - OverSpeedTime * 1.5))}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{"4-Freight Durability/Passenger Comfort Evaluation",-50}= {DrawStar(curveSpeedPenalty)}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"{"5-Emergency/Penalty Actions Evaluation",-50}= {DrawStar(emergencyPenalty)}");

            }

            builder.AppendLine();
            builder.AppendLine(LoggingUtil.SeparatorLine);
            builder.AppendLine();

            using (StreamWriter writer = new StreamWriter(ReportFileName, new FileStreamOptions() { Access = FileAccess.Write, Mode = FileMode.OpenOrCreate }))
            {
                writer.Write(ReportText = builder.ToString());
            }
        }

        private static string DrawStar(int value)
        {
            //            char starBlack = '★';
            //            char starWhite = '☆';

            return (value / 20) switch
            {
                1 => "★ ☆ ☆ ☆ ☆",
                2 => "★ ★ ☆ ☆ ☆",
                3 => "★ ★ ★ ☆ ☆",
                4 => "★ ★ ★ ★ ☆",
                5 => "★ ★ ★ ★ ★",
                _ => "-  -  -  -  -",
            };
        }

        private struct PassengerStopTask
        {
            public string StationName;
            public string ScheduledArrival;
            public string ActualArrival;
            public string ScheduledDeparture;
            public string ActualDeparture;
            public bool StopMissed;
        }

        private struct ActivityEvent
        {
            public string ActivityName;
            public string ActivityLocation;
            public string ActivityStatus;
        }
    }
}
