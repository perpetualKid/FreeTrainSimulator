using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace Orts.Toolbox
{
    public class TrackData: RuntimeData
    {
        private TrackData(string routeName, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool useMetricUnits) : 
            base(routeName, trackSections, trackDb, roadTrackDB, signalConfig, useMetricUnits, null)
        {
        }

        internal static async Task LoadTrackData(Game game, string routePath, bool? useMetricUnits, CancellationToken cancellationToken)
        {
            TrackModel.Instance<RailTrackModel>(game)?.Reset();
            List<Task> loadTasks = new List<Task>();
            TrackSectionsFile trackSections = null;
            TrackDB trackDB = null;
            RoadTrackDB roadTrackDB = null;
            SignalConfigurationFile signalConfig = null;

            FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);
            RouteFile routeFile = new RouteFile(routeFolder.TrackFileName);

            loadTasks.Add(Task.Run(() =>
            {
                string tdbFile = routeFolder.TrackDatabaseFile(routeFile.Route.FileName);
                if (!File.Exists(tdbFile))
                {
                    Trace.TraceError($"Track Database File not found in {tdbFile}");
                    return;
                }
                trackDB = new TrackDatabaseFile(tdbFile).TrackDB;
            }, cancellationToken));
            loadTasks.Add(Task.Run(() =>
            {
                trackSections = new TrackSectionsFile(routeFolder.TrackSectionFile);
                if (File.Exists(routeFolder.RouteTrackSectionFile))
                    trackSections.AddRouteTSectionDatFile(routeFolder.RouteTrackSectionFile);
            }, cancellationToken));
            loadTasks.Add(Task.Run(() =>
            {
                string rdbFile = routeFolder.RoadTrackDatabaseFile(routeFile.Route.FileName);
                if (!File.Exists(rdbFile))
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

            game.Services.RemoveService(typeof(RuntimeData));
            game.Services.AddService(typeof(RuntimeData), new TrackData(routeFile.Route.Name, trackSections, trackDB, roadTrackDB, signalConfig, useMetricUnits.GetValueOrDefault(routeFile.Route.MilepostUnitsMetric)));
        }
    }
}
