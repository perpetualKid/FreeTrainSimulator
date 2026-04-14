using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    /// <summary>
    /// Extension methods for wagon-set model collections, providing a sentinel "Any Locomotive"
    /// reference for UI selection lists.
    /// </summary>
    public static class WagonSetModelExtensions
    {
        public static WagonReferenceModel Any(this ImmutableArray<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
    }
}
