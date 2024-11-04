using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class WagonSetModelHandler : ContentHandlerBase<WagonSetModel>
    {
        internal const string SourceNameKey = "MstsSourceConsist";

        public static ValueTask<WagonSetModel> GetCore(WagonSetModel wagonSetModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonSetModel, nameof(wagonSetModel));
            return GetCore(wagonSetModel.Id, wagonSetModel.Parent, cancellationToken);
        }

        public static async ValueTask<WagonSetModel> GetCore(string consistId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(consistId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<WagonSetModel>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<WagonSetModel>>(FromFile(consistId, folderModel, cancellationToken));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            WagonSetModel wagonSetModel = await modelTask.Value.ConfigureAwait(false);

            if (wagonSetModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<WagonSetModel>>(() => Cast(Convert(wagonSetModel, cancellationToken)));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return wagonSetModel;
        }

        public static async ValueTask<FrozenSet<WagonSetModel>> GetWagonSets(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<WagonSetModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<WagonSetModel>>>(() => LoadWagonSets(folderModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<WagonSetModel>> ExpandWagonSetModels(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            string wagonsFolder = ModelFileResolver<WagonSetModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<WagonSetModel>.WildcardPattern;

            ConcurrentBag<WagonSetModel> results = new ConcurrentBag<WagonSetModel>();

            string sourceFolder = folderModel.MstsContentFolder().ConsistsFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentDictionary<string, string> consistFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(sourceFolder, "*.con").
                    ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                await Parallel.ForEachAsync(consistFiles, cancellationToken, async (path, token) =>
                {
                    Lazy<Task<WagonSetModel>> modelTask = new Lazy<Task<WagonSetModel>>(Cast(Convert(path.Value, folderModel, cancellationToken)));

                    WagonSetModel wagonSetModel = await modelTask.Value.ConfigureAwait(false);
                    string key = wagonSetModel.Hierarchy();
                    results.Add(wagonSetModel);
                    taskLazyCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<WagonSetModel> result = results.ToFrozenSet();
            string key = folderModel.Hierarchy();
            Lazy<Task<FrozenSet<WagonSetModel>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<WagonSetModel>>>(Task.FromResult(result));
            collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<FrozenSet<WagonSetModel>> LoadWagonSets(FolderModel folderModel, CancellationToken cancellationToken)
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

                    WagonSetModel path = await GetCore(consistId, folderModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        private static Task<WagonSetModel> Convert(WagonSetModel wagonSetModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonSetModel, nameof(wagonSetModel));

            return Convert(wagonSetModel.Parent.MstsContentFolder().ConsistFile(wagonSetModel.Tags[SourceNameKey]), wagonSetModel.Parent, cancellationToken);
        }

        private static async Task<WagonSetModel> Convert(string filePath, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (File.Exists(filePath))
            {
                ConsistFile consistFile = new ConsistFile(filePath);

                WagonSetModel wagonSetModel = new WagonSetModel()
                {
                    Id = consistFile.Train.Id.Trim(),
                    Name = consistFile.Train.Name.Trim(),
                    MaximumSpeed = consistFile.Train.MaxVelocity.A,
                    AccelerationFactor = consistFile.Train.MaxVelocity.B,
                    Durability = consistFile.Train.Durability,
                    Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                    TrainCars = consistFile.Train.Wagons.OrderBy(w => w.UiD).Select((w, index) => new WagonReferenceModel()
                    {
                        TrainCarType = w.IsEOT ? Common.TrainCarType.Eot : w.IsEngine ? Common.TrainCarType.Engine : Common.TrainCarType.Wagon,
                        Uid = index,//w.UiD,
                        Reverse = w.Flip,
                        Name = w.Name,
                        Reference = w.Folder,
                    }).ToFrozenSet()
                };
                //this is the case where a file may have been renamed but not the consist id, ie. in case of copy cloning, so adopting the filename as id
                if (string.IsNullOrEmpty(wagonSetModel.Id) || (!string.Equals(wagonSetModel.Tags[SourceNameKey].Trim(), wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Trace.TraceWarning($"Consist file {filePath} refers to consist Id {wagonSetModel.Id}. Renaming to {wagonSetModel.Tags[SourceNameKey]}");
                    wagonSetModel = wagonSetModel with { Id = wagonSetModel.Tags[SourceNameKey] };
                }
                await Create(wagonSetModel, folderModel, cancellationToken).ConfigureAwait(false);
                return wagonSetModel;
            }
            else
            {
                Trace.TraceWarning($"Consist file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
