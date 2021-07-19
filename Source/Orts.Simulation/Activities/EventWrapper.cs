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

using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO;
using System.Linq;

using Orts.Formats.Msts.Models;
using Orts.Simulation.Physics;
using Orts.Formats.Msts;

namespace Orts.Simulation.Activities
{
    /// <summary>
    /// This class adds attributes around the event objects parsed from the ACT file.
    /// Note: Can't add attributes to the event objects directly as ACTFile.cs is not just used by 
    /// ActivityRunner.exe but also by Menu.exe and these executables lack most of the ORTS classes.
    /// </summary>
    public abstract class EventWrapper
    {
        public Formats.Msts.Models.ActivityEvent ParsedObject;     // Points to object parsed from file *.act
        public int OriginalActivationLevel; // Needed to reset .ActivationLevel
        public int TimesTriggered;          // Needed for evaluation after activity ends
        public bool IsDisabled;          // Used for a reversible event to prevent it firing again until after it has been reset.
        protected Simulator Simulator;
        public Train Train;              // Train involved in event; if null actual or original player train

        public EventWrapper(Formats.Msts.Models.ActivityEvent @event, Simulator simulator)
        {
            ParsedObject = @event;
            Simulator = simulator;
            Train = null;
        }

        public virtual void Save(BinaryWriter outf)
        {
            outf.Write(TimesTriggered);
            outf.Write(IsDisabled);
            outf.Write(ParsedObject.ActivationLevel);
        }

        public virtual void Restore(BinaryReader inf)
        {
            TimesTriggered = inf.ReadInt32();
            IsDisabled = inf.ReadBoolean();
            ParsedObject.ActivationLevel = inf.ReadInt32();
        }

        /// <summary>
        /// After an event is triggered, any message is displayed independently by ActivityWindow.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public virtual bool Triggered(Activity activity)
        {  // To be overloaded by subclasses
            return false;  // Compiler insists something is returned.
        }

        /// <summary>
        /// Acts on the outcomes and then sets ActivationLevel = 0 to prevent re-use.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns>true if entire activity ends here whether it succeeded or failed</returns>
        public bool IsActivityEnded(Activity activity)
        {

            if (ParsedObject.Reversible)
                // Stop this event being actioned
                IsDisabled = true;
            else
                // Stop this event being monitored
                ParsedObject.ActivationLevel = 0;
            // No further action if this reversible event has been triggered before
            if (TimesTriggered > 1) return false;
            if (ParsedObject.Outcomes == null) return false;
            // Set Activation Level of each event in the Activate list to 1.
            // Uses lambda expression => for brevity.
            foreach (int eventId in ParsedObject.Outcomes.ActivateList)
                foreach (var item in activity.EventList.Where(item => item.ParsedObject.ID == eventId))
                    item.ParsedObject.ActivationLevel = 1;
            foreach (int eventId in ParsedObject.Outcomes.RestoreActivityLevels)
                foreach (var item in activity.EventList.Where(item => item.ParsedObject.ID == eventId))
                    item.ParsedObject.ActivationLevel = item.OriginalActivationLevel;
            foreach (int eventId in ParsedObject.Outcomes.DecrementActivityLevels)
                foreach (var item in activity.EventList.Where(item => item.ParsedObject.ID == eventId))
                    item.ParsedObject.ActivationLevel += -1;
            foreach (int eventId in ParsedObject.Outcomes.IncrementActivityLevels)
                foreach (var item in activity.EventList.Where(item => item.ParsedObject.ID == eventId))
                    item.ParsedObject.ActivationLevel += +1;

            // Activity sound management

            if (ParsedObject.SoundFile != null || ParsedObject.Outcomes != null && ParsedObject.Outcomes.ActivitySound != null)
                if (activity.triggeredEventWrapper == null) activity.triggeredEventWrapper = this;

            if (ParsedObject.WeatherChange != null || ParsedObject.Outcomes != null && ParsedObject.Outcomes.WeatherChange != null)
                if (activity.triggeredEventWrapper == null) activity.triggeredEventWrapper = this;

            if (ParsedObject.Outcomes.ActivityFail != null)
            {
                activity.Succeeded = false;
                return true;
            }
            if (ParsedObject.Outcomes.ActivitySuccess == true)
            {
                activity.Succeeded = true;
                return true;
            }
            if (!string.IsNullOrEmpty(ParsedObject.Outcomes.RestartWaitingTrain?.WaitingTrainToRestart))
            {
                var restartWaitingTrain = ParsedObject.Outcomes.RestartWaitingTrain;
                Simulator.RestartWaitingTrain(restartWaitingTrain);
            }
            return false;
        }
    }

    public class EventCategoryActionWrapper : EventWrapper
    {
        private SidingItem SidingEnd1;
        private SidingItem SidingEnd2;
        private List<string> ChangeWagonIdList;   // Wagons to be assembled, picked up or dropped off.

        public EventCategoryActionWrapper(Orts.Formats.Msts.Models.ActivityEvent @event, Simulator simulator)
            : base(@event, simulator)
        {
            var e = this.ParsedObject as ActionActivityEvent;
            if (e.SidingId != null)
            {
                var i = e.SidingId.Value;
                try
                {
                    SidingEnd1 = Simulator.TrackDatabase.TrackDB.TrackItems[i] as SidingItem;
                    i = SidingEnd1.LinkedSidingId;
                    SidingEnd2 = Simulator.TrackDatabase.TrackDB.TrackItems[i] as SidingItem;
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

        public override Boolean Triggered(Activity activity)
        {
            Train OriginalPlayerTrain = Simulator.OriginalPlayerTrain;
            var e = this.ParsedObject as ActionActivityEvent;
            if (e.WorkOrderWagons != null)
            {                     // only if event involves wagons
                if (ChangeWagonIdList == null)
                {           // populate the list only once - the first time that ActivationLevel > 0 and so this method is called.
                    ChangeWagonIdList = new List<string>();
                    foreach (var item in e.WorkOrderWagons)
                    {
                        ChangeWagonIdList.Add($"{((int)item.UiD & 0xFFFF0000) >> 16} - {(int)item.UiD & 0x0000FFFF}"); // form the .CarID
                    }
                }
            }
            var triggered = false;
            Train consistTrain;
            switch (e.Type)
            {
                case EventType.AllStops:
                    triggered = activity.Tasks.Count > 0 && activity.Last.IsCompleted != null;
                    break;
                case EventType.AssembleTrain:
                    consistTrain = matchesConsist(ChangeWagonIdList);
                    if (consistTrain != null)
                    {
                        triggered = true;
                    }
                    break;
                case EventType.AssembleTrainAtLocation:
                    if (atSiding(OriginalPlayerTrain.FrontTDBTraveller, OriginalPlayerTrain.RearTDBTraveller, this.SidingEnd1, this.SidingEnd2))
                    {
                        consistTrain = matchesConsist(ChangeWagonIdList);
                        triggered = consistTrain != null;
                    }
                    break;
                case EventType.DropOffWagonsAtLocation:
                    // Dropping off of wagons should only count once disconnected from player train.
                    // A better name than DropOffWagonsAtLocation would be ArriveAtSidingWithWagons.
                    // To recognize the dropping off of the cars before the event is activated, this method is used.
                    if (atSiding(OriginalPlayerTrain.FrontTDBTraveller, OriginalPlayerTrain.RearTDBTraveller, this.SidingEnd1, this.SidingEnd2))
                    {
                        consistTrain = matchesConsistNoOrder(ChangeWagonIdList);
                        triggered = consistTrain != null;
                    }
                    break;
                case EventType.PickUpPassengers:
                    break;
                case EventType.PickUpWagons: // PickUpWagons is independent of location or siding
                    triggered = includesWagons(OriginalPlayerTrain, ChangeWagonIdList);
                    break;
                case EventType.ReachSpeed:
                    triggered = (Math.Abs(Simulator.PlayerLocomotive.SpeedMpS) >= e.SpeedMpS);
                    break;
            }
            return triggered;
        }
        /// <summary>
        /// Finds the train that contains exactly the wagons (and maybe loco) in the list in the correct sequence.
        /// </summary>
        /// <param name="wagonIdList"></param>
        /// <returns>train or null</returns>
        private Train matchesConsist(List<string> wagonIdList)
        {
            foreach (var trainItem in Simulator.Trains)
            {
                if (trainItem.Cars.Count == wagonIdList.Count)
                {
                    // Compare two lists to make sure wagons are in expected sequence.
                    bool listsMatch = true;
                    //both lists with the same order
                    for (int i = 0; i < trainItem.Cars.Count; i++)
                    {
                        if (trainItem.Cars.ElementAt(i).CarID != wagonIdList.ElementAt(i)) { listsMatch = false; break; }
                    }
                    if (!listsMatch)
                    {//different order list
                        listsMatch = true;
                        for (int i = trainItem.Cars.Count; i > 0; i--)
                        {
                            if (trainItem.Cars.ElementAt(i - 1).CarID != wagonIdList.ElementAt(trainItem.Cars.Count - i)) { listsMatch = false; break; }
                        }
                    }
                    if (listsMatch) return trainItem;
                }
            }
            return null;
        }
        /// <summary>
        /// Finds the train that contains exactly the wagons (and maybe loco) in the list. Exact order is not required.
        /// </summary>
        /// <param name="wagonIdList"></param>
        /// <returns>train or null</returns>
        private Train matchesConsistNoOrder(List<string> wagonIdList)
        {
            foreach (var trainItem in Simulator.Trains)
            {
                int nCars = 0;//all cars other than WagonIdList.
                int nWagonListCars = 0;//individual wagon drop.
                foreach (var item in trainItem.Cars)
                {
                    if (!wagonIdList.Contains(item.CarID)) nCars++;
                    if (wagonIdList.Contains(item.CarID)) nWagonListCars++;
                }
                // Compare two lists to make sure wagons are present.
                bool listsMatch = true;
                //support individual wagonIdList drop
                if (trainItem.Cars.Count - nCars == (wagonIdList.Count == nWagonListCars ? wagonIdList.Count : nWagonListCars))
                {
                    if (excludesWagons(trainItem, wagonIdList)) listsMatch = false;//all wagons dropped

                    if (listsMatch) return trainItem;

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
        private static bool includesWagons(Train train, List<string> wagonIdList)
        {
            foreach (var item in wagonIdList)
            {
                if (train.Cars.Find(car => car.CarID == item) == null) return false;
            }
            // train speed < 1
            return (Math.Abs(train.SpeedMpS) <= 1 ? true : false);
        }

        /// <summary>
        /// Like MSTS, do not check for unlisted wagons as the wagon list may be shortened for convenience to contain
        /// only the first and last wagon or even just the first wagon.
        /// </summary>
        /// <param name="train"></param>
        /// <param name="wagonIdList"></param>
        /// <returns>True if all listed wagons are not part of the given train.</returns>
        private static bool excludesWagons(Train train, List<string> wagonIdList)
        {
            // The Cars list is a global list that includes STATIC cars.  We need to make sure that the active train/car is processed only.
            if (train.TrainType == TrainType.Static)
                return true;

            bool lNotFound = false;
            foreach (var item in wagonIdList)
            {
                //take in count each item in wagonIdList 
                if (train.Cars.Find(car => car.CarID == item) == null)
                {
                    lNotFound = true; //wagon not part of the train
                }
                else
                {
                    lNotFound = false; break;//wagon still part of the train
                }
            }
            return lNotFound;
        }

        /// <summary>
        /// Like platforms, checking that one end of the train is within the siding.
        /// </summary>
        /// <param name="frontPosition"></param>
        /// <param name="rearPosition"></param>
        /// <param name="sidingEnd1"></param>
        /// <param name="sidingEnd2"></param>
        /// <returns>true if both ends of train within siding</returns>
        private static bool atSiding(Traveller frontPosition, Traveller rearPosition, SidingItem sidingEnd1, SidingItem sidingEnd2)
        {
            if (sidingEnd1 == null || sidingEnd2 == null)
            {
                return true;
            }

            TDBTravellerDistanceCalculatorHelper helper;
            TDBTravellerDistanceCalculatorHelper.DistanceResult distanceEnd1;
            TDBTravellerDistanceCalculatorHelper.DistanceResult distanceEnd2;

            // Front calcs
            helper = new TDBTravellerDistanceCalculatorHelper(frontPosition);

            distanceEnd1 = helper.CalculateToPoint(sidingEnd1.Location);
            distanceEnd2 = helper.CalculateToPoint(sidingEnd2.Location);

            // If front between the ends of the siding
            if (((distanceEnd1 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Behind
                && distanceEnd2 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Valid)
                || (distanceEnd1 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Valid
                && distanceEnd2 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Behind)))
            {
                return true;
            }

            // Rear calcs
            helper = new TDBTravellerDistanceCalculatorHelper(rearPosition);

            distanceEnd1 = helper.CalculateToPoint(sidingEnd1.Location);
            distanceEnd2 = helper.CalculateToPoint(sidingEnd2.Location);

            // If rear between the ends of the siding
            if (((distanceEnd1 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Behind
                && distanceEnd2 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Valid)
                || (distanceEnd1 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Valid
                && distanceEnd2 == TDBTravellerDistanceCalculatorHelper.DistanceResult.Behind)))
            {
                return true;
            }

            return false;
        }
    }

    public class EventCategoryLocationWrapper : EventWrapper
    {
        public EventCategoryLocationWrapper(Orts.Formats.Msts.Models.ActivityEvent @event, Simulator simulator)
            : base(@event, simulator)
        {
        }

        public override Boolean Triggered(Activity activity)
        {
            var triggered = false;
            var e = this.ParsedObject as Orts.Formats.Msts.Models.LocationActivityEvent;
            var train = Simulator.PlayerLocomotive.Train;
            if (!string.IsNullOrEmpty(ParsedObject.TrainService) && Train != null)
            {
                if (Train.FrontTDBTraveller == null) return triggered;
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
            var trainFrontPosition = new Traveller(train.NextRouteReady && train.TCRoute.ActiveSubPath > 0 && train.TCRoute.ReversalInfo[train.TCRoute.ActiveSubPath - 1].Valid ?
                train.RearTDBTraveller : train.FrontTDBTraveller); // just after reversal the old train front position must be considered
            var distance = trainFrontPosition.DistanceTo(e.Location, e.RadiusM);
            if (distance == -1)
            {
                trainFrontPosition.ReverseDirection();
                distance = trainFrontPosition.DistanceTo(e.Location, e.RadiusM);
                if (distance == -1)
                    return triggered;
            }
            if (distance < e.RadiusM) { triggered = true; }
            return triggered;
        }
    }

    public class EventCategoryTimeWrapper : EventWrapper
    {

        public EventCategoryTimeWrapper(Orts.Formats.Msts.Models.ActivityEvent @event, Simulator simulator)
            : base(@event, simulator)
        {
        }

        public override Boolean Triggered(Activity activity)
        {
            var e = this.ParsedObject as Orts.Formats.Msts.Models.TimeActivityEvent;
            if (e == null) return false;
            Train = Simulator.PlayerLocomotive.Train;
            var triggered = (e.Time <= (int)Simulator.ClockTime - activity.startTimeS);
            return triggered;
        }
    }

}
