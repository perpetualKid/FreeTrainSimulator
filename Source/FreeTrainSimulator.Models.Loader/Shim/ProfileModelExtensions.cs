using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ProfileModelExtensions
    {
        public static ValueTask<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken) =>
            Get(null, profileModel?.Name, cancellationToken);
        public static ValueTask<ProfileModel> Get(this ProfileModel _, string profileName, CancellationToken cancellationToken) =>
            ProfileModelHandler.GetCore(profileName, cancellationToken);
        public static Task<ProfileModel> Setup(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) =>
            Setup(null, profileModel?.Name, folders, cancellationToken);
        public static Task<ProfileModel> Setup(this ProfileModel _, string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) =>
            ProfileModelHandler.Setup(profileName, folders, cancellationToken);
        public static ValueTask<FrozenSet<ProfileModel>> GetProfiles(this ProfileModel _, CancellationToken cancellationToken) =>
            ProfileModelHandler.GetProfiles(cancellationToken);
        public static ValueTask<FrozenSet<FolderModel>> GetFolders(this ProfileModel profileModel, CancellationToken cancellationToken) =>
            FolderModelHandler.GetFolders(profileModel, cancellationToken);
        
        public static Task<ProfileModel> GetOrCreate(this FrozenSet<ProfileModel> profiles, string profileName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profiles, nameof(profiles));
            profileName = ProfileModelHandler.CheckDefaultProfile(profileName);
            ProfileModel profileModel = profiles.GetByName(profileName);
            return null != profileModel
                ? Task.FromResult(profileModel)
                : Setup(null, profileName, Enumerable.Empty<(string, string)>(), cancellationToken);
        }

        public static async ValueTask<ProfileSelectionsModel> SelectionsModel(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            ProfileSelectionsModel selectionsModel = await ContentHandlerBase<ProfileSelectionsModel>.FromFile(profileModel.Name, profileModel, cancellationToken).ConfigureAwait(false);
            if (selectionsModel == null)
            {
                selectionsModel = new ProfileSelectionsModel() { Id = profileModel.Name, Name = profileModel.Name, ActivityType = Common.ActivityType.Activity };
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
                WagonSetName = activityModel.ConsistId,
            });
        }

        public static Task<ProfileSelectionsModel> UpdateSelectionsModel(this ProfileModel profileModel, ProfileSelectionsModel selectionsModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(selectionsModel, nameof(selectionsModel));

            return ContentHandlerBase<ProfileSelectionsModel>.ToFile(selectionsModel, cancellationToken);
        }

        public static Task<ProfileModel> Convert(this ProfileModel profileModel, bool force, CancellationToken cancellationToken)
        {
            return ContentModelConverter.ConvertContent(profileModel, force, cancellationToken);
        }

    }
}
