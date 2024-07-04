using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class AiPathNodeSaveState: SaveStateBase
    {
        public int Index { get; set; }
        public TrainPathNodeType NodeType { get; set; }
        public int WaitTime { get; set; }
        public int WaitUntil { get; set; }
        public int NextMainNodeIndex { get; set; }
        public int NextMainTrackVectorNodeIndex { get; set; }
        public int NextSidingNodeIndex { get; set; }
        public int NextSidingTrackVectorNodeIndex { get; set; }
        public int JunctionIndex { get; set; }
        public bool FacingJunction { get; set; }
        public WorldLocation Location { get; set; }
    }
}
