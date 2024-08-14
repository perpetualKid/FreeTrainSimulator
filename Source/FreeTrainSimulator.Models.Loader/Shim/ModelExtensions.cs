using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ContentModelExtensions
    {
        public static bool SetupRequired<T>(this ModelBase<T> model) where T : ModelBase<T> => model == null || model.RefreshRequired;
    }

    public static class ProfileModelExtensions
    {
        public static async ValueTask<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            return await ContentProfileHandler.Get(profileModel?.Name, CancellationToken.None).ConfigureAwait(true);
        }

        public static async ValueTask<ProfileModel> Convert(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            return await ContentProfileHandler.Convert(profileModel?.Name, folders, CancellationToken.None).ConfigureAwait(true);
        }
    }
}
