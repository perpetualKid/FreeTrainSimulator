using System;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class LoadingDataState : SaveStateBase
    {
        public string DataKey { get; set; }
        public TimeSpan LoadingDuration { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<long> Samples { get; set; } = new Collection<long>();
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
