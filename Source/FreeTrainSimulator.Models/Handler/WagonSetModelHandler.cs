using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class WagonSetModelHandler : ContentHandlerBase<WagonSetModel>
    {
        public static WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = ImmutableArray<WagonReferenceModel>.Empty
        };

        public static Task<WagonSetModel> GetCore(WagonSetModel wagonSetModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonSetModel, nameof(wagonSetModel));
            return GetCore(wagonSetModel.Id, wagonSetModel.Parent, cancellationToken);
        }

        public static Task<WagonSetModel> GetCore(string consistId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(consistId);

            if (!modelTaskCache.TryGetValue(key, out Task<WagonSetModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(consistId, folderModel, cancellationToken);
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static Task<ImmutableArray<WagonSetModel>> GetWagonSets(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<WagonSetModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadWagonSets(folderModel, cancellationToken);
            }

            return modelSetTask;
        }

        public static async ValueTask<ImmutableArray<WagonReferenceModel>> GetLocomotives(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<WagonSetModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadWagonSets(folderModel, cancellationToken);
            }

            ImmutableArray<WagonSetModel> wagonSets = await modelSetTask.ConfigureAwait(false);

            return wagonSets.Select(w => w.Locomotive).Where(l => l != null).Append(WagonReferenceHandler.LocomotiveAny).ToImmutableArray();
        }

        private static async Task<ImmutableArray<WagonSetModel>> LoadWagonSets(FolderModel folderModel, CancellationToken cancellationToken)
        {
            string wagonsFolder = ModelFileResolver<WagonSetModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<WagonSetModel>.WildcardSavePattern;

            ConcurrentBag<WagonSetModel> results = new ConcurrentBag<WagonSetModel>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(wagonsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(wagonsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string consistId = Path.GetFileNameWithoutExtension(file);

                    if (consistId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        consistId = consistId[..^fileExtension.Length];

                    WagonSetModel wagonSet = await GetCore(consistId, folderModel, token).ConfigureAwait(false);
                    if (null != wagonSet)
                        results.Add(wagonSet);
                }).ConfigureAwait(false);
            }
            return results.ToImmutableArray();
        }
    }
}
