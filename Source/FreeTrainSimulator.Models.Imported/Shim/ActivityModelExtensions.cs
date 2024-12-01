using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class ActivityModelExtensions
    {
        public static string SourceFile(this ActivityModelCore activityModel) => activityModel?.Parent.MstsRouteFolder().ActivityFile(activityModel.Tags[ActivityModelHandler.SourceNameKey]);
    }
}
