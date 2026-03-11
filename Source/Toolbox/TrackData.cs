using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Toolbox
{
    public class TrackData : RuntimeData
    {
        internal static async ValueTask LoadTrackData(RouteModel routeModel, bool? metricUnitPreference, CancellationToken cancellationToken)
        {
            List<Task> loadTasks = new List<Task>();

            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();

            Task<TrackSectionsModel> tracksectionModelTask = routeModel.GetTrackSectionModel(cancellationToken);
            Task<TrackModel> trackModelTask = routeModel.GetTrackModel(cancellationToken);
            Task<SignalConfigurationFile> signalConfigTask = Task.Run(() => new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile), cancellationToken);

            await Task.WhenAll(tracksectionModelTask, trackModelTask, signalConfigTask).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            Initialize(routeModel, await tracksectionModelTask, await trackModelTask, await signalConfigTask, metricUnitPreference.GetValueOrDefault(routeModel.MetricUnits));
        }
    }
}
