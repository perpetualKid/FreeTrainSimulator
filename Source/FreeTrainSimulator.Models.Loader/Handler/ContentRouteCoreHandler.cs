using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ContentRouteCoreHandler : ContentHandlerBase<RouteModelCore, RouteModelCore>
    {
        public static async ValueTask<RouteModelCore> Get(string name, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            return await FromFile(name, contentFolder, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> GetRoutes(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardSavePattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    RouteModelCore route = await FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        public static async ValueTask<RouteModelCore> Load(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            routeModel.InitializeWith(await ContentPathCoreHandler.GetPaths(routeModel, cancellationToken).ConfigureAwait(false));
            IFileResolve parent = (routeModel as IFileResolve).Container;
            routeModel.Initialize(ModelFileResolver<RouteModelCore>.FilePath(routeModel, parent), parent);
            routeModel.RefreshModel();
            return routeModel;
        }

        public static async ValueTask<RouteModelCore> ConvertPathModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string pathsFolder = ModelFileResolver<PathModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardPattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();
            // preload existing MSTS files
            ConcurrentDictionary<string, string> pathFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(routeModel.MstsRouteFolder().PathsFolder, "*.pat").
                ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    PathModelCore pathModel = await ContentPathCoreHandler.FromFile(file, routeModel, token, false).ConfigureAwait(false);
                    if (pathModel != null && pathFiles.Remove(pathModel.Tag, out string filePath)) //
                    {
                        if (pathModel.SetupRequired())
                            pathModel = await ContentPathHandler.Convert(filePath, routeModel, token).ConfigureAwait(false);
                        results.Add(pathModel);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS path (remaining in the preloaded dictionary), Create a path model
            await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
            {
                PathModelCore pathModel = await ContentPathHandler.Convert(path.Value, routeModel, token).ConfigureAwait(false);
                if (null != pathModel)
                {
                    results.Add(pathModel);
                }
            }).ConfigureAwait(false);

            routeModel.InitializeWith(results.ToFrozenSet());
            return routeModel;
        }
    }
}
