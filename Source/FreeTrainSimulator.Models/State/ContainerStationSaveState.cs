using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace Orts.Models.State
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct ContainerStackItem
#pragma warning restore CA1815 // Override equals and operator equals on value types
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
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<ContainerStackItem> ContainerStacks { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
