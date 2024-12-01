using System.Collections.Frozen;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class WagonSetModelExtension
    {
        public static WagonReferenceModel Any(this FrozenSet<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
    }
}
