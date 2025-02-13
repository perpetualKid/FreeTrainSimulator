using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class RouteModelHandler : ContentHandlerBase<RouteModelHeader>
    {
        public static Task<RouteModelHeader> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return GetCore(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static Task<RouteModelHeader> GetCore(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!modelTaskCache.TryGetValue(key, out Task<RouteModelHeader> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(routeId, folderModel, cancellationToken);
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static ValueTask<RouteModel> GetExtended(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return routeModel is RouteModel routeModelExtended ? ValueTask.FromResult(routeModelExtended) : GetExtended(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static async ValueTask<RouteModel> GetExtended(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!modelTaskCache.TryGetValue(key, out Task<RouteModelHeader> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not RouteModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<RouteModel, FolderModel>(routeId, folderModel, cancellationToken));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as RouteModel;
        }

        public static Task<ImmutableArray<RouteModelHeader>> GetRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<RouteModelHeader>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadRoutes(folderModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<RouteModelHeader>> LoadRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<RouteModelHeader>.WildcardSavePattern;

            ConcurrentBag<RouteModelHeader> results = new ConcurrentBag<RouteModelHeader>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string routeId = Path.GetFileNameWithoutExtension(file);

                    if (routeId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        routeId = routeId[..^fileExtension.Length];

                    RouteModelHeader route = await GetCore(routeId, folderModel, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToImmutableArray();
        }
    }
}