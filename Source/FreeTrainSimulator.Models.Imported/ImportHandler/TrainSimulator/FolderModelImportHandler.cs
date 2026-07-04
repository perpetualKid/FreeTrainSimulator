using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
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
        private const string ortsFoldersKey = "SOFTWARE\\OpenRails\\ORTS\\Folders";

        internal static ImmutableArray<FolderModel> InitialFolderImport(ContentModel contentModel)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            if (!modelSetTaskCache.TryGetValue(importKey, out Task<ImmutableArray<FolderModel>> modelSetTask))
            {
                modelSetTaskCache[importKey] = modelSetTask = Task.FromResult(DiscoverLegacyFolders(contentModel));
            }

            return modelSetTask.Result;
        }

        internal static ImmutableArray<FolderModel> InitialFolderImportForRefresh(ContentModel contentModel)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            // Present the already configured folders (authoritative) together with any legacy-discovered
            // folders, so a forced content refresh still shows the user's previously added folders.
            return MergeFoldersForRefresh(contentModel, InitialFolderImport(contentModel));
        }

        public static async Task<ImmutableArray<FolderModel>> ExpandFolderModels(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();
            ImmutableArray<FolderModel> legacyFolders = DiscoverLegacyFolders(contentModel);
            ImmutableArray<FolderModel> mergedFolders = MergeFoldersForRefresh(contentModel, legacyFolders);

            await Parallel.ForEachAsync(mergedFolders, cancellationToken, async (folderModel, token) =>
            {
                Task<FolderModel> modelTask = Convert(folderModel, token);
                FolderModel refreshedFolderModel = await modelTask.ConfigureAwait(false);
                string key = refreshedFolderModel.Hierarchy();
                results.Add(refreshedFolderModel);
                modelTaskCache[key] = modelTask;
            }).ConfigureAwait(false);

            ImmutableArray<FolderModel> result = results
                .OrderBy(folder => folder?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(folder => folder?.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            // ContentModel is the singleton hierarchy root and intentionally has an empty Id
            // (its descriptor persists as 'Content\.content.save'), so label it for readable diagnostics.
            string contentModelId = string.IsNullOrEmpty(contentModel.Id) ? "content root" : contentModel.Id;
            Trace.TraceInformation($"Folder refresh merge for '{contentModelId}': configured={contentModel.ContentFolders.Length}, legacy={legacyFolders.Length}, merged={result.Length}");

            string key = contentModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            return result;
        }

        internal static ImmutableArray<FolderModel> MergeFoldersForRefresh(ContentModel contentModel, ImmutableArray<FolderModel> legacyFolders)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            // Configured folders are authoritative and added first; legacy-discovered folders only fill gaps.
            Dictionary<string, FolderModel> mergedFolders = new Dictionary<string, FolderModel>(StringComparer.OrdinalIgnoreCase);

            foreach (FolderModel folderModel in contentModel.ContentFolders)
            {
                if (folderModel != null)
                    _ = mergedFolders.TryAdd(ResolveFolderMergeKey(folderModel), folderModel);
            }

            foreach (FolderModel folderModel in legacyFolders)
            {
                if (folderModel != null)
                    _ = mergedFolders.TryAdd(ResolveFolderMergeKey(folderModel), folderModel);
            }

            return mergedFolders.Values.ToImmutableArray();
        }

        private static ImmutableArray<FolderModel> DiscoverLegacyFolders(ContentModel contentModel)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            List<FolderModel> folderModels = new List<FolderModel>();

            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(ortsFoldersKey);
                if (key != null)
                {
                    foreach (string folder in key.GetValueNames())
                    {
                        string contentPath = key.GetValue(folder) as string;
                        if (string.IsNullOrWhiteSpace(contentPath))
                            continue;

                        folderModels.Add(new FolderModel(folder, contentPath, contentModel));
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or ObjectDisposedException)
            {
                Trace.TraceWarning($"Could not import existing content folders {ex.Message}.");
            }

            return folderModels.ToImmutableArray();
        }

        private static string ResolveFolderMergeKey(FolderModel folderModel)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (!string.IsNullOrWhiteSpace(folderModel.ContentPath))
            {
                try
                {
                    return Path.GetFullPath(folderModel.ContentPath)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    Trace.TraceWarning($"Invalid content path '{folderModel.ContentPath}' for folder '{folderModel.Id}'. Falling back to Id merge key.");
                }
            }

            return string.IsNullOrWhiteSpace(folderModel.Id) ? string.Empty : folderModel.Id;
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
