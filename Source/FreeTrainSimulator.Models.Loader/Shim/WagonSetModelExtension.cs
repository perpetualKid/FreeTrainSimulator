﻿using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class WagonSetModelExtension
    {
        public static WagonReferenceModel Any(this FrozenSet<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
    }
}