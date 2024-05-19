using System.Collections.ObjectModel;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

using Orts.Common;

namespace Orts.Models.State
{
    public readonly struct ContainerStackItem
    {
        public readonly bool Usable;
        public readonly Collection<ContainerSaveState> Containers;
        
        public ContainerStackItem(bool usable, int count)
        { 
            Usable = usable;
            if (count > 0)
                Containers = new Collection<ContainerSaveState>(EnumerableExtension.PresetCollection<ContainerSaveState>(count));
        }
    }

    [MemoryPackable]
    public sealed partial class ContainerStationSaveState : SaveStateBase
    {
        public int ContainerStationId { get; set; }
        public ContainerStationStatus ContainerStationStatus { get; set; }
        public Matrix ContainerPosition { get; set; }
        public float VerticalOffset { get; set; }
        public Collection<ContainerStackItem> ContainerStacks { get; set; }
    }
}
