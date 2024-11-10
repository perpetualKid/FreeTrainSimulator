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
    internal class WagonReferenceHandler : ContentHandlerBase<WagonReferenceModel>
    {
        internal const string SourceNameKey = "MstsSourceTrainCar";
        private static readonly string[] sourceFileExtensions = new[] { "*.wag", "*.eng" };

        public static WagonReferenceModel Missing = new WagonReferenceModel()
        {
            Id = "<unknown>",
            Name = "Missing",
        };

        public static WagonReferenceModel LocomotiveAny = new WagonReferenceModel()
        {
            Id = "<Any>",
            Name = "- Any Locomotive -",
        };

        public static async Task<FrozenSet<WagonReferenceModel>> ExpandWagonModels(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            string wagonsFolder = ModelFileResolver<WagonReferenceModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<WagonReferenceModel>.WildcardPattern;

            ConcurrentBag<WagonReferenceModel> results = new ConcurrentBag<WagonReferenceModel>();

            string sourceFolder = folderModel.MstsContentFolder().TrainSetsFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                IEnumerable<string> wagonFiles = sourceFileExtensions.SelectMany(extension =>
                        Directory.EnumerateFiles(sourceFolder, extension, new EnumerationOptions()
                        {
                            RecurseSubdirectories = true,
                            MaxRecursionDepth = 1,
                        }));

                await Parallel.ForEachAsync(wagonFiles, cancellationToken, async (wagonFile, token) =>
                    {
                        Lazy<Task<WagonReferenceModel>> modelTask = new Lazy<Task<WagonReferenceModel>>(Cast(Convert(wagonFile, folderModel, cancellationToken)));

                        WagonReferenceModel wagonModel = await modelTask.Value.ConfigureAwait(false);
                        string key = wagonModel.Hierarchy();
                        results.Add(wagonModel);
                        taskLazyCache[key] = modelTask;

                    }).ConfigureAwait(false);
            }
            string key = folderModel.Hierarchy();
            FrozenSet<WagonReferenceModel> result = results.ToFrozenSet();
            taskLazyCollectionCache[key] = new Lazy<Task<FrozenSet<WagonReferenceModel>>>(Task.FromResult(result));
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static Task<WagonReferenceModel> Convert(WagonReferenceModel wagonModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonModel, nameof(wagonModel));

            return Convert(Path.Combine(wagonModel.Parent.MstsContentFolder().TrainSetsFolder, wagonModel.Tags[SourceNameKey]), wagonModel.Parent, cancellationToken);
        }

        private static async Task<WagonReferenceModel> Convert(string filePath, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (File.Exists(filePath))
            {
                WagonReferenceModel wagonReferenceModel = null;

                switch (Path.GetExtension(filePath).ToLowerInvariant())
                {
                    case ".wag":
                        WagonFile wagon = new WagonFile(filePath);

                        wagonReferenceModel = new WagonReferenceModel()
                        {
                            Name = wagon.Name.Trim(),
                            Id = wagon.Name.Trim(),
                            TrainCarType = Common.TrainCarType.Wagon,
                            Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                        };
                        break;
                    case ".eng":
                        EngineFile engine = new EngineFile(filePath);

                        wagonReferenceModel = new WagonReferenceModel()
                        {
                            Name = engine.Name.Trim(),
                            Id = engine.Name.Trim(),
                            TrainCarType = Common.TrainCarType.Engine,
                            Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                        };
                        break;
                }
                //this is the case where a file may have been renamed but not the path id, ie. in case of copy cloning, so adopting the filename as path id
                if (string.IsNullOrEmpty(wagonReferenceModel.Id) || (!string.Equals(wagonReferenceModel.Tags[SourceNameKey].Trim(), wagonReferenceModel.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Trace.TraceWarning($"Wagon or Engine file {filePath} refers to wagon Id {wagonReferenceModel.Id}. Renaming to {wagonReferenceModel.Tags[SourceNameKey]}");
                    wagonReferenceModel = wagonReferenceModel with { Id = wagonReferenceModel.Tags[SourceNameKey] };
                }
                await Create(wagonReferenceModel, folderModel, cancellationToken).ConfigureAwait(false);
                return wagonReferenceModel;
            }
            else
            {
                Trace.TraceWarning($"Wagon or Engine file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
