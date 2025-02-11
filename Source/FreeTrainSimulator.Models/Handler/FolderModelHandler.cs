﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class FolderModelHandler : ContentHandlerBase<FolderModel>
    {
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
