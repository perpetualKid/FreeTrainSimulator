using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.ImportHandler
{
    internal sealed class ProfileModelHandler : ContentHandlerBase<ProfileModel>
    {
        private const string root = "root";

        public static Task<ProfileModel> Expand(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Name;

            Task<ProfileModel> modelTask = Convert(profileModel, cancellationToken);
            modelTaskCache[key] = modelTask;
            collectionUpdateRequired[root] = true;

            return modelTask;
        }

        private static async Task<ProfileModel> Convert(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            profileModel = profileModel with
            {
                ContentFolders = await FolderModelHandler.ExpandFolderModels(profileModel, cancellationToken).ConfigureAwait(false)
            };
            await Create(profileModel, ProfileModel.None, cancellationToken).ConfigureAwait(false);
            return profileModel;
        }
    }
}
