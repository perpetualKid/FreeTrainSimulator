using System.Collections.Frozen;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class WagonSetModelExtension
    {
        public static WagonReferenceModel Any(this FrozenSet<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
        public static string SourceFile(this WagonSetModel wagonSetModel) => wagonSetModel?.Parent.MstsContentFolder().ConsistFile(wagonSetModel.Tags[WagonSetModelHandler.SourceNameKey]);
    }
}
