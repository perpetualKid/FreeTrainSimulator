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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.State;
using Orts.Simulation.Physics;

using ActivityEvent = Orts.Formats.Msts.Models.ActivityEvent;

namespace Orts.Simulation.Activities
{
    /// <summary>
    /// This class adds attributes around the event objects parsed from the ACT file.
    /// Note: Can't add attributes to the event objects directly as ACTFile.cs is not just used by 
    /// ActivityRunner.exe but also by Menu.exe and these executables lack most of the ORTS classes.
    /// </summary>
    public abstract class EventWrapper : ISaveStateApi<ActivityEventSaveState>
    {
        /// <summary>Maximum size of a platform or station we use for searching forward and backward</summary>
        protected const float MaxPlatformOrStationSize = 10000f;

        public ActivityEvent ActivityEvent { get; }     // Points to object parsed from file *.act
        public int OriginalActivationLevel { get; internal set; } // Needed to reset .ActivationLevel
        public int TimesTriggered { get; internal set; }          // Needed for evaluation after activity ends
        public bool Enabled { get; internal set; }          // Used for a reversible event to prevent it firing again until after it has been reset.
        public Train Train { get; internal set; }              // Train involved in event; if null actual or original player train

        protected EventWrapper(ActivityEvent activityEvent)
        {
            ActivityEvent = activityEvent;
            Train = null;
        }

        public ValueTask<ActivityEventSaveState> Snapshot()
        {
            return ValueTask.FromResult(new ActivityEventSaveState() 
            { 
                TimesTriggered = TimesTriggered,
                Enabled = Enabled
            });
        }

        public ValueTask Restore(ActivityEventSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            TimesTriggered = saveState.TimesTriggered;
            Enabled = saveState.Enabled;
            ActivityEvent.ActivationLevel = saveState.ActivationLevel;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// After an event is triggered, any message is displayed independently by ActivityWindow.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public abstract bool Triggered(Activity activity);

        /// <summary>
        /// Acts on the outcomes and then sets ActivationLevel = 0 to prevent re-use.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns>true if entire activity ends here whether it succeeded or failed</returns>
        internal bool CompletesActivity(Activity activity)
        {
            if (ActivityEvent.Reversible)
                // Stop this event being actioned
                Enabled = false;
            else
                // Stop this event being monitored
                ActivityEvent.ActivationLevel = 0;
            // No further action if this reversible event has been triggered before
            if (TimesTriggered > 1)
                return false;
            if (ActivityEvent.Outcomes == null)
                return false;

            // Set Activation Level of each event in the Activate list to 1.
            // Uses lambda expression => for brevity.
            foreach (int eventId in ActivityEvent.Outcomes.ActivateList)
                foreach (EventWrapper item in activity.EventList.Where(item => item.ActivityEvent.ID == eventId))
                    item.ActivityEvent.ActivationLevel = 1;
            foreach (int eventId in ActivityEvent.Outcomes.RestoreActivityLevels)
                foreach (EventWrapper item in activity.EventList.Where(item => item.ActivityEvent.ID == eventId))
                    item.ActivityEvent.ActivationLevel = item.OriginalActivationLevel;
            foreach (int eventId in ActivityEvent.Outcomes.DecrementActivityLevels)
                foreach (EventWrapper item in activity.EventList.Where(item => item.ActivityEvent.ID == eventId))
                    item.ActivityEvent.ActivationLevel += -1;
            foreach (int eventId in ActivityEvent.Outcomes.IncrementActivityLevels)
                foreach (EventWrapper item in activity.EventList.Where(item => item.ActivityEvent.ID == eventId))
                    item.ActivityEvent.ActivationLevel += +1;

            // Activity sound management
            if (ActivityEvent.SoundFile != null || ActivityEvent.Outcomes.ActivitySound != null)
                activity.TriggeredActivityEvent ??= this;

            if (ActivityEvent.WeatherChange != null || ActivityEvent.Outcomes.WeatherChange != null)
                activity.TriggeredActivityEvent ??= this;

            if (ActivityEvent.Outcomes.ActivityFail != null)
            {
                activity.Succeeded = false;
                return true;
            }
            if (ActivityEvent.Outcomes.ActivitySuccess == true)
            {
                activity.Succeeded = true;
                return true;
            }
            if (!string.IsNullOrEmpty(ActivityEvent.Outcomes.RestartWaitingTrain?.WaitingTrainToRestart))
            {
                RestartWaitingTrain restartWaitingTrain = ActivityEvent.Outcomes.RestartWaitingTrain;
                Simulator.Instance.RestartWaitingTrain(restartWaitingTrain);
            }
            return false;
        }

        private protected static DistanceResult CalculateToPoint(Traveller start, in WorldLocation target)
        {
            Traveller poiTraveller;
            poiTraveller = new Traveller(start);

            // Find distance once
            float distance = poiTraveller.DistanceTo(target, MaxPlatformOrStationSize);

            // If valid
            if (distance > 0)
            {
                return DistanceResult.Valid;
            }
            else
            {
                // Go to opposite direction
                poiTraveller = new Traveller(start, true);

                distance = poiTraveller.DistanceTo(target, MaxPlatformOrStationSize);
                // If valid, it is behind us
                if (distance > 0)
                {
                    return DistanceResult.Behind;
                }
            }

            // Otherwise off path
            return DistanceResult.OffPath;
        }
    }

    public class EventCategoryActionWrapper : EventWrapper
    {
        private readonly SidingItem sidingEnd1;
        private readonly SidingItem sidingEnd2;
        private List<string> changeWagonIdList;   // Wagons to be assembled, picked up or dropped off.

        public EventCategoryActionWrapper(ActivityEvent activityEvent)
            : base(activityEvent)
        {
            if (ActivityEvent is ActionActivityEvent actionActivityEvent && actionActivityEvent.SidingId > -1)
            {
                int i = actionActivityEvent.SidingId;
                try
                {
                    sidingEnd1 = RuntimeData.Instance.TrackDB.TrackItems[i] as SidingItem;
                    i = sidingEnd1.LinkedSidingId;
                    sidingEnd2 = RuntimeData.Instance.TrackDB.TrackItems[i] as SidingItem;
                }
                catch (IndexOutOfRangeException)
                {
                    Trace.TraceWarning("Siding {0} is not in track database.", i);
                }
                catch (NullReferenceException)
                {
                    Trace.TraceWarning("Item {0} in track database is not a siding.", i);
                }
            }
        }

        public override bool Triggered(Activity activity)
        {
            ArgumentNullException.ThrowIfNull(activity);

            Train playerTrain = Simulator.Instance.OriginalPlayerTrain;
            ActionActivityEvent actionActivityEvent = ActivityEvent as ActionActivityEvent ?? throw new InvalidCastException(nameof(ActivityEvent));
            if (actionActivityEvent.WorkOrderWagons != null)
            {                     // only if event involves wagons
                if (changeWagonIdList == null)
                {           // populate the list only once - the first time that ActivationLevel > 0 and so this method is called.
                    changeWagonIdList = new List<string>();
                    foreach (WorkOrderWagon item in actionActivityEvent.WorkOrderWagons)
                    {
                        changeWagonIdList.Add($"{((int)item.UiD & 0xFFFF0000) >> 16} - {(int)item.UiD & 0x0000FFFF}"); // form the .CarID
                    }
                }
            }
            bool triggered = false;
            Train consistTrain;
            switch (actionActivityEvent.Type)
            {
                case EventType.AllStops:
                    triggered = activity.Tasks.Count > 0 && activity.Last.IsCompleted != null;
                    break;
                case EventType.AssembleTrain:
                    consistTrain = MatchesConsist(changeWagonIdList);
                    if (consistTrain != null)
                    {
                        triggered = true;
                    }
                    break;
                case EventType.AssembleTrainAtLocation:
                    if (AtSiding(playerTrain.FrontTDBTraveller, playerTrain.RearTDBTraveller, sidingEnd1, sidingEnd2))
                    {
                        consistTrain = MatchesConsist(changeWagonIdList);
                        triggered = consistTrain != null;
                    }
                    break;
                case EventType.DropOffWagonsAtLocation:
                    // Dropping off of wagons should only count once disconnected from player train.
                    // A better name than DropOffWagonsAtLocation would be ArriveAtSidingWithWagons.
                    // To recognize the dropping off of the cars before the event is activated, this method is used.
                    if (AtSiding(playerTrain.FrontTDBTraveller, playerTrain.RearTDBTraveller, sidingEnd1, sidingEnd2))
                    {
                        consistTrain = MatchesConsistNoOrder(changeWagonIdList);
                        triggered = consistTrain != null;
                    }
                    break;
                case EventType.PickUpPassengers:
                    break;
                case EventType.PickUpWagons: // PickUpWagons is independent of location or siding
                    triggered = IncludesWagons(playerTrain, changeWagonIdList);
                    break;
                case EventType.ReachSpeed:
                    triggered = (Math.Abs(Simulator.Instance.PlayerLocomotive.SpeedMpS) >= actionActivityEvent.SpeedMpS);
                    break;
            }
            return triggered;
        }
        /// <summary>
        /// Finds the train that contains exactly the wagons (and maybe loco) in the list in the correct sequence.
        /// </summary>
        /// <param name="wagonIdList"></param>
        /// <returns>train or null</returns>
        private static Train MatchesConsist(List<string> wagonIdList)
        {
            foreach (Train trainItem in Simulator.Instance.Trains)
            {
                if (trainItem.Cars.Count == wagonIdList.Count)
                {
                    // Compare two lists to make sure wagons are in expected sequence.
                    bool listsMatch = true;
                    //both lists with the same order
                    for (int i = 0; i < trainItem.Cars.Count; i++)
                    {
                        if (trainItem.Cars.ElementAt(i).CarID != wagonIdList.ElementAt(i))
                        { listsMatch = false; break; }
                    }
                    if (!listsMatch)
                    {//different order list
                        listsMatch = true;
                        for (int i = trainItem.Cars.Count; i > 0; i--)
                        {
                            if (trainItem.Cars.ElementAt(i - 1).CarID != wagonIdList.ElementAt(trainItem.Cars.Count - i))
                            { listsMatch = false; break; }
                        }
                    }
                    if (listsMatch)
                        return trainItem;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the train that contains exactly the wagons (and maybe loco) in the list. Exact order is not required.
        /// </summary>
        /// <param name="wagonIdList"></param>
        /// <returns>train or null</returns>
        private static Train MatchesConsistNoOrder(List<string> wagonIdList)
        {
            foreach (Train trainItem in Simulator.Instance.Trains)
            {
                int numberCars = 0;//all cars other than WagonIdList.
                int numberWagonListCars = 0;//individual wagon drop.
                foreach (RollingStocks.TrainCar item in trainItem.Cars)
                {
                    if (!wagonIdList.Contains(item.CarID))
                        numberCars++;
                    if (wagonIdList.Contains(item.CarID))
                        numberWagonListCars++;
                }
                // Compare two lists to make sure wagons are present.
                bool listsMatch = true;
                //support individual wagonIdList drop
                if (trainItem.Cars.Count - numberCars == (wagonIdList.Count == numberWagonListCars ? wagonIdList.Count : numberWagonListCars))
                {
                    if (ExcludesWagons(trainItem, wagonIdList))
                        listsMatch = false;//all wagons dropped

                    if (listsMatch)
                        return trainItem;
                }
            }
            return null;
        }

        /// <summary>
        /// Like MSTS, do not check for unlisted wagons as the wagon list may be shortened for convenience to contain
        /// only the first and last wagon or even just the first wagon.
        /// </summary>
        /// <param name="train"></param>
        /// <param name="wagonIdList"></param>
        /// <returns>True if all listed wagons are part of the given train.</returns>
        private static bool IncludesWagons(Train train, List<string> wagonIdList)
        {
            foreach (string item in wagonIdList)
            {
                if (train.Cars.Find(car => car.CarID == item) == null)
                    return false;
            }
            // train speed < 1
            return Math.Abs(train.SpeedMpS) <= 1;
        }

        /// <summary>
        /// Like MSTS, do not check for unlisted wagons as the wagon list may be shortened for convenience to contain
        /// only the first and last wagon or even just the first wagon.
        /// </summary>
        /// <param name="train"></param>
        /// <param name="wagonIdList"></param>
        /// <returns>True if all listed wagons are not part of the given train.</returns>
        private static bool ExcludesWagons(Train train, List<string> wagonIdList)
        {
            // The Cars list is a global list that includes STATIC cars.  We need to make sure that the active train/car is processed only.
            if (train.TrainType == TrainType.Static)
                return true;

            bool notFound = false;
            foreach (string item in wagonIdList)
            {
                //take in count each item in wagonIdList 
                if (train.Cars.Find(car => car.CarID == item) == null)
                {
                    notFound = true; //wagon not part of the train
                }
                else
                {
                    notFound = false;
                    break;//wagon still part of the train
                }
            }
            return notFound;
        }

        /// <summary>
        /// Like platforms, checking that one end of the train is within the siding.
        /// </summary>
        /// <param name="frontPosition"></param>
        /// <param name="rearPosition"></param>
        /// <param name="sidingEnd1"></param>
        /// <param name="sidingEnd2"></param>
        /// <returns>true if both ends of train within siding</returns>
        private static bool AtSiding(Traveller frontPosition, Traveller rearPosition, SidingItem sidingEnd1, SidingItem sidingEnd2)
        {
            if (sidingEnd1 == null || sidingEnd2 == null)
            {
                return true;
            }

            DistanceResult distanceEnd1 = CalculateToPoint(frontPosition, sidingEnd1.Location);
            DistanceResult distanceEnd2 = CalculateToPoint(frontPosition, sidingEnd2.Location);

            // If front between the ends of the siding
            if (((distanceEnd1 == DistanceResult.Behind && distanceEnd2 == DistanceResult.Valid)
                || (distanceEnd1 == DistanceResult.Valid && distanceEnd2 == DistanceResult.Behind)))
            {
                return true;
            }

            distanceEnd1 = CalculateToPoint(rearPosition, sidingEnd1.Location);
            distanceEnd2 = CalculateToPoint(rearPosition, sidingEnd2.Location);

            // If rear between the ends of the siding
            if (((distanceEnd1 == DistanceResult.Behind && distanceEnd2 == DistanceResult.Valid)
                || (distanceEnd1 == DistanceResult.Valid && distanceEnd2 == DistanceResult.Behind)))
            {
                return true;
            }

            return false;
        }
    }

    public class EventCategoryLocationWrapper : EventWrapper
    {
        public EventCategoryLocationWrapper(ActivityEvent activityEvent)
            : base(activityEvent)
        {
        }

        public override bool Triggered(Activity activity)
        {
            bool triggered = false;
            LocationActivityEvent e = ActivityEvent as LocationActivityEvent ?? throw new InvalidCastException(nameof(ActivityEvent));
            Train train = Simulator.Instance.PlayerLocomotive.Train;
            if (!string.IsNullOrEmpty(ActivityEvent.TrainService) && Train != null)
            {
                if (Train.FrontTDBTraveller == null)
                    return triggered;
                train = Train;
            }
            Train = train;
            if (e.TriggerOnStop)
            {
                // Is train still moving?
                if (Math.Abs(train.SpeedMpS) > 0.032f)
                {
                    return triggered;
                }
            }
            Traveller trainFrontPosition = new Traveller(train.NextRouteReady && train.TCRoute.ActiveSubPath > 0 && train.TCRoute.ReversalInfo[train.TCRoute.ActiveSubPath - 1].Valid ?
                train.RearTDBTraveller : train.FrontTDBTraveller); // just after reversal the old train front position must be considered
            float distance = trainFrontPosition.DistanceTo(e.Location, e.RadiusM);
            if (distance == -1)
            {
                trainFrontPosition.ReverseDirection();
                distance = trainFrontPosition.DistanceTo(e.Location, e.RadiusM);
                if (distance == -1)
                    return triggered;
            }
            if (distance < e.RadiusM)
            {
                triggered = true;
            }
            return triggered;
        }
    }

    public class EventCategoryTimeWrapper : EventWrapper
    {

        public EventCategoryTimeWrapper(ActivityEvent activityEvent)
            : base(activityEvent)
        {
        }

        public override bool Triggered(Activity activity)
        {
            ArgumentNullException.ThrowIfNull(activity);

            TimeActivityEvent timeActivityEvent = ActivityEvent as TimeActivityEvent ?? throw new InvalidCastException(nameof(ActivityEvent));
            //if (timeActivityEvent == null) 
            //    return false;
            Train = Simulator.Instance.PlayerLocomotive.Train;
            bool triggered = (timeActivityEvent.Time <= (int)Simulator.Instance.ClockTime - activity.StartTime);
            return triggered;
        }

    }

    public class EventCategorySystemWrapper : EventWrapper
    {
        public EventCategorySystemWrapper(string header, string text) :
            base(new SystemActivityEvent(header, text))
        {
        }

        public override bool Triggered(Activity activity)
        {
            return true;
        }
    }

    // Result of calculation
    internal enum DistanceResult
    {
        Valid,
        Behind,
        OffPath,
    }
}
