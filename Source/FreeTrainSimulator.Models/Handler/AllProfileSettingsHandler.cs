using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal class AllProfileSettingsHandler: ContentHandlerBase<AllProfileSettingsModel>
    {
        private const string root = "root";

        public static async Task<AllProfileSettingsModel> GetCore(CancellationToken cancellationToken)
        {
            if (!modelTaskCache.TryGetValue(root, out Task<AllProfileSettingsModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[root] = modelTask = ProfileSettingModelHandler<AllProfileSettingsModel>.FromFile(null, cancellationToken);
            }
            return await modelTask.ConfigureAwait(false);
        }

        public static async Task SetUpdateMode(UpdateMode updateMode, CancellationToken cancellationToken)
        {
            AllProfileSettingsModel currentProfileSettingsModel = ((await GetCore(cancellationToken).ConfigureAwait(false)) ?? new AllProfileSettingsModel()) with
            {
                UpdateMode = updateMode
            };
            modelTaskCache[root] = ToFile(currentProfileSettingsModel, cancellationToken);
        }

        public static async Task UpdateCurrent(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            AllProfileSettingsModel currentProfileSettingsModel = ((await GetCore(cancellationToken).ConfigureAwait(false)) ?? new AllProfileSettingsModel()) with
            {
                Profile = profileModel?.Name,
            };
            modelTaskCache[root] = ToFile(currentProfileSettingsModel, cancellationToken);
        }
    }
}
