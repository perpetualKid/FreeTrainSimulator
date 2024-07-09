﻿using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TriggerActivationSaveState: SaveStateBase
    {
        public int ActivatedTrain { get; set; }
        public TriggerActivationType ActivationType { get; set; }
        public int PlatformId { get; set; }
        public string ActivatedTrainName { get; set; }
    }
}