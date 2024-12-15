using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ProfileModelExtensions
    {
        public static Task<ProfileModel> Current(this ProfileModel _, CancellationToken cancellationToken) => ProfileModelHandler.Current(cancellationToken);
        public static Task UpdateCurrent(this ProfileModel profileModel, CancellationToken cancellationToken) => ProfileModelHandler.UpdateCurrent(profileModel, cancellationToken);
        public static Task<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken) => Get(null, profileModel?.Name, cancellationToken);
        public static Task<ProfileModel> Get(this ProfileModel _, string profileName, CancellationToken cancellationToken) => ProfileModelHandler.GetCore(profileName, cancellationToken);
        public static Task<FrozenSet<ProfileModel>> GetProfiles(this ProfileModel _, CancellationToken cancellationToken) =>
            ProfileModelHandler.GetProfiles(cancellationToken);
        public static Task<ProfileModel> Empty(this ProfileModel profileModel, CancellationToken cancellationToken) => Task.FromResult(ProfileModel.None);// Setup(null, profileModel?.Name, null, cancellationToken);
        //public static Task<ProfileModel> Setup(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => Setup(null, profileModel?.Name, folders, cancellationToken);
        //public static Task<ProfileModel> Setup(this ProfileModel _, string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => ProfileModelHandler.Setup(profileName, folders, cancellationToken);
    }
}
