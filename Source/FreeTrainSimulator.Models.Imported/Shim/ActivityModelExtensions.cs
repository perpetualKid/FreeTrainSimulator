using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class ActivityModelExtensions
    {
        public static string SourceFile(this ActivityModelHeader activityModel) => activityModel?.Parent.MstsRouteFolder().ActivityFile(activityModel.Tags[ActivityModelImportHandler.SourceNameKey]);
    }
}
