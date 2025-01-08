using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;

using Orts.Formats.OpenRails.Files;
using Orts.Formats.OpenRails.Models;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails
{
    internal class TimetableModelHandler : ContentHandlerBase<TimetableModel>
    {
        internal const string SourceNameKey = "OrSourceRoute";

        public static async Task<FrozenSet<TimetableModel>> ExpandTimetableModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            ConcurrentBag<TimetableModel> results = new ConcurrentBag<TimetableModel>();

            string sourceFolder = routeModel.MstsRouteFolder().OpenRailsActivitiesFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentBag<string> consistFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, "*.timetable*or"));

                await Parallel.ForEachAsync(consistFiles, cancellationToken, async (consistFile, token) =>
                {
                    Task<TimetableModel> modelTask = Convert(consistFile, routeModel, cancellationToken);

                    TimetableModel timetableModel = await modelTask.ConfigureAwait(false);
                    string key = timetableModel.Hierarchy();
                    results.Add(timetableModel);
                    modelTaskCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<TimetableModel> result = results.ToFrozenSet();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<TimetableModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            static IEnumerable<TimetableTrainModel> BuildTrains(IEnumerable<TrainInformation> trains, string group)
            {
                return trains.Select(t =>
                {
                    if (!TimeOnly.TryParse(t.StartTime.Max(5), out TimeOnly startTime))
                        startTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12));
                    return new TimetableTrainModel()
                    {
                        Id = t.Train,
                        Name = t.Train,
                        Briefing = t.Briefing,
                        Group = group,
                        Path = t.Path,
                        WagonSet = t.LeadingConsist,
                        WagonSetReverse = t.ReverseConsist,
                        StartTime = startTime,
                    };
                });
            }

            if (File.Exists(filePath))
            {
                TimetableModel timetableModel;

                if (Path.GetExtension(filePath).StartsWith(".timetablelist", StringComparison.OrdinalIgnoreCase))
                {
                    TimetableGroupFile groupFile = new TimetableGroupFile(filePath);
                    timetableModel = new TimetableModel()
                    {
                        Id = Path.GetFileNameWithoutExtension(filePath),
                        Name = groupFile.Description,
                        TimetableTrains = groupFile.TimeTables.SelectMany(timetable => BuildTrains(timetable.Trains, timetable.Description))?.ToFrozenSet() ?? FrozenSet<TimetableTrainModel>.Empty,
                        Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileName(filePath) } },
                    };
                }
                else
                {
                    TimetableFile timetableFile = new TimetableFile(filePath);

                    timetableModel = new TimetableModel()
                    {
                        Id = Path.GetFileNameWithoutExtension(filePath),
                        Name = timetableFile.Description,
                        TimetableTrains = BuildTrains(timetableFile.Trains, timetableFile.Description)?.ToFrozenSet() ?? FrozenSet<TimetableTrainModel>.Empty,
                        Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileName(filePath) } },
                    };
                }

                await Create(timetableModel, routeModel, cancellationToken).ConfigureAwait(false);
                return timetableModel;
            }
            else
            {
                Trace.TraceWarning($"Timetable file {filePath} refers to non-existing file.");
                return null;
            }
        }

    }
}
