using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Imported.State
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct TrainOnTableItem
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly int TrainNumber;
        public readonly bool FrontOnBoard;
        public readonly bool RearOnBoard;

        public TrainOnTableItem(int train, bool frontOnBoard, bool rearOnBoard)
        {
            TrainNumber = train;
            FrontOnBoard = frontOnBoard;
            RearOnBoard = rearOnBoard;
        }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct ConnectionItem
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly bool Connected;
        public readonly bool SaveStatus;
        public readonly int Target;

        public ConnectionItem(bool connected, bool saveStatus, int target)
        {
            Connected = connected;
            SaveStatus = saveStatus;
            Target = target;
        }
    }


    [MemoryPackable]
    public sealed partial class MovingTableSaveState : SaveStateBase
    {
        public int Index { get; set; }
        public bool ContinousMotion { get; set; }
        public bool MoveToTarget { get; set; }
        public bool MoveToAutoTarget { get; set; }
        public int? TurntableFrameRate { get; set; }
        public int ConnectedTrackEnd { get; set; }
        public bool SendNotifications { get; set; }
        public bool Used { get; set; }
        public Vector3 RelativeFrontTraveller { get; set; }
        public Vector3 RelativeRearTraveller { get; set; }
        public Vector3 FinalFrontTraveller { get; set; }
        public Vector3 FinalRearTraveller { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrainOnTableItem> TrainsOnTable { get; set; }
        public Queue<int> WaitingTrains { get; set; }
        public MidpointDirection MotionDirection { get; set; }
        public float Offset { get; set; }
        public ConnectionItem ForwardConnection { get; set; }
        public ConnectionItem BackwardConnection { get; set; }
        public float TargetOffset { get; set; }
        public Rotation RotationDirection { get; set; }
        public Rotation AutoRotationDirection { get; set; }
        public Vector2 Target { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

    }
}
