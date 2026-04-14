using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for <see cref="WagonReferenceModel"/> providing shared sentinel instances:
    /// <see cref="Missing"/> for unresolved wagon references and <see cref="LocomotiveAny"/>
    /// as a wildcard "any locomotive" placeholder for UI selection lists.
    /// </summary>
    internal class WagonReferenceHandler : ContentHandlerBase<WagonReferenceModel>
    {
        public static WagonReferenceModel Missing = new WagonReferenceModel()
        {
            Id = "<unknown>",
            Name = "Missing",
        };

        public static WagonReferenceModel LocomotiveAny = new WagonReferenceModel()
        {
            Id = "<Any>",
            Name = "- Any Locomotive -",
        };
    }
}
