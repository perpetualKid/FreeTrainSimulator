using System.Collections.Frozen;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class WagonSetModelExtension
    {
        public static string SourceFile(this WagonSetModel wagonSetModel) => wagonSetModel?.Parent.MstsContentFolder().ConsistFile(wagonSetModel.Tags[WagonSetModelImportHandler.SourceNameKey]);
    }
}
