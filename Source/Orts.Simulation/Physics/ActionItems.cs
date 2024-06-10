using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Models.State;
using Orts.Simulation.AIs;

namespace Orts.Simulation.Physics
{
    internal class DistanceTravelledItemActivator:
                ISaveStateRestoreApi<ActionItemSaveState, DistanceTravelledItem>
    {
        DistanceTravelledItem ISaveStateRestoreApi<ActionItemSaveState, DistanceTravelledItem>.CreateRuntimeTarget(ActionItemSaveState saveState)
        {
            return saveState.ActionItemType switch
            {
                ActionItemType.ActiveSpeedLimit => new ActivateSpeedLimit(),
                ActionItemType.ClearSection => new ClearSectionItem(),
                ActionItemType.AiActionItem => new AIActionItem(),
                ActionItemType.AuxiliaryAction => new AuxActionItem(),
                ActionItemType.ClearMovingTable => new ClearMovingTableAction(),
                _ => null,
            };
        }
    }

    /// <summary>
    /// Distance Travelled action item - base class for all possible actions
    /// </summary>
    internal abstract class DistanceTravelledItem : 
        ISaveStateApi<ActionItemSaveState>
    {
        public float RequiredDistance { get; internal set; }

        public virtual ValueTask<ActionItemSaveState> Snapshot()
        {
            return ValueTask.FromResult(new ActionItemSaveState()
            {
                Distance = RequiredDistance,
            });
        }

        public virtual ValueTask Restore(ActionItemSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            RequiredDistance = saveState.Distance;

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Distance Travelled Clear Section action item
    /// </summary>
    internal class ClearSectionItem : DistanceTravelledItem
    {
        public int TrackSectionIndex { get; private set; }  // in case of CLEAR_SECTION  //

        public ClearSectionItem() { }
        /// <summary>
        /// constructor for clear section
        /// </summary>
        public ClearSectionItem(float distance, int sectionIndex)
        {
            RequiredDistance = distance;
            TrackSectionIndex = sectionIndex;
        }

        public override async ValueTask<ActionItemSaveState> Snapshot()
        {
            ActionItemSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.ActionItemType = ActionItemType.ClearSection;
            saveState.TrackSectionIndex = TrackSectionIndex;

            return saveState;
        }

        public override async ValueTask Restore([NotNull] ActionItemSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            TrackSectionIndex = saveState.TrackSectionIndex;
        }
    }

    /// <summary>
    /// Distance Travelled Speed Limit Item
    /// </summary>
    internal class ActivateSpeedLimit : DistanceTravelledItem
    {
        public float MaxSpeedMpSLimit { get; internal set; } = -1;
        public float MaxSpeedMpSSignal { get; internal set; } = -1;
        public float MaxTempSpeedMpSLimit { get; internal set; } = -1;

        public ActivateSpeedLimit() { }

        /// <summary>
        /// constructor for speedlimit value
        /// </summary>
        public ActivateSpeedLimit(float reqDistance, float maxSpeedMpSLimit, float maxSpeedMpSSignal, float maxTempSpeedMpSLimit = -1)
        {
            RequiredDistance = reqDistance;
            MaxSpeedMpSLimit = maxSpeedMpSLimit;
            MaxSpeedMpSSignal = maxSpeedMpSSignal;
            MaxTempSpeedMpSLimit = maxTempSpeedMpSLimit;
        }

        public override async ValueTask<ActionItemSaveState> Snapshot()
        {
            ActionItemSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.ActionItemType = Common.ActionItemType.ActiveSpeedLimit;
            saveState.MaxSpeedLimit = MaxSpeedMpSLimit;
            saveState.MaxSpeedSignal = MaxSpeedMpSSignal;
            saveState.MaxTempSpeedLimit = MaxTempSpeedMpSLimit;

            return saveState;
        }

        public override async ValueTask Restore([NotNull] ActionItemSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            MaxSpeedMpSLimit = saveState.MaxSpeedLimit;
            MaxSpeedMpSSignal = saveState.MaxSpeedSignal;
            MaxTempSpeedMpSLimit = saveState.MaxTempSpeedLimit;
        }
    }

    internal class ClearMovingTableAction : DistanceTravelledItem
    {
        public float OriginalMaxTrainSpeedMpS { get; private set; }                // original train speed

        //================================================================================================//
        /// <summary>
        /// constructor for speedlimit value
        /// </summary>

        public ClearMovingTableAction() { }

        public ClearMovingTableAction(float reqDistance, float maxSpeedMpSLimit)
        {
            RequiredDistance = reqDistance;
            OriginalMaxTrainSpeedMpS = maxSpeedMpSLimit;
        }

        //================================================================================================//
        //
        // Restore
        //

        public override async ValueTask<ActionItemSaveState> Snapshot()
        {
            ActionItemSaveState saveState = await base.Snapshot().ConfigureAwait(false);

            saveState.OriginalMaxTrainSpeed = OriginalMaxTrainSpeedMpS;

            return saveState;
        }

        public override async ValueTask Restore(ActionItemSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            OriginalMaxTrainSpeedMpS = saveState.OriginalMaxTrainSpeed;
        }
    }

    //================================================================================================//
    /// <summary>
    /// Distance Travelled action item list
    /// </summary>

    internal class DistanceTravelledActions : LinkedList<DistanceTravelledItem>
    {
        // Copy list
        public DistanceTravelledActions Copy()
        {
            DistanceTravelledActions newList = new DistanceTravelledActions();

            LinkedListNode<DistanceTravelledItem> nextNode = First;
            DistanceTravelledItem item = nextNode.Value;

            newList.AddFirst(item);
            LinkedListNode<DistanceTravelledItem> prevNode = newList.First;

            nextNode = nextNode.Next;

            while (nextNode != null)
            {
                item = nextNode.Value;
                newList.AddAfter(prevNode, item);
                nextNode = nextNode.Next;
                prevNode = prevNode.Next;
            }

            return newList;
        }


        /// <summary>
        /// Insert item on correct distance
        /// <\summary>
        public void InsertAction(DistanceTravelledItem item)
        {

            if (Count == 0)
            {
                AddFirst(item);
            }
            else
            {
                LinkedListNode<DistanceTravelledItem> nextNode = First;
                DistanceTravelledItem nextItem = nextNode.Value;
                bool inserted = false;
                while (!inserted)
                {
                    if (item.RequiredDistance < nextItem.RequiredDistance)
                    {
                        AddBefore(nextNode, item);
                        inserted = true;
                    }
                    else if (nextNode.Next == null)
                    {
                        AddAfter(nextNode, item);
                        inserted = true;
                    }
                    else
                    {
                        nextNode = nextNode.Next;
                        nextItem = nextNode.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Insert section clearance item
        /// <\summary>
        public void InsertClearSection(float distance, int sectionIndex)
        {
            ClearSectionItem thisItem = new ClearSectionItem(distance, sectionIndex);
            InsertAction(thisItem);
        }

        /// <summary>
        /// Get list of items to be processed
        /// <\summary>
        public List<DistanceTravelledItem> GetActions(float distance)
        {
            List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();

            bool itemsCollected = false;
            LinkedListNode<DistanceTravelledItem> nextNode = First;
            LinkedListNode<DistanceTravelledItem> prevNode;

            while (!itemsCollected && nextNode != null)
            {
                if (nextNode.Value.RequiredDistance <= distance)
                {
                    itemList.Add(nextNode.Value);
                    prevNode = nextNode;
                    nextNode = prevNode.Next;
                    Remove(prevNode);
                }
                else
                {
                    itemsCollected = true;
                }
            }
            return itemList;
        }

        public List<DistanceTravelledItem> GetAuxActions(Train train)
        {
            List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();
            LinkedListNode<DistanceTravelledItem> nextNode = First;

            while (nextNode != null)
            {
                if (nextNode.Value is AuxActionItem)
                {
                    AuxActionItem item = nextNode.Value as AuxActionItem;
                    if (item.CanActivate(train, train.SpeedMpS, false))
                        itemList.Add(nextNode.Value);
                }
                nextNode = nextNode.Next;
            }
            return (itemList);
        }

        /// <summary>
        /// Get list of items to be processed of particular type
        /// <\summary>
        public List<DistanceTravelledItem> GetActions(float distance, Type reqType)
        {
            List<DistanceTravelledItem> itemList = new List<DistanceTravelledItem>();

            bool itemsCollected = false;
            LinkedListNode<DistanceTravelledItem> nextNode = First;
            LinkedListNode<DistanceTravelledItem> prevNode;

            while (!itemsCollected && nextNode != null)
            {
                if (nextNode.Value.RequiredDistance <= distance)
                {
                    if (nextNode.Value.GetType() == reqType)
                    {
                        itemList.Add(nextNode.Value);
                        prevNode = nextNode;
                        nextNode = prevNode.Next;
                        Remove(prevNode);
                    }
                    else
                    {
                        nextNode = nextNode.Next;
                    }
                }
                else
                {
                    itemsCollected = true;
                }
            }

            return (itemList);
        }

        /// <summary>
        /// Get distance of last track clearance item
        /// <\summary>
        public float? GetLastClearingDistance()
        {
            float? lastDistance = null;

            bool itemsCollected = false;
            LinkedListNode<DistanceTravelledItem> nextNode = Last;

            while (!itemsCollected && nextNode != null)
            {
                if (nextNode.Value is ClearSectionItem)
                {
                    lastDistance = nextNode.Value.RequiredDistance;
                    itemsCollected = true;
                }
                nextNode = nextNode.Previous;
            }

            return lastDistance;
        }

        //================================================================================================//
        /// <summary>
        /// update any pending speed limits to new limit
        /// <\summary>
        public void UpdatePendingSpeedlimits(float speedMpS)
        {
            foreach (ActivateSpeedLimit speedLimit in this.OfType<ActivateSpeedLimit>())
            {
                if (speedLimit.MaxSpeedMpSLimit > speedMpS)
                {
                    speedLimit.MaxSpeedMpSLimit = speedMpS;
                }
                if (speedLimit.MaxSpeedMpSSignal > speedMpS)
                {
                    speedLimit.MaxSpeedMpSSignal = speedMpS;
                }
                if (speedLimit.MaxTempSpeedMpSLimit > speedMpS)
                {
                    speedLimit.MaxTempSpeedMpSLimit = speedMpS;
                }
            }
        }

        /// <summary>
        /// remove any pending AIActionItems
        /// <\summary>
        public void RemovePendingAIActionItems(bool removeAll)
        {
            List<DistanceTravelledItem> itemsToRemove = new List<DistanceTravelledItem>();

            foreach (DistanceTravelledItem action in this)
            {
                if ((action is AIActionItem && !(action is AuxActionItem)) || removeAll)
                {
                    DistanceTravelledItem thisItem = action;
                    itemsToRemove.Add(thisItem);
                }
            }

            foreach (DistanceTravelledItem removalItem in itemsToRemove)
            {
                Remove(removalItem);
            }

        }

        /// <summary>
        /// Modifies required distance of actions after a train coupling
        /// <\summary>
        public void ModifyRequiredDistance(float Length)
        {
            foreach (DistanceTravelledItem item in this)
            {
                item.RequiredDistance += Length;
            }
        }
    }
}
