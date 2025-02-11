using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

using Microsoft.Win32;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed class FolderModelImportHandler : ContentHandlerBase<FolderModel>
    {
        private const string importKey = "$Import";

        internal static ImmutableArray<FolderModel> InitialFolderImport(ContentModel contentModel)
        {
            if (!modelSetTaskCache.TryGetValue(importKey, out Task<ImmutableArray<FolderModel>> modelSetTask))
            {

                List<FolderModel> folderModels = new List<FolderModel>();

                string location = "SOFTWARE\\OpenRails\\ORTS\\Folders";
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(location);

                    foreach (string folder in key.GetValueNames())
                    {
                        folderModels.Add(new FolderModel(folder, key.GetValue(folder) as string, contentModel));
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Could not import existing content folders {ex.Message}.");
                }
                modelSetTaskCache[importKey] = modelSetTask = Task.FromResult(folderModels.ToImmutableArray());
            }

            return modelSetTask.Result;
        }

        public static async Task<ImmutableArray<FolderModel>> ExpandFolderModels(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            Dictionary<string, FolderModel> configuredFolders = new Dictionary<string, FolderModel>(contentModel.ContentFolders.ToDictionary(f => f.Id), StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(configuredFolders, cancellationToken, async (folderModelHolder, token) =>
            {
                Task<FolderModel> modelTask = Convert(folderModelHolder.Value, cancellationToken);
                FolderModel folderModel = await modelTask.ConfigureAwait(false);
                string key = folderModel.Hierarchy();
                results.Add(folderModel);
                modelTaskCache[key] = modelTask;
            }).ConfigureAwait(false);

            ImmutableArray<FolderModel> result = results.ToImmutableArray();
            string key = contentModel.Hierarchy();
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
