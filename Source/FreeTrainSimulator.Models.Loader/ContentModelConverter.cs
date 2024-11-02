using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader
{
    public static class ContentModelConverter
    {
        public static async Task<ProfileModel> Convert(ProfileModel profileModel, bool force, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            if (profileModel.RefreshRequired || force)
            {
                profileModel = await profileModel.Get(cancellationToken).ConfigureAwait(false);

                FrozenSet<FolderModel> folders = await FolderModelHandler.ExpandFolderModels(profileModel, cancellationToken).ConfigureAwait(false);
                await Parallel.ForEachAsync(folders, async (folderModel, cancellationToken) =>
                {
                    Task<FrozenSet<RouteModelCore>> routesTask = RouteModelHandler.ExpandRouteModels(folderModel, cancellationToken);
                    Task<FrozenSet<WagonSetModel>> wagonSetsTask = WagonSetModelHandler.ExpandWagonSetModels(folderModel, cancellationToken);

                    await Task.WhenAll(wagonSetsTask, routesTask).ConfigureAwait(false);

                    await Parallel.ForEachAsync(routesTask.Result, async (routeModel, cancellationToken) =>
                    {
                        await Task.WhenAll(
                            PathModelHandler.ExpandPathModels(routeModel, cancellationToken),
                            ActivityModelHandler.ExpandActivityModels(routeModel, cancellationToken)).ConfigureAwait(false);
                    });
                });
            }
            return profileModel;
        }
    }
}
