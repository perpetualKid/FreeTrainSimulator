using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class PathModelExtensions
    {
        public static string SourceFile(this PathModelCore pathModel) => pathModel?.Parent.MstsRouteFolder().PathFile(pathModel.Tags[PathModelImportHandler.SourceNameKey]);
    }
}
