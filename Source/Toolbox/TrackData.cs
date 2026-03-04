using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Toolbox
{
    public class TrackData : RuntimeData
    {
        internal static async ValueTask LoadTrackData(RouteModel routeModel, bool? metricUnitPreference, CancellationToken cancellationToken)
        {
            List<Task> loadTasks = new List<Task>();
            TrackSectionModel trackSectionModel = null;
            TrackDB trackDB = null;
            RoadTrackDB roadTrackDB = null;
            SignalConfigurationFile signalConfig = null;

            FolderStructure.ContentFolder.RouteFolder routeFolder = routeModel.MstsRouteFolder();

            loadTasks.Add(Task.Run(async () => trackSectionModel = await routeModel.GetTrackSectionModel(cancellationToken).ConfigureAwait(false)));
            loadTasks.Add(Task.Run(() =>
            {
                string tdbFile = routeFolder.TrackDatabaseFile(routeModel.RouteKey);
                if (!System.IO.File.Exists(tdbFile))
                {
                    Trace.TraceError($"Track Database File not found in {tdbFile}");
                    return;
                }
                trackDB = new TrackDatabaseFile(tdbFile).TrackDB;
            }, cancellationToken));
            loadTasks.Add(Task.Run(() =>
            {
                string rdbFile = routeFolder.RoadTrackDatabaseFile(routeModel.RouteKey);
                if (!System.IO.File.Exists(rdbFile))
                {
                    Trace.TraceWarning($"Road Database File not found in {rdbFile}");
                    return;
                }
                roadTrackDB = new RoadDatabaseFile(rdbFile).RoadTrackDB;
            }, cancellationToken));
            loadTasks.Add(Task.Run(() => signalConfig = new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile), cancellationToken));

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            Initialize(routeModel, trackSectionModel, trackDB, roadTrackDB, signalConfig, metricUnitPreference.GetValueOrDefault(routeModel.MetricUnits));
        }
    }
}
