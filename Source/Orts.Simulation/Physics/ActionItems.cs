using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orts.Simulation.AIs;

namespace Orts.Simulation.Physics
{
    //================================================================================================//
    /// <summary>
    /// Distance Travelled action item - base class for all possible actions
    /// </summary>

    internal class DistanceTravelledItem
    {
        public float RequiredDistance { get; internal set; }

        //================================================================================================//
        //
        // Base contructor
        //

        public DistanceTravelledItem()
        {
        }

        // Restore
        public DistanceTravelledItem(BinaryReader inf)
        {
            RequiredDistance = inf.ReadSingle();
        }

        // Save
        public void Save(BinaryWriter outf)
        {
            switch (this)
            {
                case ActivateSpeedLimit speedLimit:
                    outf.Write(1);
                    outf.Write(RequiredDistance);
                    speedLimit.SaveItem(outf);
                    break;
                case ClearSectionItem clearSerction:
                    outf.Write(2);
                    outf.Write(RequiredDistance);
                    clearSerction.SaveItem(outf);
                    break;
                case AuxActionItem auxAction:
                    outf.Write(4);
                    outf.Write(RequiredDistance);
                    auxAction.SaveItem(outf);
                    break;
                case AIActionItem aiAction:
                    outf.Write(3);
                    outf.Write(RequiredDistance);
                    aiAction.SaveItem(outf);
                    break;
                case ClearMovingTableAction clearMovingTableAction:
                    outf.Write(5);
                    outf.Write(RequiredDistance);
                    clearMovingTableAction.SaveItem(outf);
                    break;
                default:
                    outf.Write(-1);
                    break;
            }
        }
    }

    /// <summary>
    /// Distance Travelled Clear Section action item
    /// </summary>
    internal class ClearSectionItem : DistanceTravelledItem
    {
        public int TrackSectionIndex { get; }  // in case of CLEAR_SECTION  //

        /// <summary>
        /// constructor for clear section
        /// </summary>
        public ClearSectionItem(float distance, int sectionIndex)
        {
            RequiredDistance = distance;
            TrackSectionIndex = sectionIndex;
        }

        // Restore
        public ClearSectionItem(BinaryReader inf)
            : base(inf)
        {
            TrackSectionIndex = inf.ReadInt32();
        }

        // Save
        public void SaveItem(BinaryWriter outf)
        {
            outf.Write(TrackSectionIndex);
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

        // Restore
        public ActivateSpeedLimit(BinaryReader inf)
            : base(inf)
        {
            MaxSpeedMpSLimit = inf.ReadSingle();
            MaxSpeedMpSSignal = inf.ReadSingle();
            MaxTempSpeedMpSLimit = inf.ReadSingle();
        }

        // Save
        public void SaveItem(BinaryWriter outf)
        {
            outf.Write(MaxSpeedMpSLimit);
            outf.Write(MaxSpeedMpSSignal);
            outf.Write(MaxTempSpeedMpSLimit);
        }
    }

    internal class ClearMovingTableAction : DistanceTravelledItem
    {
        public float OriginalMaxTrainSpeedMpS { get; }                // original train speed

        //================================================================================================//
        /// <summary>
        /// constructor for speedlimit value
        /// </summary>

        public ClearMovingTableAction(float reqDistance, float maxSpeedMpSLimit)
        {
            RequiredDistance = reqDistance;
            OriginalMaxTrainSpeedMpS = maxSpeedMpSLimit;
        }

        //================================================================================================//
        //
        // Restore
        //

        public ClearMovingTableAction(BinaryReader inf)
            : base(inf)
        {
            OriginalMaxTrainSpeedMpS = inf.ReadSingle();
        }

        //================================================================================================//
        //
        // Save
        //

        public void SaveItem(BinaryWriter outf)
        {
            outf.Write(OriginalMaxTrainSpeedMpS);
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

            return (lastDistance);
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
