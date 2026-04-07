using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Toolbox
{
    public class TrackData : RuntimeData
    {
        internal static async ValueTask LoadTrackData(RouteModel routeModel, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();

            Task<SignalConfigurationFile> signalConfigTask = Task.Run(() => new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile), cancellationToken);

            await Task.WhenAll(signalConfigTask).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            Initialize(routeModel, await signalConfigTask.ConfigureAwait(false));
        }
    }
}
