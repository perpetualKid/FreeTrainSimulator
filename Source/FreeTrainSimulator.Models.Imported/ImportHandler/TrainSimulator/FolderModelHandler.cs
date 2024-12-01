using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed class FolderModelHandler : ContentHandlerBase<FolderModel>
    {
        public static readonly FolderModel MstsFolder = new FolderModel("Train Simulator", FolderStructure.MstsFolder, null);

        public static async Task<FrozenSet<FolderModel>> ExpandFolderModels(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            Dictionary<string, FolderModel> configuredFolders = new Dictionary<string, FolderModel>(profileModel.ContentFolders.ToDictionary(f => f.Id), StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(configuredFolders, cancellationToken, async (folderModelHolder, token) =>
            {
                Task<FolderModel> modelTask = Convert(folderModelHolder.Value, cancellationToken);
                FolderModel folderModel = await modelTask.ConfigureAwait(false);
                string key = folderModel.Hierarchy();
                results.Add(folderModel);
                modelTaskCache[key] = modelTask;
            }).ConfigureAwait(false);

            FrozenSet<FolderModel> result = results.ToFrozenSet();
            string key = profileModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            return result;
        }

        private static async Task<FolderModel> Convert(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            folderModel.RefreshModel();

            await Create(folderModel, folderModel.Parent, false, true, cancellationToken).ConfigureAwait(false);
            return folderModel;
        }
    }
}
