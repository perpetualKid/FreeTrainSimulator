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
        public static Task<FrozenSet<ProfileModel>> GetProfiles(this ProfileModel _, CancellationToken cancellationToken) => ProfileModelHandler.GetProfiles(cancellationToken);
    }
}
