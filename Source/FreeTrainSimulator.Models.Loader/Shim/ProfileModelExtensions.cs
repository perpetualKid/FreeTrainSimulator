using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ProfileModelExtensions
    {
        public static ProfileModel Default(this ProfileModel _) => ProfileModelHandler.DefaultProfile;

        public static async ValueTask<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            return await ProfileModelHandler.Get(profileModel?.Name, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> FolderModel(this ProfileModel profileModel, string folderName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentException.ThrowIfNullOrEmpty(folderName, nameof(folderName));

            return await FolderModelHandler.Get(folderName, profileModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ProfileSelectionsModel> SelectionsModel(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            ProfileSelectionsModel selectionsModel = await ContentHandlerBase<ProfileSelectionsModel, ProfileSelectionsModel>.FromFile(profileModel.Name, profileModel, cancellationToken).ConfigureAwait(false);
            if (selectionsModel == null)
            {
                selectionsModel = new ProfileSelectionsModel() { Name = profileModel.Name };
                selectionsModel.Initialize(ModelFileResolver<ProfileSelectionsModel>.FilePath(selectionsModel, profileModel), profileModel);
            }
            return selectionsModel;
        }

        public static async ValueTask<ProfileSelectionsModel> SelectionFromActivity(this ProfileModel profileModel, ProfileSelectionsModel selectionsModel, RouteModelCore routeModel, ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));

            return (selectionsModel ?? await profileModel.SelectionsModel(cancellationToken).ConfigureAwait(false) with 
            {
                ActivityType = Common.ActivityType.Activity,
                ActivityName = activityModel.Name,
                Season = activityModel.Season,
                Weather = activityModel.Weather,
                StartTime = activityModel.StartTime,
                PathName = activityModel.PathId,
            });
        }

        public static async ValueTask<ProfileSelectionsModel> UpdateSelectionsModel(this ProfileModel profileModel, ProfileSelectionsModel selectionsModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(selectionsModel, nameof(selectionsModel));

            return await ContentHandlerBase<ProfileSelectionsModel, ProfileSelectionsModel>.ToFile(selectionsModel, cancellationToken).ConfigureAwait(false);
        }


        public static async ValueTask<ProfileModel> Convert(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            return await ProfileModelHandler.Convert(profileModel?.Name, folders, cancellationToken).ConfigureAwait(false);
        }
    }
}
