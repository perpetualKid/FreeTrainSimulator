using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

using Orts.Common;

namespace Orts.Models.State
{
    public readonly struct TrainOnTableItem
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

    public readonly struct ConnectionItem
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
        public bool Used {  get; set; }
        public Vector3 RelativeFrontTraveller {  get; set; }
        public Vector3 RelativeRearTraveller { get; set; }
        public Vector3 FinalFrontTraveller { get; set; }
        public Vector3 FinalRearTraveller { get; set; }
        public Collection<TrainOnTableItem> TrainsOnTable { get; set; }
        public Queue<int> WaitingTrains { get; set; }
        public MidpointDirection MotionDirection { get; set; }
        public float Offset { get; set; }
        public ConnectionItem ForwardConnection { get; set; }
        public ConnectionItem BackwardConnection { get; set; }
        public float TargetOffset { get; set; }
        public Rotation RotationDirection { get; set; }
        public Rotation AutoRotationDirection { get; set; }
        public Vector2 Target {  get; set; }


    }
}
