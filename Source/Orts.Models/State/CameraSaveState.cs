using System.Numerics;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common.Position;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class CameraSaveState: SaveStateBase
    {
        public WorldLocation Location { get; set; }
        public float FieldOfView { get; set; }
        public WorldLocation TargetLocation { get; set; }
        public int AttachedTrainCarIndex { get; set; }
        public float Distance { get; set; }
        public bool BrowseTracking { get; set; }
        public bool BrowseForward {  get; set; }
        public bool BrowseBackward { get; set;}
        public Vector3 TrackingPosition { get; set; }
        public Vector3 TrackingRotation { get; set; }
        public Vector3 TrackingRotationStart { get; set; }
        public int SideLocation { get; set; }
        public bool UsingRearCab {  get; set; }
        public int CurrentViewPoint { get; set; }
        public int PreviousViewPoint { get; set; }
        public string CarId { get; set; }
    }
}
