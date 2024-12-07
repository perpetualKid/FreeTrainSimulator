using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ProfileModelExtensions
    {
        public static Task<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken) => Get(null, profileModel?.Name, cancellationToken);
        public static Task<ProfileModel> Get(this ProfileModel _, string profileName, CancellationToken cancellationToken) => ProfileModelHandler.GetCore(profileName, cancellationToken);
        public static Task<FrozenSet<ProfileModel>> GetProfiles(this ProfileModel _, CancellationToken cancellationToken) =>
            ProfileModelHandler.GetProfiles(cancellationToken);
        public static ValueTask<FrozenSet<FolderModel>> GetFolders(this ProfileModel profileModel, CancellationToken cancellationToken) =>
            FolderModelHandler.GetFolders(profileModel, cancellationToken);
        public static Task<ProfileModel> Empty(this ProfileModel profileModel, CancellationToken cancellationToken) => Setup(null, profileModel?.Name, null, cancellationToken);
        public static Task<ProfileModel> Setup(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => Setup(null, profileModel?.Name, folders, cancellationToken);
        public static Task<ProfileModel> Setup(this ProfileModel _, string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => ProfileModelHandler.Setup(profileName, folders, cancellationToken);

        #region settings
        public static async ValueTask<T> LoadSettingsModel<T>(this ProfileModel profileModel, CancellationToken cancellationToken) where T: ModelBase<T>, new()
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            T settingsModel = new T() { Id = profileModel.Name, Name = profileModel.Name };
            settingsModel.Initialize(ModelFileResolver<T>.FilePath(settingsModel, profileModel), profileModel);

            return settingsModel.Parent is not ProfileModel
                ? throw new InvalidCastException($"{nameof(T)} needs to be a ProfileModel child.")
                : await ContentHandlerBase<T>.FromFile(profileModel.Name, settingsModel, profileModel, cancellationToken).ConfigureAwait(false);
        }

        public static Task<T> UpdateSettingsModel<T>(this ProfileModel profileModel, T settingsModel, CancellationToken cancellationToken) where T : ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(settingsModel, nameof(settingsModel));

            return settingsModel.Parent is not ProfileModel
                ? throw new InvalidCastException($"{nameof(settingsModel)} needs to be a ProfileModel child.")
                : ContentHandlerBase<T>.ToFile(settingsModel, cancellationToken);
        }
        #endregion
    }
}
