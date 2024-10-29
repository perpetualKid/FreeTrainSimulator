using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader
{
    public static class ContentModelConverter
    {
        public static async Task Convert(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            if (profileModel.SetupRequired())
            {
                profileModel = await profileModel.Get(cancellationToken).ConfigureAwait(false);
                FrozenSet<FolderModel> folders = await profileModel.GetFolders(cancellationToken).ConfigureAwait(false);
                await Parallel.ForEachAsync(folders, async (folderModel, cancellationToken) =>
                {
                    folderModel = await folderModel.Get(cancellationToken).ConfigureAwait(false);

                });
            }
        }
    }
}
