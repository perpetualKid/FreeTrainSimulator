using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class DeadlockInfoSaveState : SaveStateBase
    {
        public int DeadlockIndex { get; set; }
        public Collection<DeadlockPathInfoSaveState> AvailablePaths { get; set; }
        public Dictionary<int, List<int>> PathReferences { get; set; }
        public Dictionary<int, List<int>> TrainReferences { get; set; }
        public Dictionary<int, Dictionary<int, bool>> TrainLengthFit { get; set; }
        public Dictionary<int, int> TrainOwnPath {  get; set; }
        public Dictionary<int, int> InverseInfo { get; set; }
        public Dictionary<int, Dictionary<int, int>> TrainSubpathIndex { get; set; }
        public int NextTrainSubpathIndex { get; set; }
    }
}
