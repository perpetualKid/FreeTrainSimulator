using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
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
