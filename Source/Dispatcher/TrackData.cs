﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Environment;
using FreeTrainSimulator.Models.Simplified;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Dispatcher
{
    public class TrackData : RuntimeData
    {
        public IEnumerable<Path> TrainPaths { get; }

        private TrackData(RouteModel route, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool metricUnits, IEnumerable<Path> trainPaths) :
            base(route, trackSections, trackDb, roadTrackDB, signalConfig, metricUnits, null)
        {
            TrainPaths = trainPaths;
        }

        internal static async Task LoadTrackData(Game game, FolderStructure.ContentFolder.RouteFolder routeFolder, bool? metricUnitPreference, CancellationToken cancellationToken)
        {
            List<Task> loadTasks = new List<Task>();
            TrackSectionsFile trackSections = null;
            TrackDB trackDB = null;
            RoadTrackDB roadTrackDB = null;
            SignalConfigurationFile signalConfig = null;

            RouteFile routeFile = new RouteFile(routeFolder.TrackFileName);

            loadTasks.Add(Task.Run(() =>
            {
                string tdbFile = routeFolder.TrackDatabaseFile(routeFile.Route.FileName);
                if (!System.IO.File.Exists(tdbFile))
                {
                    Trace.TraceError($"Track Database File not found in {tdbFile}");
                    return;
                }
                trackDB = new TrackDatabaseFile(tdbFile).TrackDB;
            }, cancellationToken));
            loadTasks.Add(Task.Run(() =>
            {
                trackSections = new TrackSectionsFile(routeFolder.TrackSectionFile);
                if (System.IO.File.Exists(routeFolder.RouteTrackSectionFile))
                    trackSections.AddRouteTSectionDatFile(routeFolder.RouteTrackSectionFile);
            }, cancellationToken));
            loadTasks.Add(Task.Run(() =>
            {
                string rdbFile = routeFolder.RoadTrackDatabaseFile(routeFile.Route.FileName);
                if (!System.IO.File.Exists(rdbFile))
                {
                    Trace.TraceWarning($"Road Database File not found in {rdbFile}");
                    return;
                }
                roadTrackDB = new RoadDatabaseFile(rdbFile).RoadTrackDB;
            }, cancellationToken));
            loadTasks.Add(Task.Run(() => signalConfig = new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile), cancellationToken));
            Task<IEnumerable<Path>> pathTask;
            loadTasks.Add(pathTask = Path.GetPaths(routeFolder.PathsFolder, true, cancellationToken));

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            game.Services.RemoveService(typeof(RuntimeData));
            game.Services.AddService(typeof(RuntimeData), new TrackData(routeFile.Route.RouteData, trackSections, trackDB, roadTrackDB, signalConfig, metricUnitPreference.GetValueOrDefault(routeFile.Route.MilepostUnitsMetric), await pathTask.ConfigureAwait(false)));
        }
    }
}