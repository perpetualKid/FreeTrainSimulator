using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class WagonSetModelExtension
    {
        public static WagonReferenceModel Any(this ImmutableArray<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
    }
}
