using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal class TimetableModelHandler : ContentHandlerBase<TimetableModel>
    {
        public static Task<TimetableModel> GetCore(TimetableModel timetableModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));
            return GetCore(timetableModel.Id, timetableModel.Parent, cancellationToken);
        }

        public static Task<TimetableModel> GetCore(string timetableId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(timetableId);

            if (!modelTaskCache.TryGetValue(key, out Task<TimetableModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(timetableId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static Task<ImmutableArray<TimetableModel>> GetTimetables(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<TimetableModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadTimetables(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<TimetableModel>> LoadTimetables(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string timetablesFolder = ModelFileResolver<TimetableModel>.FolderPath(routeModel);
            string pattern = ModelFileResolver<TimetableModel>.WildcardSavePattern;

            ConcurrentBag<TimetableModel> results = new ConcurrentBag<TimetableModel>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(timetablesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(timetablesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string timetableId = Path.GetFileNameWithoutExtension(file);

                    if (timetableId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        timetableId = timetableId[..^fileExtension.Length];

                    TimetableModel timetable = await GetCore(timetableId, routeModel, token).ConfigureAwait(false);
                    if (null != timetable)
                        results.Add(timetable);
                }).ConfigureAwait(false);
            }
            return results.ToImmutableArray();
        }
    }
}
