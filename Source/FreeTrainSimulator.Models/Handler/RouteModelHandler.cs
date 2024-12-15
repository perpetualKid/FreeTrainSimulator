using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class RouteModelHandler : ContentHandlerBase<RouteModelCore>
    {
        public static Task<RouteModelCore> GetCore(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return GetCore(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static Task<RouteModelCore> GetCore(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!modelTaskCache.TryGetValue(key, out Task<RouteModelCore> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(routeId, folderModel, cancellationToken);
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static ValueTask<RouteModel> GetExtended(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return routeModel is RouteModel routeModelExtended ? ValueTask.FromResult(routeModelExtended) : GetExtended(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static async ValueTask<RouteModel> GetExtended(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!modelTaskCache.TryGetValue(key, out Task<RouteModelCore> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not RouteModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<RouteModel, FolderModel>(routeId, folderModel, cancellationToken));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as RouteModel;
        }

        public static Task<FrozenSet<RouteModelCore>> GetRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<RouteModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadRoutes(folderModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<FrozenSet<RouteModelCore>> LoadRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardSavePattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string routeId = Path.GetFileNameWithoutExtension(file);

                    if (routeId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        routeId = routeId[..^fileExtension.Length];

                    RouteModelCore route = await GetCore(routeId, folderModel, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }
    }
}