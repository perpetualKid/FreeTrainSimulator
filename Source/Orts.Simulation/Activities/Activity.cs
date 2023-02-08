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
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;

using ActivityEvent = Orts.Formats.Msts.Models.ActivityEvent;

namespace Orts.Simulation.Activities
{
    public class ActivityEventArgs : EventArgs
    {
        public EventWrapper TriggeredEvent { get; }

        internal ActivityEventArgs(EventWrapper triggeredEvent)
        {
            TriggeredEvent = triggeredEvent;
        }
    }

    public class Activity
    {
        private readonly Simulator simulator;
        private bool reloadedActivityEvent;
        private EventWrapper triggeredEvent;
        private EventHandler<ActivityEventArgs> onActivityEventTriggered;

        // station stop logging flags - these are saved to resume correct logging after save
        private string stationStopLogFile;   // logfile name
        private bool stationStopLogActive;   // logging is active

        // Passenger tasks
        private double prevTrainSpeed = 1;  // set a start value above stop-limit (0.2) so if the train speed at activity start is below, this will trigger a Station-Stop
        internal int StartTime { get; private set; }    // Clock time in seconds when activity was launched.

#pragma warning disable CA1002 // Do not expose generic lists
        public List<ActivityTask> Tasks { get; } = new List<ActivityTask>();
#pragma warning restore CA1002 // Do not expose generic lists
        public ActivityTask ActivityTask { get; private set; }

        // Freight events
#pragma warning disable CA1002 // Do not expose generic lists
        public List<EventWrapper> EventList { get; } = new List<EventWrapper>();
#pragma warning restore CA1002 // Do not expose generic lists
        public bool Completed { get; private set; }          // true once activity is completed.
        public bool Succeeded { get; internal set; }        // status of completed activity

        public EventWrapper TriggeredActivityEvent { get; set; }        // used for exchange with Sound.cs to trigger activity sounds;

#pragma warning disable CA1002 // Do not expose generic lists
        public List<TempSpeedPostItem> TempSpeedPostItems { get; private set; }
#pragma warning restore CA1002 // Do not expose generic lists

        public bool WeatherChangesPresent { get; private set; } // tested in case of randomized activities to state wheter weather should be randomized

        public event EventHandler<ActivityEventArgs> OnEventTriggered
        {
#pragma warning disable CA1030 // Use events where appropriate
            add
            {
                onActivityEventTriggered += value;
                if (null != triggeredEvent)
                    value?.Invoke(this, new ActivityEventArgs(triggeredEvent));
            }
            remove
            {
                onActivityEventTriggered -= value;
            }
#pragma warning restore CA1030 // Use events where appropriate
        }

        private Activity(BinaryReader inf, Simulator simulator, List<EventWrapper> oldEventList, List<TempSpeedPostItem> tempSpeedPostItems)
        {
            TempSpeedPostItems = tempSpeedPostItems;
            this.simulator = simulator;
            Restore(inf, simulator, oldEventList);
        }

        public Activity(ActivityFile activityFile, Simulator simulator)
        {
            if (null == activityFile)
                throw new ArgumentNullException(nameof(activityFile));

            this.simulator = simulator;  // Save for future use.
            StartTime = (int)activityFile.Activity.Header.StartTime.TotalSeconds;
            PlayerServices sd;
            sd = activityFile.Activity.PlayerServices;
            if (sd != null)
            {
                if (sd.PlayerTraffics.Count > 0)
                {
                    ActivityTask task = null;

                    foreach (ServiceTrafficItem i in sd.PlayerTraffics)
                    {
                        PlatformItem Platform;
                        if (i.PlatformStartID < RuntimeData.Instance.TrackDB.TrackItems.Count && i.PlatformStartID >= 0 &&
                            RuntimeData.Instance.TrackDB.TrackItems[i.PlatformStartID] is PlatformItem)
                            Platform = RuntimeData.Instance.TrackDB.TrackItems[i.PlatformStartID] as PlatformItem;
                        else
                        {
                            Trace.TraceWarning("PlatformStartID {0} is not present in TDB file", i.PlatformStartID);
                            continue;
                        }
                        if (Platform != null)
                        {
                            if (RuntimeData.Instance.TrackDB.TrackItems[Platform.LinkedPlatformItemId] is PlatformItem)
                            {
                                PlatformItem Platform2 = RuntimeData.Instance.TrackDB.TrackItems[Platform.LinkedPlatformItemId] as PlatformItem;
                                Tasks.Add(task = new ActivityTaskPassengerStopAt(simulator, task, i.ArrivalTime, i.DepartTime, Platform, Platform2));
                            }
                        }
                    }
                    ActivityTask = Tasks[0];
                }
            }

            // Compile list of freight events, if any, from the parsed ACT file.
            foreach (ActivityEvent activityEvent in activityFile.Activity?.Events ?? Enumerable.Empty<ActivityEvent>())
            {
                if (activityEvent is ActionActivityEvent)
                    EventList.Add(new EventCategoryActionWrapper(activityEvent));
                if (activityEvent is LocationActivityEvent)
                    EventList.Add(new EventCategoryLocationWrapper(activityEvent));
                if (activityEvent is TimeActivityEvent)
                    EventList.Add(new EventCategoryTimeWrapper(activityEvent));
                EventWrapper eventAdded = EventList.Last();
                eventAdded.OriginalActivationLevel = activityEvent.ActivationLevel;
                if (activityEvent.WeatherChange != null || activityEvent.Outcomes.WeatherChange != null)
                    WeatherChangesPresent = true;
            }

            stationStopLogActive = false;
            stationStopLogFile = null;
        }

        public ActivityTask Last => Tasks.Count == 0 ? null : Tasks[^1];

        public bool IsFinished => Tasks.Count != 0 && Last.IsCompleted != null;

        public void Update()
        {
            if (!Completed && triggeredEvent == null)
            {
                foreach (EventWrapper item in EventList)
                {
                    if (item?.ActivityEvent.ActivationLevel > 0 && (item.TimesTriggered < 1 || item.ActivityEvent.Reversible))
                    {
                        if (item.Triggered(this))
                        {
                            if (!item.Disabled)
                            {
                                item.TimesTriggered += 1;
                                if (item.CompletesActivity(this))
                                    Completed = true;
                                triggeredEvent = item;
                                onActivityEventTriggered?.Invoke(this, new ActivityEventArgs(triggeredEvent));
                                simulator.GamePaused = true;
                                break;
                            }
                        }
                        else
                        {
                            if (item.ActivityEvent.Reversible)
                                // Reversible event is no longer triggered, so can re-enable it.
                                item.Disabled = false;
                        }
                    }
                }
            }
            else if (reloadedActivityEvent && triggeredEvent != null) //should happen first time only
            {
                reloadedActivityEvent = false;
                onActivityEventTriggered?.Invoke(this, new ActivityEventArgs(triggeredEvent));
                simulator.GamePaused = true;
            }
            else if (Completed && triggeredEvent == null)
            {
                triggeredEvent = new EventCategorySystemWrapper(triggeredEvent.ActivityEvent.Name, Simulator.Catalog.GetString($"This activity has ended {(Succeeded ? Simulator.Catalog.GetString("successfully") : Simulator.Catalog.GetString("without success"))}.\nFor a detailed evaluation, see the Help Window (F1)."));
                EventList.Add(triggeredEvent);
                onActivityEventTriggered?.Invoke(this, new ActivityEventArgs(triggeredEvent));
                simulator.GamePaused = true;
            }

            // Update passenger tasks
            if (ActivityTask == null)
                return;

            ActivityTask.NotifyEvent(ActivityEventType.Timer);
            if (ActivityTask.IsCompleted != null)    // Surely this doesn't test for: 
                ActivityTask = ActivityTask.NextTask;

            if (simulator.OriginalPlayerTrain.TrainType == TrainType.Player || simulator.OriginalPlayerTrain.TrainType == TrainType.AiPlayerDriven)
            {
                if (Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS) < 0.2f)
                {
                    if (prevTrainSpeed >= 0.2f)
                    {
                        prevTrainSpeed = 0;
                        ActivityTask.NotifyEvent(ActivityEventType.TrainStop);
                        if (ActivityTask.IsCompleted != null)
                            ActivityTask = ActivityTask.NextTask;
                    }
                }
                else
                {
                    if (prevTrainSpeed < 0.2f && Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS) >= 0.2f)
                    {
                        prevTrainSpeed = Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS);
                        ActivityTask.NotifyEvent(ActivityEventType.TrainStart);
                        if (ActivityTask.IsCompleted != null)
                            ActivityTask = ActivityTask.NextTask;
                    }
                }
            }
            else
            {
                if (Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS) <= Simulator.MaxStoppedMpS)
                {
                    if (prevTrainSpeed != 0)
                    {
                        prevTrainSpeed = 0;
                        ActivityTask.NotifyEvent(ActivityEventType.TrainStop);
                        if (ActivityTask.IsCompleted != null)
                            ActivityTask = ActivityTask.NextTask;
                    }
                }
                else
                {
                    if (Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS) > 0.2f)
                    {
                        prevTrainSpeed = Math.Abs(simulator.OriginalPlayerTrain.SpeedMpS);
                        ActivityTask.NotifyEvent(ActivityEventType.TrainStart);
                        if (ActivityTask.IsCompleted != null)
                            ActivityTask = ActivityTask.NextTask;
                    }
                }
            }
        }

        public void AcknowledgeEvent(EventWrapper activityEvent)
        {
            simulator.GamePaused = false;

            if (activityEvent == null)
                return;

            if (triggeredEvent != activityEvent)
            {
                Trace.TraceError($"Failed to acknowledge Activity Event {activityEvent.ActivityEvent.Name}{activityEvent.ActivityEvent.ID}. Excepted {triggeredEvent?.ActivityEvent.Name}{triggeredEvent?.ActivityEvent.ID}");
                return;
            }

            triggeredEvent = null;
        }

        public void SendActivityMessage(string header, string text)
        {
            EventList.Add(new EventCategorySystemWrapper(header, text));
        }

        public static void Save(BinaryWriter outf, Activity act)
        {
            if (outf == null)
                throw new ArgumentNullException(nameof(outf));

            if (act == null)
                outf.Write(-1);
            else
            {
                outf.Write(1);
                act.Save(outf);
            }
        }

        public static Activity Restore(BinaryReader inf, Simulator simulator, Activity activity)

        {
            if (inf == null)
                throw new ArgumentNullException(nameof(inf));

            if (inf.ReadInt32() == -1)
                return null;
            else
                // Retain the old EventList. It's full of static data so save and restore is a waste of effort
                return new Activity(inf, simulator, activity?.EventList ?? throw new ArgumentNullException(nameof(activity)), activity.TempSpeedPostItems);
        }

        public void Save(BinaryWriter outf)
        {
            if (outf == null)
                throw new ArgumentNullException(nameof(outf));
            int noval = -1;

            // Save passenger activity
            outf.Write(Tasks.Count);
            foreach (ActivityTask task in Tasks)
                task.Save(outf);
            if (ActivityTask == null)
                outf.Write(noval);
            else
                outf.Write(Tasks.IndexOf(ActivityTask));
            outf.Write(prevTrainSpeed);

            // Save freight activity
            outf.Write(Completed);
            outf.Write(Succeeded);
            outf.Write((int)StartTime);
            foreach (EventWrapper e in EventList)
                e.Save(outf);
            if (triggeredEvent == null)
                outf.Write(false);
            else
            {
                outf.Write(true);
                outf.Write(EventList.IndexOf(triggeredEvent));
            }

            // write log details
            outf.Write(stationStopLogActive);
            if (stationStopLogActive)
                outf.Write(stationStopLogFile);
        }

        private void Restore(BinaryReader inf, Simulator simulator, List<EventWrapper> oldEventList)
        {
            int rdval;

            // Restore passenger activity
            ActivityTask task;
            rdval = inf.ReadInt32();
            for (int i = 0; i < rdval; i++)
            {
                task = inf.ReadInt32() == 1 ? new ActivityTaskPassengerStopAt(simulator) : (ActivityTask)null;
                task.Restore(inf);
                Tasks.Add(task);
            }
            rdval = inf.ReadInt32();
            ActivityTask = rdval == -1 ? null : Tasks[rdval];
            prevTrainSpeed = inf.ReadDouble();

            task = null;
            for (int i = 0; i < Tasks.Count; i++)
            {
                Tasks[i].PrevTask = task;
                if (task != null)
                    task.NextTask = Tasks[i];
                task = Tasks[i];
            }

            // Restore freight activity
            Completed = inf.ReadBoolean();
            Succeeded = inf.ReadBoolean();
            StartTime = inf.ReadInt32();

            EventList.Clear();
            foreach (EventWrapper item in oldEventList)
                EventList.Add(item);
            foreach (EventWrapper e in EventList)
                e.Restore(inf);

            if (reloadedActivityEvent = inf.ReadBoolean())
                triggeredEvent = EventList[inf.ReadInt32()];

            // restore logging info
            stationStopLogActive = inf.ReadBoolean();
            if (stationStopLogActive)
            {
                stationStopLogFile = inf.ReadString();

                foreach (ActivityTaskPassengerStopAt stopTask in Tasks.OfType<ActivityTaskPassengerStopAt>())
                    stopTask.SetLogStationStop(stationStopLogFile);
            }
            else
                stationStopLogFile = null;
        }

        public void StartStationLogging(string stationLogFile)
        {
            stationStopLogFile = stationLogFile;
            stationStopLogActive = true;

            StringBuilder stringBuild = new StringBuilder();

            char separator = (char)simulator.Settings.DataLoggerSeparator;

            stringBuild.Append("STATION");
            stringBuild.Append(separator);
            stringBuild.Append("BOOKED ARR");
            stringBuild.Append(separator);
            stringBuild.Append("BOOKED DEP");
            stringBuild.Append(separator);
            stringBuild.Append("ACTUAL ARR");
            stringBuild.Append(separator);
            stringBuild.Append("ACTUAL DEP");
            stringBuild.Append(separator);
            stringBuild.Append("DELAY");
            stringBuild.Append(separator);
            stringBuild.Append("STATE");
            stringBuild.Append('\n');
            File.AppendAllText(stationStopLogFile, stringBuild.ToString());

            foreach (ActivityTaskPassengerStopAt stopTask in Tasks.OfType<ActivityTaskPassengerStopAt>())
                stopTask.SetLogStationStop(stationStopLogFile);
        }

        /// <summary>
        /// Add speedposts to the track database for each Temporary Speed Restriction zone
        /// </summary>
        /// <param name="routeFile"></param>
        /// <param name="zones">List of speed restriction zones</param>
        internal void AddRestrictZones(Route routeFile, RestrictedSpeedZones zones)
        {
            if (zones == null)
                throw new ArgumentNullException(nameof(zones));

            if (zones.Count < 1)
                return;

            TempSpeedPostItems = new List<TempSpeedPostItem>();

            TrackItem[] newSpeedPostItems = new TempSpeedPostItem[2];

            const float MaxDistanceOfWarningPost = 2000;

            for (int idxZone = 0; idxZone < zones.Count; idxZone++)
            {
                newSpeedPostItems[0] = new TempSpeedPostItem(routeFile,
                    zones[idxZone].StartPosition, true, WorldPosition.None, false);
                newSpeedPostItems[1] = new TempSpeedPostItem(routeFile,
                    zones[idxZone].EndPosition, false, WorldPosition.None, false);

                // Add the speedposts to the track database. This will set the TrItemId's of all speedposts
                RuntimeData.Instance.TrackDB.AddTrackItems(newSpeedPostItems);

                // And now update the various (vector) tracknodes (this needs the TrItemIds.
                float? endOffset = AddItemIdToTrackNode(zones[idxZone].EndPosition, newSpeedPostItems[1], out _);
                float? startOffset = AddItemIdToTrackNode(zones[idxZone].StartPosition, newSpeedPostItems[0], out Traveller traveller);
                float distanceOfWarningPost = 0;

                if (startOffset != null && endOffset != null && startOffset > endOffset)
                {
                    ((TempSpeedPostItem)newSpeedPostItems[0]).Flip();
                    ((TempSpeedPostItem)newSpeedPostItems[1]).Flip();
                    distanceOfWarningPost = (float)Math.Min(MaxDistanceOfWarningPost, traveller.TrackNodeLength - (double)startOffset);
                }
                else if (startOffset != null && endOffset != null && startOffset <= endOffset)
                    distanceOfWarningPost = (float)Math.Max(-MaxDistanceOfWarningPost, -(double)startOffset);
                traveller.Move(distanceOfWarningPost);
                WorldPosition worldPosition3 = WorldPosition.None;
                TempSpeedPostItem speedWarningPostItem = new TempSpeedPostItem(routeFile, zones[idxZone].StartPosition, false, worldPosition3, true);
                SpeedPostPosition(speedWarningPostItem, ref traveller);
                if (startOffset != null && endOffset != null && startOffset > endOffset)
                    speedWarningPostItem.Flip();
                ((TempSpeedPostItem)newSpeedPostItems[0]).ComputeTablePosition();
                TempSpeedPostItems.Add((TempSpeedPostItem)newSpeedPostItems[0]);
                ((TempSpeedPostItem)newSpeedPostItems[1]).ComputeTablePosition();
                TempSpeedPostItems.Add((TempSpeedPostItem)newSpeedPostItems[1]);
                speedWarningPostItem.ComputeTablePosition();
                TempSpeedPostItems.Add(speedWarningPostItem);
            }
        }

        /// <summary>
        /// Add a reference to a new TrItemId to the correct trackNode (which needs to be determined from the position)
        /// </summary>
        /// <param name="location">Position of the new </param>
        /// <param name="newTrItem">The Id of the new TrItem to add to the tracknode</param>
        /// <param name="traveller">The computed traveller to the speedPost position</param>
        private static float? AddItemIdToTrackNode(in WorldLocation location, TrackItem newTrItem, out Traveller traveller)
        {
            float? offset = 0.0f;
            traveller = new Traveller(location);
            if (traveller.TrackNode is TrackVectorNode trackVectorNode)
            {
                offset = traveller.TrackNodeOffset;
                SpeedPostPosition((TempSpeedPostItem)newTrItem, ref traveller);
                InsertTrackItemRef(trackVectorNode, (int)newTrItem.TrackItemId, (float)offset);
            }
            return offset;
        }

        /// <summary>
        /// Determine position parameters of restricted speed Post
        /// </summary>
        /// <param name="restrSpeedPost">The Id of the new restricted speed post to position</param>
        /// <param name="traveller">The traveller to the speedPost position</param>
        /// 
        private static void SpeedPostPosition(TempSpeedPostItem restrSpeedPost, ref Traveller traveller)
        {
            restrSpeedPost.Update(traveller.Y, -traveller.RotY + (float)Math.PI / 2, new WorldPosition(traveller.TileX, traveller.TileZ, MatrixExtension.SetTranslation(Matrix.CreateFromYawPitchRoll(-traveller.RotY, 0, 0), traveller.X, traveller.Y, -traveller.Z)));
        }

        /// <summary>
        /// Insert a reference to a new TrItem to the already existing TrItemRefs basing on its offset within the track node.
        /// </summary>
        /// 
        private static void InsertTrackItemRef(TrackVectorNode thisVectorNode, int newTrItemId, float offset)
        {
            int index = 0;
            // insert the new TrItemRef accordingly to its offset
            for (int iTrItems = thisVectorNode.TrackItemIndices.Length - 1; iTrItems >= 0; iTrItems--)
            {
                int currTrItemID = thisVectorNode.TrackItemIndices[iTrItems];
                TrackItem currTrItem = RuntimeData.Instance.TrackDB.TrackItems[currTrItemID];
                Traveller traveller = new Traveller(currTrItem.Location);
                if (offset >= traveller.TrackNodeOffset)
                {
                    index = iTrItems + 1;
                    break;
                }
            }
            thisVectorNode.InsertTrackItemIndex(newTrItemId, index);
        }

        internal void AssociateEvents(Train train)
        {
            foreach (EventWrapper eventWrapper in EventList)
                if (eventWrapper is EventCategoryLocationWrapper && !string.IsNullOrEmpty(eventWrapper.ActivityEvent.TrainService) &&
                    eventWrapper.ActivityEvent.TrainService.Equals(train.Name, StringComparison.OrdinalIgnoreCase))
                    if (eventWrapper.ActivityEvent.TrainStartingTime == -1 || (train as AITrain).ServiceDefinition.Time == eventWrapper.ActivityEvent.TrainStartingTime)
                        eventWrapper.Train = train;
        }
    }
}
