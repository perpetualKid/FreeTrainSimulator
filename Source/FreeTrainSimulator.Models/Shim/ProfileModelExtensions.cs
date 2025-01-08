using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ProfileModelExtensions
    {
        #region common for all profile models
        public static async Task<UpdateMode> AllProfileGetUpdateMode(this ProfileModel _, CancellationToken cancellationToken) => (await AllProfileSettingsHandler.GetCore(cancellationToken).ConfigureAwait(false))?.UpdateMode ?? UpdateMode.Release;
        public static Task AllProfileSetUpdateMode(this UpdateMode updateMode, CancellationToken cancellationToken) => AllProfileSettingsHandler.SetUpdateMode(updateMode, cancellationToken);
        #endregion
        public static Task<ProfileModel> Current(this ProfileModel _, CancellationToken cancellationToken) => ProfileModelHandler.Current(cancellationToken);
        public static Task UpdateCurrent(this ProfileModel profileModel, CancellationToken cancellationToken) => AllProfileSettingsHandler.UpdateCurrent(profileModel, cancellationToken);
        public static Task<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken) => Get(null, profileModel?.Name, cancellationToken);
        public static Task<ProfileModel> Get(this ProfileModel _, string profileName, CancellationToken cancellationToken) => ProfileModelHandler.GetCore(profileName, cancellationToken);
        public static Task<FrozenSet<ProfileModel>> GetProfiles(this ProfileModel _, CancellationToken cancellationToken) => ProfileModelHandler.GetProfiles(cancellationToken);
        public static Task<FrozenSet<ProfileModel>> Create(this ProfileModel profileModel, CancellationToken cancellationToken) => ProfileModelHandler.Create(profileModel, cancellationToken);
        public static Task<FrozenSet<ProfileModel>> Delete(this ProfileModel profileModel, CancellationToken cancellationToken) => ProfileModelHandler.Delete(profileModel, cancellationToken);
    }
}
