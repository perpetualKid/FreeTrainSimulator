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
            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();

            Task<TrackSectionModel> tracksectionModelTask = routeModel.GetTrackSectionModel(cancellationToken);
            Task<SignalConfigurationFile> signalConfigTask = Task.Run(() => new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile), cancellationToken);

            await Task.WhenAll(tracksectionModelTask, signalConfigTask).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            Initialize(routeModel, await tracksectionModelTask.ConfigureAwait(false), await signalConfigTask.ConfigureAwait(false), 
                metricUnitPreference.GetValueOrDefault(routeModel.MetricUnits));
        }
    }
}
