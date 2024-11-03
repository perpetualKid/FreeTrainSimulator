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
        public static async Task<ProfileModel> ConvertContent(ProfileModel profileModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            if (profileModel.RefreshRequired || refresh)
            {
                profileModel = await ProfileModelHandler.Expand(profileModel, cancellationToken).ConfigureAwait(false);

                await Parallel.ForEachAsync(profileModel.ContentFolders, async (folderModel, cancellationToken) =>
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
