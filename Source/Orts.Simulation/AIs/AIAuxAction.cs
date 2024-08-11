// COPYRIGHT 2013 by the Open Rails project.
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

/* AI
 * 
 * Contains code to initialize and control AI trains.
 * 
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.State;

using Orts.Formats.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;
using Orts.Simulation.World;

namespace Orts.Simulation.AIs
{
    #region AuxActionsContainer

    /// <summary>
    /// AuxActionsContainer
    /// Used to manage all the action ref object.
    /// </summary>
    internal class AuxActionsContainer :
        ISaveStateRestoreApi<AuxActionRefSaveState, AuxActionRef>
    {
        private readonly Train train;
        private readonly List<KeyValuePair<Type, AuxActionRef>> genericFunctions = new List<KeyValuePair<Type, AuxActionRef>>();
        private readonly DistanceTravelledActions genericRequiredActions = new DistanceTravelledActions(); // distance travelled Generic action list for AITrain

        public DistanceTravelledActions SpecificRequiredActions { get; } = new DistanceTravelledActions();
        public List<AuxActionRef> SpecAuxActions { get; } = new List<AuxActionRef>();          // Actions To Do during activity, like WP with specific location

        public AuxActionsContainer(Train train)
        {

            if (train is AITrain aiTrain)
            {
                SetGenAuxActions(aiTrain);
            }
            this.train = train;
        }

        AuxActionRef ISaveStateRestoreApi<AuxActionRefSaveState, AuxActionRef>.CreateRuntimeTarget(AuxActionRefSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            return saveState.NextAction switch
            {
                AuxiliaryAction.WaitingPoint => new AIActionWPRef(),
                AuxiliaryAction.SoundHorn => new AIActionHornRef(),
                AuxiliaryAction.SignalDelegate => new AIActSigDelegateRef(saveState, SpecAuxActions),
                _ => null,
            };
        }

        public void PreSave()
        {
            int currentClock = (int)Simulator.Instance.ClockTime;
            AITrain aiTrain = train as AITrain;
            if (SpecAuxActions.Count > 0 && SpecAuxActions[0] != null &&
                    SpecificRequiredActions.First != null && SpecificRequiredActions.First.Value is AuxActSigDelegate)

                // SigDelegate WP is running

                if (((AuxActSigDelegate)SpecificRequiredActions.First.Value).currentMvmtState == AiMovementState.HandleAction &&
                    !(((AuxActSigDelegate)SpecificRequiredActions.First.Value).ActionRef as AIActSigDelegateRef).IsAbsolute)
                {
                    int remainingDelay = ((AuxActSigDelegate)SpecificRequiredActions.First.Value).ActualDepart - currentClock;
                    AIActSigDelegateRef actionRef = ((AuxActSigDelegate)SpecificRequiredActions.First.Value).ActionRef as AIActSigDelegateRef;
                    if (actionRef.AssociatedWPAction != null)
                        actionRef.AssociatedWPAction.SetDelay(remainingDelay);
                    actionRef.Delay = remainingDelay;
                }
            if (!(train == Simulator.Instance.OriginalPlayerTrain && (train.TrainType == TrainType.AiPlayerDriven ||
                train.TrainType == TrainType.AiPlayerHosting || train.TrainType == TrainType.Player || train.TrainType == TrainType.Ai)))
            {

                if (train is AITrain && ((aiTrain.MovementState == AiMovementState.HandleAction && aiTrain.nextActionInfo != null &&
                aiTrain.nextActionInfo.NextAction == AiActionType.AuxiliaryAction && aiTrain.nextActionInfo is AuxActionWPItem)
                || (aiTrain.AuxActionsContainer.SpecAuxActions.Count > 0 &&
                aiTrain.AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef && (aiTrain.AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).WaitingPoint != null &&
                (aiTrain.AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).WaitingPoint.currentMvmtState == AiMovementState.HandleAction)))
                // WP is running
                {
                    // Do nothing if it is an absolute WP
                    if (!(aiTrain.AuxActionsContainer.SpecAuxActions.Count > 0 && aiTrain.AuxActionsContainer.SpecAuxActions[0] is AIActionWPRef &&
                        (aiTrain.AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).Delay >= 30000 && (aiTrain.AuxActionsContainer.SpecAuxActions[0] as AIActionWPRef).Delay < 40000))
                    {
                        int remainingDelay;
                        if (aiTrain.nextActionInfo is AuxActionWPItem auxActionWPItem)
                            remainingDelay = auxActionWPItem.ActualDepart - currentClock;
                        else
                            remainingDelay = ((AIActionWPRef)SpecAuxActions[0]).WaitingPoint.ActualDepart - currentClock;
                        ((AIActionWPRef)SpecAuxActions[0]).SetDelay(remainingDelay);
                    }
                }
            }
        }

        protected void SetGenAuxActions(AITrain thisTrain)  //  Add here the new Generic Action
        {
            Formats.Msts.Files.ActivityFile activity = Simulator.Instance.ActivityFile;
            if (activity != null && activity.Activity.AIBlowsHornAtLevelCrossings && SpecAuxActions.Count == 0)
            {
                AuxActionHorn auxActionHorn = new AuxActionHorn(true, 2, 0, activity.Activity.AILevelCrossingHornPattern);
                AIActionHornRef horn = new AIActionHornRef(thisTrain, auxActionHorn);
                List<KeyValuePair<Type, AuxActionRef>> listInfo = horn.GetCallFunction();
                foreach (var function in listInfo)
                    genericFunctions.Add(function);
            }
        }

        //public bool CheckGenActions(System.Type typeSource, float rearDist, float frontDist, WorldLocation location, uint trackNodeIndex)
        public bool CheckGenActions(Type typeSource, in WorldLocation location, params object[] list)
        {
            if (train is AITrain)
            {
                AITrain aiTrain = train as AITrain;
                foreach (KeyValuePair<Type, AuxActionRef> function in genericFunctions)
                {
                    if (typeSource == function.Key)   //  Caller object is a LevelCrossing
                    {
                        AIAuxActionsRef called = (AIAuxActionsRef)function.Value;
                        if (called.HasAction(train.Number, location))
                            return false;
                        AIActionItem newAction = called.CheckGenActions(location, aiTrain, list);
                        if (newAction != null)
                        {
                            if (newAction is AuxActionWPItem)
                                SpecificRequiredActions.InsertAction(newAction);
                            else
                                genericRequiredActions.InsertAction(newAction);
                        }
                    }
                }
            }
            return false;
        }

        public void RemoveSpecReqAction(AuxActionItem thisAction)
        {
            if (thisAction.CanRemove(train))
            {
                DistanceTravelledItem thisItem = thisAction;
                SpecificRequiredActions.Remove(thisItem);
            }
        }

        public void ProcessGenAction(AITrain thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            if (genericRequiredActions.Count <= 0 || !(train is AITrain))
                return;
            AITrain aiTrain = train as AITrain;
            List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();
            foreach (var action in genericRequiredActions)
            {
                AIActionItem actionItem = action as AIActionItem;
                if (actionItem.RequiredDistance <= train.DistanceTravelledM)
                {
                    itemList.Add(actionItem);
                }
            }
            foreach (var action in itemList)
            {
                AIActionItem actionItem = action as AIActionItem;
                actionItem.ProcessAction(aiTrain, presentTime, elapsedClockSeconds, movementState);
            }
        }

        public AiMovementState ProcessSpecAction(AITrain thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            AiMovementState MvtState = movementState;
            if (SpecificRequiredActions.Count <= 0 || !(train is AITrain))
                return MvtState;
            AITrain aiTrain = train as AITrain;
            List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();
            foreach (var action in SpecificRequiredActions)
            {
                AIActionItem actionItem = action as AIActionItem;
                if (actionItem.RequiredDistance >= train.DistanceTravelledM)
                    continue;
                if (actionItem is AuxActSigDelegate)
                {
                    var actionRef = (actionItem as AuxActSigDelegate).ActionRef;
                    if ((actionRef as AIActSigDelegateRef).IsAbsolute)
                        continue;
                }
                itemList.Add(actionItem);
            }
            foreach (var action in itemList)
            {
                AiMovementState tmpMvt;
                AIActionItem actionItem = action as AIActionItem;
                tmpMvt = actionItem.ProcessAction(aiTrain, presentTime, elapsedClockSeconds, movementState);
                if (tmpMvt != movementState)
                    MvtState = tmpMvt;  //  Try to avoid override of changed state of previous action
            }
            return MvtState;
        }

        public void Remove(AuxActionItem action)
        {
            bool ret = false;
            bool remove = true;
            if (action.ActionRef.GenericAction)
            {
                if (genericRequiredActions.Count > 0)
                    ret = genericRequiredActions.Remove(action);
                if (!ret && SpecificRequiredActions.Count > 0)
                {
                    if (((AIAuxActionsRef)action.ActionRef).CallFreeAction(train))
                        RemoveSpecReqAction(action);
                }
            }
            if (action.ActionRef.ActionType == AuxiliaryAction.SoundHorn)
            {
                if (SpecificRequiredActions.Contains(action))
                    RemoveSpecReqAction(action);
                else
                    remove = false;
            }
            if (CountSpec() > 0 && remove == true)
                SpecAuxActions.Remove(action.ActionRef);
            if (train is AITrain)
                ((AITrain)train).ResetActions(true);
        }

        public void RemoveAt(int posit)
        {
            SpecAuxActions.RemoveAt(posit);
        }

        public AuxActionRef this[int key]
        {
            get
            {
                if (key >= SpecAuxActions.Count)
                    return null;
                return SpecAuxActions[key];
            }
            set
            {

            }
        }

        public int Count()
        {
            return CountSpec();
        }

        public int CountSpec()
        {
            return SpecAuxActions.Count;
        }

        //================================================================================================//
        //  SPA:    Added for use with new AIActionItems
        /// <summary>
        /// Create Specific Auxiliary Action, like WP
        /// <\summary>
        public void SetAuxAction(Train thisTrain)
        {
            if (SpecAuxActions.Count <= 0)
                return;
            AIAuxActionsRef thisAction;
            int specAuxActionsIndex = 0;
            bool requiredActionsInserted = false;
            while (specAuxActionsIndex <= SpecAuxActions.Count - 1)
            {
                while (SpecAuxActions.Count > 0)
                {
                    thisAction = (AIAuxActionsRef)SpecAuxActions[specAuxActionsIndex];

                    if (thisAction.SubrouteIndex > thisTrain.TCRoute.ActiveSubPath)
                    {
                        return;
                    }
                    if (thisAction.SubrouteIndex == thisTrain.TCRoute.ActiveSubPath)
                        break;
                    else
                    {
                        SpecAuxActions.RemoveAt(0);
                        if (SpecAuxActions.Count <= 0)
                            return;
                    }
                }

                thisAction = (AIAuxActionsRef)SpecAuxActions[specAuxActionsIndex];
                bool validAction = false;
                float[] distancesM;
                while (!validAction)
                {
                    if (thisTrain is AITrain && thisTrain.TrainType != TrainType.AiPlayerDriven)
                    {
                        AITrain aiTrain = thisTrain as AITrain;
                        distancesM = thisAction.CalculateDistancesToNextAction(aiTrain, ((AITrain)aiTrain).TrainMaxSpeedMpS, true);
                    }
                    else
                    {
                        distancesM = thisAction.CalculateDistancesToNextAction(thisTrain, thisTrain.SpeedMpS, true);
                    }
                    //<CSComment> Next block does not seem useful. distancesM[0] includes distanceTravelledM, so it practically can be 0 only at start of game
                    /*                if (distancesM[0]< 0f)
                                    {
                                        SpecAuxActions.RemoveAt(0);
                                        if (SpecAuxActions.Count == 0)
                                        {
                                            return;
                                        }

                                        thisAction = (AIAuxActionsRef)SpecAuxActions[0];
                                        if (thisAction.SubrouteIndex > thisTrain.TCRoute.activeSubpath) return;
                                    }
                                    else */
                    {
                        float requiredSpeedMpS = 0;
                        if (requiredActionsInserted && ((thisAction is AIActSigDelegateRef && !((AIActSigDelegateRef)thisAction).IsAbsolute)))
                            return;
                        validAction = true;
                        AIActionItem newAction = ((AIAuxActionsRef)SpecAuxActions[specAuxActionsIndex]).Handler(distancesM[1], requiredSpeedMpS, distancesM[0], thisTrain.DistanceTravelledM);
                        if (newAction != null)
                        {
                            if (thisTrain is AITrain && newAction is AuxActionWPItem)   // Put only the WP for AI into the requiredAction, other are on the container
                            {
                                bool found = false;
                                requiredActionsInserted = true;
                                if ((thisTrain.TrainType == TrainType.AiPlayerDriven || thisTrain.TrainType == TrainType.AiPlayerHosting) && thisTrain.RequiredActions.Count > 0)
                                {
                                    // check if action already inserted
                                    foreach (DistanceTravelledItem item in thisTrain.RequiredActions)
                                    {
                                        if (item is AuxActionWPItem)
                                        {
                                            found = true;
                                            continue;
                                        }
                                    }
                                }
                                if (!found)
                                {
                                    thisTrain.RequiredActions.InsertAction(newAction);
                                    continue;
                                    //                              ((AITrain)thisTrain).nextActionInfo = newAction; // action must be restored through required actions only
                                }
                            }
                            else
                            {
                                SpecificRequiredActions.InsertAction(newAction);
                                if (newAction is AuxActionWPItem || newAction is AuxActSigDelegate)
                                    return;
                            }
                        }
                    }
                }
                specAuxActionsIndex++;
            }
        }

        //================================================================================================//
        //  
        /// <summary>
        /// Reset WP Aux Action, if any
        /// <\summary>

        public void ResetAuxAction(Train thisTrain)
        {
            if (SpecAuxActions.Count <= 0)
                return;
            AIAuxActionsRef thisAction;
            thisAction = (AIAuxActionsRef)SpecAuxActions[0];
            if (thisAction.SubrouteIndex != thisTrain.TCRoute.ActiveSubPath)
                return;
            thisAction.LinkedAuxAction = false;
            return;
        }

        //================================================================================================//
        //  
        /// <summary>
        /// Move next Aux Action, if in same section, under train in case of decoupling
        /// <\summary>
        public void MoveAuxAction(Train thisTrain)
        {
            AITrain thisAITrain = (AITrain)thisTrain;
            if (SpecAuxActions.Count <= 0)
                return;
            AIAuxActionsRef thisAction;
            thisAction = (AIAuxActionsRef)SpecAuxActions[0];
            if (thisAction is AIActionWPRef && thisAction.SubrouteIndex == thisTrain.TCRoute.ActiveSubPath && thisAction.TCSectionIndex == thisTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex)
            // Waiting point is just in the same section where the train is; move it under the train
            {
                AuxActionWPItem thisWPItem;
                if (thisAITrain.nextActionInfo != null && thisAITrain.nextActionInfo is AuxActionWPItem)
                {
                    thisWPItem = (AuxActionWPItem)thisAITrain.nextActionInfo;
                    if (thisWPItem.ActionRef == thisAction)
                    {
                        thisWPItem.ActivateDistanceM = thisTrain.PresentPosition[Direction.Forward].DistanceTravelled - 5;
                        thisAction.LinkedAuxAction = true;
                    }
                }
                thisAction.RequiredDistance = thisTrain.PresentPosition[Direction.Forward].Offset - 5;
            }
        }

        //================================================================================================//
        //  
        /// <summary>
        /// Move next Aux Action, if in same section and in next subpath (reversal in between), under train in case of decoupling
        /// <\summary>
        public void MoveAuxActionAfterReversal(Train thisTrain)
        {
            if (SpecAuxActions.Count <= 0)
                return;
            AIAuxActionsRef thisAction;
            thisAction = (AIAuxActionsRef)SpecAuxActions[0];
            if (thisAction is AIActionWPRef && thisAction.SubrouteIndex == thisTrain.TCRoute.ActiveSubPath + 1 && thisAction.TCSectionIndex == thisTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex)
            // Waiting point is just in the same section where the train is; move it under the train
            {
                int thisSectionIndex = thisTrain.PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
                thisAction.RequiredDistance = thisSection.Length - thisTrain.PresentPosition[Direction.Backward].Offset - 5;
            }
        }

        public void Add(AuxActionRef action)
        {
            SpecAuxActions.Add(action);
        }
    }

    #endregion

    #region AuxActionRef

    ////================================================================================================//
    ///// <summary>
    ///// AuxActionRef
    ///// info used to figure out one auxiliary action along the route.  It's a reference data, not a run data.
    ///// </summary>

    public class AIAuxActionsRef : AuxActionRef
    {
        public int SubrouteIndex;
        public int RouteIndex;
        public int TCSectionIndex;
        public Direction Direction { get; private set; }
        protected int TriggerDistance;
        public bool LinkedAuxAction;
        protected List<KeyValuePair<int, WorldLocation>> AskingTrain;
        public Signal SignalReferenced;
        public float RequiredSpeedMpS;
        public float RequiredDistance;
        public int Delay;
        public int EndSignalIndex { get; protected set; }

        public AuxiliaryAction NextAction = AuxiliaryAction.None;

        //================================================================================================//
        /// <summary>
        /// AIAuxActionsRef: Generic Constructor
        /// The specific datas are used to fire the Action.
        /// </summary>

        public AIAuxActionsRef()
        {
        }

        public AIAuxActionsRef(Train train, float distance, float requiredSpeedMpS, int subrouteIdx, int routeIdx, 
            int sectionIdx, Direction direction, AuxiliaryAction actionType = AuxiliaryAction.None) :
            base(actionType, false)                 //null, requiredSpeedMpS, , -1, )
        {
            RequiredDistance = distance;
            RequiredSpeedMpS = requiredSpeedMpS;
            SubrouteIndex = subrouteIdx;
            RouteIndex = routeIdx;
            TCSectionIndex = sectionIdx;
            Direction = direction;
            AskingTrain = new List<KeyValuePair<int, WorldLocation>>();
            GenericAction = false;
            EndSignalIndex = -1;
        }

        public override async ValueTask Restore([NotNull] AuxActionRefSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            RequiredSpeedMpS = saveState.RequiredSpeed;
            RequiredDistance = saveState.RequiredDistance;
            RouteIndex = saveState.RouteIndex;
            SubrouteIndex = saveState.SubRouteIndex;
            TCSectionIndex = saveState.TrackCircuitSectionIndex;
            Direction = saveState.Direction;
            TriggerDistance = saveState.TriggerDistance;
            GenericAction = saveState.GenericAction;
            AskingTrain = new List<KeyValuePair<int, WorldLocation>>();
            EndSignalIndex = saveState.EndSignalIndex;
            if (EndSignalIndex >= 0)
                SetSignalObject(Simulator.Instance.SignalEnvironment.Signals[EndSignalIndex]);
            else
                SetSignalObject(null);
            ActionType = saveState.NextAction;
        }

        public virtual List<KeyValuePair<Type, AuxActionRef>> GetCallFunction()
        {
            return default(List<KeyValuePair<Type, AuxActionRef>>);
        }

        //================================================================================================//
        /// <summary>
        /// Handler
        /// Like a fabric, if other informations are needed, please define specific function that can be called on the new object
        /// </summary>


        internal virtual AIActionItem Handler(params object[] list)
        {
            AIActionItem info = null;
            if (!LinkedAuxAction || GenericAction)
            {
                info = new AuxActionItem(this, AiActionType.AuxiliaryAction);
                info.SetParam((float)list[0], (float)list[1], (float)list[2], (float)list[3]);

                //info = new AuxActionItem(distance, speed, activateDistance, insertedDistance,
                //                this, AIActionItem.AI_ACTION_TYPE.AUX_ACTION);
            }
            return info;
        }

        //================================================================================================//
        /// <summary>
        /// CalculateDistancesToNextAction
        /// PLease, don't use the default function, redefine it.
        /// </summary>

        public virtual float[] CalculateDistancesToNextAction(Train thisTrain, float presentSpeedMpS, bool reschedule)
        {
            float[] distancesM = new float[2];
            distancesM[1] = 0.0f;
            distancesM[0] = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled;

            return distancesM;
        }

        public virtual float[] GetActivationDistances(Train train) => new float[2] { float.MaxValue, float.MaxValue };

        public override async ValueTask<AuxActionRefSaveState> Snapshot()
        {
            AuxActionRefSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.NextAction = NextAction;
            saveState.RequiredSpeed = RequiredSpeedMpS;
            saveState.RequiredDistance = RequiredDistance;
            saveState.RouteIndex = RouteIndex;
            saveState.SubRouteIndex = SubrouteIndex;
            saveState.TrackCircuitSectionIndex = TCSectionIndex;
            saveState.Direction = Direction;
            saveState.TriggerDistance = TriggerDistance;
            saveState.GenericAction = GenericAction;
            saveState.EndSignalIndex = EndSignalIndex;
            return saveState;
        }

        //================================================================================================//
        //
        // Restore
        //

        public void Register(int trainNumber, in WorldLocation location)
        {
            AskingTrain.Add(new KeyValuePair<int, WorldLocation>(trainNumber, location));
        }

        //public bool CheckGenActions(System.Type typeSource, float rearDist, float frontDist, WorldLocation location, uint trackNodeIndex)
        internal virtual AIActionItem CheckGenActions(in WorldLocation location, AITrain thisTrain, params object[] list)
        {
            return null;
        }

        public bool HasAction(int trainNumber, in WorldLocation location)
        {
            foreach (var info in AskingTrain)
            {
                int number = (int)info.Key;
                if (number == trainNumber)
                {
                    WorldLocation locationRegistered = info.Value;
                    if (location == locationRegistered)
                        return true;
                }
            }
            return false;
        }

        public virtual bool CallFreeAction(Train ThisTrain)
        {
            return true;
        }

        public void SetSignalObject(Signal signal)
        {
            SignalReferenced = signal;
        }
    }

    /// <summary>
    /// AIActionWPRef
    /// info used to figure out a Waiting Point along the route.
    /// </summary>
    internal class AIActionWPRef : AIAuxActionsRef
    {
        public AuxActionWPItem WaitingPoint { get; private set; }

        public AIActionWPRef() { }

        public AIActionWPRef(Train thisTrain, float distance, float requiredSpeedMpS, int subrouteIdx, int routeIdx, int sectionIdx, Direction direction)
            : base(thisTrain, distance, requiredSpeedMpS, subrouteIdx, routeIdx, sectionIdx, direction, AuxiliaryAction.WaitingPoint)
        {
            NextAction = AuxiliaryAction.WaitingPoint;
        }

        public override async ValueTask<AuxActionRefSaveState> Snapshot()
        {
            AuxActionRefSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.Delay = Delay;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] AuxActionRefSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ActionType = AuxiliaryAction.WaitingPoint;
            Delay = saveState.Delay;
            NextAction = AuxiliaryAction.WaitingPoint;
        }

        internal override AIActionItem Handler(params object[] list)
        {
            AIActionItem info = null;
            if (!LinkedAuxAction || GenericAction)
            {
                LinkedAuxAction = true;
                WaitingPoint = new AuxActionWPItem(this, AiActionType.AuxiliaryAction);
                WaitingPoint.SetParam((float)list[0], (float)list[1], (float)list[2], (float)list[3]);
                WaitingPoint.SetDelay(Delay);
                info = WaitingPoint;
            }
            else if (LinkedAuxAction)
            {
                info = WaitingPoint;
            }
            return info;
        }

        internal override AIActionItem CheckGenActions(in WorldLocation location, AITrain thisTrain, params object[] list)
        {
            AIActionItem newAction = null;
            int SpeedMps = (int)thisTrain.SpeedMpS;
            if (Math.Abs(SpeedMps) <= Simulator.MaxStoppedMpS)   //  We call the handler to generate an actionRef
            {
                newAction = Handler(0f, 0f, thisTrain.DistanceTravelledM, thisTrain.DistanceTravelledM);

                Register(thisTrain.Number, location);
            }
            return newAction;
        }


        /// <summary>
        /// SetDelay
        /// To fullfill the waiting delay.
        /// </summary>
        public void SetDelay(int delay)
        {
            Delay = delay;
        }

        public override float[] CalculateDistancesToNextAction(Train thisTrain, float presentSpeedMpS, bool reschedule)
        {
            float[] distancesM = new float[2];

            int thisSectionIndex = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].TrackCircuitSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
            float leftInSectionM = thisSection.Length - thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].Offset;

            // get action route index - if not found, return distances < 0

            int actionIndex0 = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].RouteListIndex;
            int actionRouteIndex = thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetRouteIndex(TCSectionIndex, actionIndex0);
            float activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled + thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, this.RequiredDistance, thisTrain.AITrainDirectionForward);


            // if reschedule, use actual speed

            float triggerDistanceM = TriggerDistance;

            if (thisTrain.TrainType != TrainType.AiPlayerDriven)
            {

                if (thisTrain is AITrain)
                {
                    AITrain aiTrain = thisTrain as AITrain;
                    if (reschedule)
                    {
                        float firstPartTime = 0.0f;
                        float firstPartRangeM = 0.0f;
                        float secndPartRangeM = 0.0f;
                        float remainingRangeM = activateDistanceTravelledM - thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled;

                        firstPartTime = presentSpeedMpS / (0.25f * aiTrain.MaxDecelMpSS);
                        firstPartRangeM = 0.25f * aiTrain.MaxDecelMpSS * (firstPartTime * firstPartTime);

                        if (firstPartRangeM < remainingRangeM && thisTrain.SpeedMpS < thisTrain.TrainMaxSpeedMpS) // if distance left and not at max speed
                        // split remaining distance based on relation between acceleration and deceleration
                        {
                            secndPartRangeM = (remainingRangeM - firstPartRangeM) * (2.0f * aiTrain.MaxDecelMpSS) / (aiTrain.MaxDecelMpSS + aiTrain.MaxAccelMpSS);
                        }

                        triggerDistanceM = activateDistanceTravelledM - (firstPartRangeM + secndPartRangeM);
                    }
                    else

                    // use maximum speed
                    {
                        float deltaTime = thisTrain.TrainMaxSpeedMpS / aiTrain.MaxDecelMpSS;
                        float brakingDistanceM = (thisTrain.TrainMaxSpeedMpS * deltaTime) + (0.5f * aiTrain.MaxDecelMpSS * deltaTime * deltaTime);
                        triggerDistanceM = activateDistanceTravelledM - brakingDistanceM;
                    }
                }
                else
                {
                    activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled + thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, this.RequiredDistance, true);
                    triggerDistanceM = activateDistanceTravelledM;
                }

                distancesM[1] = triggerDistanceM;
                if (activateDistanceTravelledM < thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled &&
                    thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled - activateDistanceTravelledM < thisTrain.Length)
                    activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled;
                distancesM[0] = activateDistanceTravelledM;

                return (distancesM);
            }
            else
            {
                activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled + thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, this.RequiredDistance, true);
                triggerDistanceM = activateDistanceTravelledM - Math.Min(this.RequiredDistance, 300);

                if (activateDistanceTravelledM < thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled &&
                    thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled - activateDistanceTravelledM < thisTrain.Length)
                {
                    activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled;
                    triggerDistanceM = activateDistanceTravelledM;
                }
                distancesM[1] = triggerDistanceM;
                distancesM[0] = activateDistanceTravelledM;

                return (distancesM);
            }
        }


        public override List<KeyValuePair<Type, AuxActionRef>> GetCallFunction()
        {
            List<KeyValuePair<Type, AuxActionRef>> listInfo = new List<KeyValuePair<Type, AuxActionRef>>();

            Type managed = typeof(Signal);
            KeyValuePair<Type, AuxActionRef> info = new KeyValuePair<Type, AuxActionRef>(managed, this);
            listInfo.Add(info);
            return listInfo;
        }
    }

    /// <summary>
    /// AIActionHornRef
    /// Start and Stop the horn
    /// </summary>
    public class AIActionHornRef : AIAuxActionsRef
    {
        /// <summary>
        /// The duration of the horn blast, if specified by an activity event.
        /// </summary>
        private int? duration;

        /// <summary>
        /// The horn pattern to use.
        /// </summary>
        private AILevelCrossingHornPattern hornPattern;

        public AIActionHornRef() { }

        public AIActionHornRef(Train thisTrain, float distance, float requiredSpeedMpS, int subrouteIdx, int routeIdx, int sectionIdx, Direction direction, int? durationS, AILevelCrossingHornPattern hornPattern)
            : base(thisTrain, distance, requiredSpeedMpS, subrouteIdx, routeIdx, sectionIdx, direction, AuxiliaryAction.SoundHorn)
        {
            duration = durationS;
            this.hornPattern = hornPattern;
            NextAction = AuxiliaryAction.SoundHorn;
        }

        public AIActionHornRef(Train thisTrain, AuxActionHorn source)
            : base(thisTrain, 0f, 0f, 0, 0, 0, 0, source.ActionType)
        {
            duration = source.Delay;
            NextAction = AuxiliaryAction.SoundHorn;
            GenericAction = source.GenericAction;
            hornPattern = AILevelCrossingHornPattern.CreateInstance(source.Pattern);
        }

        public override async ValueTask<AuxActionRefSaveState> Snapshot()
        {
            AuxActionRefSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.Duration = duration;
            saveState.LevelCrossingHornPattern = AILevelCrossingHornPattern.LevelCrossingHornPatternType(hornPattern);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] AuxActionRefSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ActionType = AuxiliaryAction.SoundHorn;
            duration = saveState.Duration;
            hornPattern = AILevelCrossingHornPattern.CreateInstance(saveState.LevelCrossingHornPattern);
            NextAction = AuxiliaryAction.SoundHorn;
        }

        internal override AIActionItem Handler(params object[] list)
        {
            AIActionItem info = null;
            if (!LinkedAuxAction || GenericAction)
            {
                LinkedAuxAction = true;
                info = new AuxActionHornItem(this, AiActionType.AuxiliaryAction, duration, hornPattern);
                info.SetParam((float)list[0], (float)list[1], (float)list[2], (float)list[3]);
            }
            return (AIActionItem)info;
        }

        internal override AIActionItem CheckGenActions(in WorldLocation location, AITrain thisTrain, params object[] list)
        {
            AIActionItem newAction = null;
            float rearDist = (float)list[0];
            float frontDist = (float)list[1];
            uint trackNodeIndex = (uint)list[2];
            float minDist = Math.Min(Math.Abs(rearDist), frontDist);

            float[] distances = GetActivationDistances(thisTrain);

            if (distances[0] >= -minDist)   //  We call the handler to generate an actionRef
            {
                hornPattern = (AILevelCrossingHornPattern)list[3];
                newAction = Handler(distances[0] + thisTrain.DistanceTravelledM, thisTrain.SpeedMpS, distances[0] + thisTrain.DistanceTravelledM, thisTrain.DistanceTravelledM);
                Register(thisTrain.Number, location);
            }
            return newAction;
        }

        public override List<KeyValuePair<Type, AuxActionRef>> GetCallFunction()
        {
            Type managed = typeof(LevelCrossings);
            KeyValuePair<Type, AuxActionRef> info = new KeyValuePair<Type, AuxActionRef>(managed, this);
            List<KeyValuePair<Type, AuxActionRef>> listInfo = new List<KeyValuePair<Type, AuxActionRef>>
            {
                info
            };
            return listInfo;
        }

        //  Start horn whatever the speed.
        public override float[] CalculateDistancesToNextAction(Train thisTrain, float presentSpeedMpS, bool reschedule)
        {
            int thisSectionIndex = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].TrackCircuitSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
            float leftInSectionM = thisSection.Length - thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].Offset;

            // get action route index - if not found, return distances < 0

            int actionIndex0 = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].RouteListIndex;
            int actionRouteIndex = thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetRouteIndex(TCSectionIndex, actionIndex0);
            float activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled + thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, this.RequiredDistance, thisTrain.AITrainDirectionForward);
            float[] distancesM = new float[2];
            distancesM[1] = activateDistanceTravelledM;
            distancesM[0] = activateDistanceTravelledM;

            return (distancesM);
        }

        //  SPA:    We use this fonction and not the one from Train in order to leave control to the AuxAction
        public override float[] GetActivationDistances(Train train) => new float[2] { RequiredDistance, RequiredDistance + train.Length };

    }

    /// <summary>
    /// AIActSigDelegateRef
    /// An action to delegate the Signal management from a WP
    /// </summary>
    internal class AIActSigDelegateRef : AIAuxActionsRef
    {
        public bool IsAbsolute;
        public AIActionWPRef AssociatedWPAction;
        public float brakeSection;
        protected AuxActSigDelegate AssociatedItem;  //  In order to Unlock the signal when removing Action Reference

        public AIActSigDelegateRef(AuxActionRefSaveState saveState, List<AuxActionRef> auxActions)
        {
            if (saveState.WaitPointAction && auxActions.Count > 0)
            {
                AuxActionRef candidateAssociate = auxActions[^1];
                if (candidateAssociate is AIActionWPRef aiActionWaitPointRef && aiActionWaitPointRef.TCSectionIndex == saveState.TrackCircuitSectionIndex)
                {
                    AssociatedWPAction = aiActionWaitPointRef;
                }
            }
        }

        public AIActSigDelegateRef(Train thisTrain, float distance, float requiredSpeedMpS, int subrouteIdx, int routeIdx, int sectionIdx, Direction direction, AIActionWPRef associatedWPAction = null)
            : base(thisTrain, distance, requiredSpeedMpS, subrouteIdx, routeIdx, sectionIdx, direction, AuxiliaryAction.SignalDelegate)
        {
            AssociatedWPAction = associatedWPAction;
            NextAction = AuxiliaryAction.SignalDelegate;
            GenericAction = true;

            brakeSection = distance; // Set to 1 later when applicable
        }

        public override async ValueTask<AuxActionRefSaveState> Snapshot()
        {
            AuxActionRefSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.Delay = Delay;
            saveState.BrakeSection = brakeSection;
            saveState.Absolute = IsAbsolute;
            saveState.WaitPointAction = AssociatedWPAction != null;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] AuxActionRefSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            ActionType = AuxiliaryAction.SignalDelegate;
            Delay = saveState.Delay;
            brakeSection = saveState.BrakeSection;
            IsAbsolute = saveState.Absolute;
            NextAction = AuxiliaryAction.SignalDelegate;
        }

        public override bool CallFreeAction(Train thisTrain)
        {
            if (AssociatedItem != null && AssociatedItem.SignalReferenced != null)
            {
                if (AssociatedItem.locked)
                    AssociatedItem.SignalReferenced.UnlockForTrain(thisTrain.Number, thisTrain.TCRoute.ActiveSubPath);
                AssociatedItem.SignalReferenced = null;
                AssociatedItem = null;
                return true;
            }
            else if (AssociatedItem == null)
                return true;
            return false;
        }

        internal override AIActionItem Handler(params object[] list)
        {
            if (AssociatedItem != null)
                return null;
            AuxActSigDelegate info = new AuxActSigDelegate(this, AiActionType.AuxiliaryAction);
            info.SetParam((float)list[0], (float)list[1], (float)list[2], (float)list[3]);
            AssociatedItem = info;
            return info;
        }

        //  SigDelegateRef.

        public override float[] CalculateDistancesToNextAction(Train thisTrain, float presentSpeedMpS, bool reschedule)
        {
            int thisSectionIndex = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].TrackCircuitSectionIndex;
            TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[thisSectionIndex];
            float leftInSectionM = thisSection.Length - thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].Offset;

            // get action route index - if not found, return distances < 0

            int actionIndex0 = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].RouteListIndex;
            int actionRouteIndex = thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetRouteIndex(TCSectionIndex, actionIndex0);
            float activateDistanceTravelledM = -1;

            if (actionIndex0 != -1 && actionRouteIndex != -1)
                activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled + thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].GetDistanceAlongRoute(actionIndex0, leftInSectionM, actionRouteIndex, this.RequiredDistance, true);

            var currBrakeSection = (thisTrain is AITrain && !(thisTrain.TrainType == TrainType.AiPlayerDriven)) ? 1 : brakeSection;
            float triggerDistanceM = activateDistanceTravelledM - Math.Min(this.RequiredDistance, 300);   //  TODO, add the size of train

            float[] distancesM = new float[2];
            distancesM[1] = triggerDistanceM;
            if (activateDistanceTravelledM < thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled &&
                thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled - activateDistanceTravelledM < thisTrain.Length)
                activateDistanceTravelledM = thisTrain.PresentPosition[FreeTrainSimulator.Common.Direction.Forward].DistanceTravelled;
            distancesM[0] = activateDistanceTravelledM;

            return (distancesM);
        }

        public void SetEndSignalIndex(int idx)
        {
            EndSignalIndex = idx;
        }

        /// <summary>
        /// SetDelay
        /// To fullfill the waiting delay.
        /// </summary>
        public void SetDelay(int delay)
        {
            Delay = delay;
        }
    }



    #endregion

    #region AuxActionData
    /// <summary>
    /// AuxActionItem
    /// A specific AIActionItem used at run time to manage a specific Auxiliary Action
    /// </summary>
    internal class AuxActionItem : AIActionItem
    {
        public AuxActionRef ActionRef { get; set; }
        public bool Triggered { get; set; }
        public bool Processing { get; set; }
        public AiMovementState currentMvmtState { get; set; } = AiMovementState.InitAction;
        public Signal SignalReferenced { get { return ((AIAuxActionsRef)ActionRef).SignalReferenced; } set { } }

        /// <summary>
        /// AuxActionItem
        /// The basic constructor
        /// </summary>
        public AuxActionItem() { }

        public AuxActionItem(AuxActionRef thisItem, AiActionType thisAction) :
            base(null, thisAction)
        {
            NextAction = AiActionType.AuxiliaryAction;
            ActionRef = thisItem;
        }

        public override async ValueTask<ActionItemSaveState> Snapshot()
        {
            ActionItemSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.ActionItemType = ActionItemType.AuxiliaryAction;
            return saveState;
        }

        public virtual bool CanActivate(Train thisTrain, float SpeedMpS, bool reschedule)
        {
            return false;
        }

        public virtual bool CanRemove(Train thisTrain)
        {
            return true;
        }

        public override AiMovementState ProcessAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            AiMovementState mvtState = movementState;
            if (ActionRef.GenericAction)
                mvtState = currentMvmtState;
            int correctedTime = presentTime;
            switch (mvtState)
            {
                case AiMovementState.InitAction:
                    movementState = InitAction(thisTrain, presentTime, elapsedClockSeconds, mvtState);
                    break;
                case AiMovementState.HandleAction:
                    movementState = HandleAction(thisTrain, presentTime, elapsedClockSeconds, mvtState);
                    break;
                case AiMovementState.Braking:
                    break;
                case AiMovementState.Stopped:
                    break;
                default:
                    break;
            }
            currentMvmtState = movementState;
            return movementState;
        }

        public override AiMovementState ProcessAction(Train thisTrain, int presentTime)
        {
            int correctedTime = presentTime;
            switch (currentMvmtState)
            {
                case AiMovementState.InitAction:
                case AiMovementState.Stopped:
                    currentMvmtState = InitAction(thisTrain, presentTime, 0f, currentMvmtState);
                    break;
                case AiMovementState.HandleAction:
                    currentMvmtState = HandleAction(thisTrain, presentTime, 0f, currentMvmtState);
                    break;
                default:
                    break;
            }
            return currentMvmtState;
        }

    }

    /// <summary>
    /// AuxActionWPItem
    /// A specific class used at run time to manage a Waiting Point Action
    /// </summary>
    internal class AuxActionWPItem : AuxActionItem
    {
        private int Delay;
        public int ActualDepart;

        /// <summary>
        /// AuxActionWPItem
        /// The specific constructor for WP action
        /// </summary>
        public AuxActionWPItem(AuxActionRef thisItem, AiActionType thisAction) :
            base(thisItem, thisAction)
        {
            ActualDepart = 0;
        }

        /// <summary>
        /// AsString
        /// Used by debugging in HUDWindows.
        /// </summary>
        public override string AsString(AITrain thisTrain)
        {
            return " WP(";
        }

        public override bool CanActivate(Train thisTrain, float SpeedMpS, bool reschedule)
        {
            if (ActionRef == null || thisTrain.PresentPosition[Direction.Forward].RouteListIndex == -1)
                return false;
            float[] distancesM = ((AIAuxActionsRef)ActionRef).CalculateDistancesToNextAction(thisTrain, SpeedMpS, reschedule);
            if (thisTrain.TrainType != TrainType.AiPlayerDriven)
            {
                if (RequiredDistance < thisTrain.DistanceTravelledM) // trigger point
                {
                    return true;
                }

                RequiredDistance = distancesM[1];
                ActivateDistanceM = distancesM[0];
                return false;
            }
            else
            {
                return Math.Abs(thisTrain.SpeedMpS) <= 0.1f && distancesM[1] <= thisTrain.DistanceTravelledM;
            }
        }

        public void SetDelay(int delay)
        {
            Delay = delay;
        }

        public override bool ValidAction(Train train)
        {
            bool actionValid = CanActivate(train, train.SpeedMpS, true);
            if (train is AITrain aiTrain && train.TrainType != TrainType.AiPlayerDriven)
            {
                if (!actionValid)
                {
                    aiTrain.RequiredActions.InsertAction(this);
                }
                aiTrain.EndProcessAction(actionValid, this, false);
            }
            return actionValid;
        }

        public override AiMovementState InitAction(Train train, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            if (train is AITrain aiTrain)
            {
                // repeat stopping of train, because it could have been moved by UpdateBrakingState after ProcessAction
                if (aiTrain.TrainType != TrainType.AiPlayerDriven)
                {
                    aiTrain.AdjustControlsBrakeMore(aiTrain.MaxDecelMpSS, elapsedClockSeconds, 100);
                    aiTrain.SpeedMpS = 0;
                }
                int correctedTime = presentTime;
                // If delay between 40000 and 60000 an uncoupling is performed and delay is returned with the two lowest digits of the original one
                aiTrain.TestUncouple(ref Delay);
                // If delay between 30000 and 40000 it is considered an absolute delay in the form 3HHMM, where HH and MM are hour and minute where the delay ends
                Delay = train.TestAbsDelay(Delay, correctedTime);
                // If delay equal to 60001 it is considered as a command to unconditionally attach to the nearby train;
                aiTrain.TestUncondAttach(ref Delay);
                // If delay equal to 60002 it is considered as a request for permission to pass signal;
                aiTrain.TestPermission(ref Delay);
                ActualDepart = correctedTime + Delay;
                aiTrain.AuxActionsContainer.CheckGenActions(this.GetType(), aiTrain.RearTDBTraveller.WorldLocation, Delay);

            }

            return AiMovementState.HandleAction;
        }

        public override AiMovementState HandleAction(Train train, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            if (train is AITrain aiTrain)
            {
                if (train.TrainType != TrainType.AiPlayerDriven)
                {
                    train.SpeedMpS = 0;
                }
                train.AuxActionsContainer.CheckGenActions(this.GetType(), aiTrain.RearTDBTraveller.WorldLocation, ActualDepart - presentTime);

                if (ActualDepart > presentTime)
                {
                    movementState = AiMovementState.HandleAction;
                }
                else
                {
                    if (train.AuxActionsContainer.CountSpec() > 0)
                        train.AuxActionsContainer.Remove(this);
                    return AiMovementState.Stopped;
                }
            }
            else
            {
                if (train.AuxActionsContainer.CountSpec() > 0)
                    train.AuxActionsContainer.Remove(this);
                return AiMovementState.Stopped;
            }
            return movementState;
        }

        public override AiMovementState ProcessAction(Train train, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            //int correctedTime = presentTime;
            //switch (movementState)
            //{
            AiMovementState mvtState = movementState;
            if (ActionRef.GenericAction)
                mvtState = currentMvmtState;
            int correctedTime = presentTime;
            switch (mvtState)
            {
                case AiMovementState.InitAction:
                    movementState = InitAction(train, presentTime, elapsedClockSeconds, movementState);
                    break;
                case AiMovementState.HandleAction:
                    movementState = HandleAction(train, presentTime, elapsedClockSeconds, movementState);
                    break;
                case AiMovementState.Braking:
                    if (train is AITrain)
                    {
                        AITrain aiTrain = train as AITrain;
                        float distanceToGoM = train.ActivityClearingDistanceM;
                        distanceToGoM = ActivateDistanceM - aiTrain.PresentPosition[Direction.Forward].DistanceTravelled;
                        float NextStopDistanceM = distanceToGoM;
                        if (distanceToGoM <= 0f)
                        {
                            aiTrain.AdjustControlsBrakeMore(aiTrain.MaxDecelMpSS, elapsedClockSeconds, 100);
                            aiTrain.AITrainThrottlePercent = 0;

                            if (aiTrain.SpeedMpS < 0.001)
                            {
                                aiTrain.SpeedMpS = 0f;
                                movementState = AiMovementState.InitAction;
                            }
                        }
                        else if (distanceToGoM < AITrain.SignalApproachDistance && Math.Abs(aiTrain.SpeedMpS) <= 0.1f)
                        {
                            aiTrain.AdjustControlsBrakeMore(aiTrain.MaxDecelMpSS, elapsedClockSeconds, 100);
                            movementState = AiMovementState.InitAction;
                        }
                    }
                    else
                    {
                        if (train.AuxActionsContainer.CountSpec() > 0)
                            train.AuxActionsContainer.RemoveAt(0);
                    }
                    break;
                case AiMovementState.Stopped:
                    if (!(train is AITrain))
                        if (train.AuxActionsContainer.CountSpec() > 0)
                            train.AuxActionsContainer.Remove(this);

                    if (train is AITrain)
                    {
                        AITrain aiTrain = train as AITrain;

                        //movementState = thisTrain.UpdateStoppedState();   // Don't call UpdateStoppedState(), WP can't touch Signal
                        movementState = AiMovementState.Braking;
                        aiTrain.ResetActions(true);
                    }
                    break;
                default:
                    break;
            }
            if (ActionRef.GenericAction)
                currentMvmtState = movementState;
            return movementState;
        }

    }

    /// <summary>
    /// AuxActionHornItem
    /// A specific class used at run time to manage a Horn Action
    /// </summary>
    internal class AuxActionHornItem : AuxActionItem
    {
        private int? DurationS { get; }
        private AILevelCrossingHornPattern HornPattern { get; }
        private int NextStepTimeS { get; set; }
        private IEnumerator<int> Execution { get; set; }

        /// <summary>
        /// AuxActionhornItem
        /// The specific constructor for horn action
        /// </summary>
        public AuxActionHornItem(AuxActionRef thisItem, AiActionType thisAction, int? durationS, AILevelCrossingHornPattern hornPattern) :
            base(thisItem, thisAction)
        {
            DurationS = durationS;
            HornPattern = hornPattern;
        }

        /// <summary>
        /// AsString
        /// Used by debugging in HUDWindows.
        /// </summary>
        public override string AsString(AITrain thisTrain)
        {
            return " Horn(";
        }

        public override bool CanActivate(Train thisTrain, float SpeedMpS, bool reschedule)
        {
            if (ActionRef == null)
                return false;
            float[] distancesM = ((AIAuxActionsRef)ActionRef).CalculateDistancesToNextAction(thisTrain, SpeedMpS, reschedule);


            if (RequiredDistance < thisTrain.DistanceTravelledM) // trigger point
            {
                return true;
            }

            RequiredDistance = distancesM[1];
            ActivateDistanceM = distancesM[0];
            return false;
        }

        public override bool ValidAction(Train thisTrain)
        {
            bool actionValid = CanActivate(thisTrain, thisTrain.SpeedMpS, true);
            if (!(thisTrain is AITrain))
            {
                AITrain aiTrain = thisTrain as AITrain;

                if (!actionValid)
                {
                    aiTrain.RequiredActions.InsertAction(this);
                }
                aiTrain.EndProcessAction(actionValid, this, false);
            }
            return actionValid;
        }

        public override AiMovementState InitAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            Processing = true;
            int correctedTime = presentTime;
            if (!Triggered)
            {
                NextStepTimeS = correctedTime;
                var locomotive = (MSTSLocomotive)thisTrain.FindLeadLocomotive();
                Execution = HornPattern.Execute(locomotive, DurationS);
                Triggered = true;
            }
            return AiMovementState.HandleAction;
        }

        public override AiMovementState HandleAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            if (Triggered && presentTime > NextStepTimeS)
            {
                // Advance to the next step.
                if (Execution.MoveNext())
                {
                    NextStepTimeS = presentTime + Execution.Current;
                    return AiMovementState.HandleAction;
                }
                else
                {
                    thisTrain.AuxActionsContainer.Remove(this);
                    Triggered = false;
                    return currentMvmtState;    //  Restore previous MovementState
                }
            }
            else
            {
                return movementState;
            }
        }

        public override AiMovementState ProcessAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            AiMovementState mvtState = movementState;
            if (ActionRef.GenericAction)
                mvtState = currentMvmtState;
            int correctedTime = presentTime;
            switch (mvtState)
            {
                case AiMovementState.InitAction:
                    movementState = InitAction(thisTrain, presentTime, elapsedClockSeconds, mvtState);
                    break;
                case AiMovementState.HandleAction:
                    movementState = HandleAction(thisTrain, presentTime, elapsedClockSeconds, mvtState);
                    break;
                case AiMovementState.Braking:
                    if (this.ActionRef.ActionType != AuxiliaryAction.SoundHorn)
                    {
                        float distanceToGoM = thisTrain.ActivityClearingDistanceM;
                        distanceToGoM = ActivateDistanceM - thisTrain.PresentPosition[Direction.Forward].DistanceTravelled;
                        float NextStopDistanceM = distanceToGoM;
                        if (distanceToGoM < 0f)
                        {
                            currentMvmtState = movementState;
                            movementState = AiMovementState.InitAction;
                        }
                    }
                    break;
                case AiMovementState.Stopped:
                    if (thisTrain is AITrain)
                        movementState = ((AITrain)thisTrain).UpdateStoppedState(elapsedClockSeconds);
                    break;
                default:
                    break;
            }
            if (ActionRef.GenericAction)
                currentMvmtState = movementState;

            return movementState;
        }

    }

    /// <summary>
    /// AuxActSigDelegate
    /// Used to postpone the signal clear after WP
    /// </summary>
    internal class AuxActSigDelegate : AuxActionItem
    {
        public int ActualDepart;
        public bool locked = true;

        /// <summary>
        /// AuxActSigDelegate Item
        /// The specific constructor for AuxActSigDelegate action
        /// </summary>
        public AuxActSigDelegate(AuxActionRef thisItem, AiActionType thisAction) :
            base(thisItem, thisAction)
        {
            ActualDepart = 0;
        }

        /// <summary>
        /// AsString
        /// Used by debugging in HUDWindows.
        /// </summary>
        public override string AsString(AITrain thisTrain)
        {
            return " SigDlgt(";
        }

        public bool ClearSignal(Train thisTrain)
        {
            if (SignalReferenced != null)
            {
                bool ret = SignalReferenced.RequestClearSignal(thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward], thisTrain.RoutedForward, 0, false, null);
                return ret;
            }
            return true;
        }

        public override bool CanActivate(Train thisTrain, float SpeedMpS, bool reschedule)
        {
            if (ActionRef == null || SignalReferenced == null)
            {
                thisTrain.AuxActionsContainer.RemoveSpecReqAction(this);
                return false;
            }
            if (((AIAuxActionsRef)ActionRef).LinkedAuxAction)
                return false;
            float[] distancesM = ((AIAuxActionsRef)ActionRef).CalculateDistancesToNextAction(thisTrain, SpeedMpS, reschedule);
            if (distancesM[0] < thisTrain.DistanceTravelledM && !((AIActSigDelegateRef)ActionRef).IsAbsolute) // trigger point
            {
                if (thisTrain.SpeedMpS > 0f)
                {
                    if (thisTrain is AITrain && thisTrain.TrainType != TrainType.AiPlayerDriven)
                    {
                        thisTrain.SetTrainOutOfControl(OutOfControlReason.OutOfPath);
                        return true;
                    }
                    else
                        return false;
                }
            }

            if (!reschedule && distancesM[1] < thisTrain.DistanceTravelledM && (Math.Abs(thisTrain.SpeedMpS) <= 0.1f ||
                (thisTrain.IsPlayerDriven && currentMvmtState == AiMovementState.HandleAction)))
            {
                return true;
            }

            if (!reschedule && ((AIActSigDelegateRef)ActionRef).IsAbsolute)
            {
                TrackCircuitSection thisSection = TrackCircuitSection.TrackCircuitList[((AIActSigDelegateRef)ActionRef).TCSectionIndex];
                if (((thisSection.CircuitState.TrainReserved != null && thisSection.CircuitState.TrainReserved.Train == thisTrain) || thisSection.CircuitState.OccupiedByThisTrain(thisTrain)) &&
                    ((AIActSigDelegateRef)ActionRef).EndSignalIndex != -1)
                    return true;
            }

            //RequiredDistance = distancesM[1];
            //ActivateDistanceM = distancesM[0];
            return false;
        }

        public override bool CanRemove(Train thisTrain)
        {
            if (Processing && (currentMvmtState == AiMovementState.Stopped || currentMvmtState == AiMovementState.HandleAction))
                return true;
            return SignalReferenced == null;
        }


        public override bool ValidAction(Train thisTrain)
        {
            bool actionValid = CanActivate(thisTrain, thisTrain.SpeedMpS, true);
            if (!actionValid)
            {
                //thisTrain.requiredActions.InsertAction(this);
            }
            return actionValid;
        }

        public override AiMovementState InitAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            int delay = ((AIActSigDelegateRef)ActionRef).Delay;
            // If delay between 30000 and 40000 it is considered an absolute delay in the form 3HHMM, where HH and MM are hour and minute where the delay ends
            delay = thisTrain.TestAbsDelay(delay, presentTime);
            ActualDepart = presentTime + delay;
            Processing = true;
            return AiMovementState.HandleAction;
        }

        public override AiMovementState HandleAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            if (ActualDepart >= presentTime)
            {

                return movementState;
            }
            if (locked && SignalReferenced != null)
            {
                locked = false;
                if (SignalReferenced.HasLockForTrain(thisTrain.Number, thisTrain.TCRoute.ActiveSubPath))
                    SignalReferenced.UnlockForTrain(thisTrain.Number, thisTrain.TCRoute.ActiveSubPath);
                else
                {
                    //                    locked = true;
                    Trace.TraceWarning("SignalObject trItem={0}, trackNode={1}, wasn't locked for train {2}.",
                        SignalReferenced.TrackItemIndex, SignalReferenced.TrackNode, thisTrain.Number);
                }
            }
            if (ClearSignal(thisTrain) || (thisTrain.NextSignalObjects[Direction.Forward] != null && (thisTrain.NextSignalObjects[Direction.Forward].SignalLR(SignalFunction.Normal) > SignalAspectState.Stop)) ||
                thisTrain.NextSignalObjects[Direction.Forward] == null || SignalReferenced != thisTrain.NextSignalObjects[Direction.Forward] ||
                thisTrain.PresentPosition[Direction.Forward].TrackCircuitSectionIndex == thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward][thisTrain.ValidRoutes[FreeTrainSimulator.Common.Direction.Forward].Count - 1].TrackCircuitSection.Index)
            {
                if (((AIActSigDelegateRef)ActionRef).AssociatedWPAction != null)
                {
                    var WPAction = ((AIActSigDelegateRef)ActionRef).AssociatedWPAction.WaitingPoint;
                    if (thisTrain.RequiredActions.Contains(WPAction))
                    {
                        thisTrain.RequiredActions.Remove(WPAction);
                    }
                    if (thisTrain.AuxActionsContainer.SpecificRequiredActions.Contains(WPAction))
                        thisTrain.AuxActionsContainer.SpecificRequiredActions.Remove(WPAction);
                    if (thisTrain.AuxActionsContainer.SpecAuxActions.Contains(((AIActSigDelegateRef)ActionRef).AssociatedWPAction))
                        thisTrain.AuxActionsContainer.SpecAuxActions.Remove(((AIActSigDelegateRef)ActionRef).AssociatedWPAction);
                }
                if (thisTrain.AuxActionsContainer.CountSpec() > 0)
                {
                    thisTrain.AuxActionsContainer.Remove(this);
                }
                return (thisTrain is AITrain && (thisTrain as AITrain).MovementState == AiMovementState.StationStop ? AiMovementState.StationStop : AiMovementState.Stopped);
            }
            return movementState;
        }

        public override AiMovementState ProcessAction(Train thisTrain, int presentTime, double elapsedClockSeconds, AiMovementState movementState)
        {
            movementState = base.ProcessAction(thisTrain, presentTime, elapsedClockSeconds, movementState);
            return movementState;
        }
    }

    #endregion
}