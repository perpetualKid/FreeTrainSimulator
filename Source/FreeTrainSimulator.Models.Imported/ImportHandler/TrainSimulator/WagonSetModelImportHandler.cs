using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed class WagonSetModelImportHandler : ContentHandlerBase<WagonSetModel>
    {
        internal const string SourceNameKey = "MstsSourceConsist";

        public static WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = FrozenSet<WagonReferenceModel>.Empty
        };

        public static async Task<FrozenSet<WagonSetModel>> ExpandWagonSetModels(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            ConcurrentBag<WagonSetModel> results = new ConcurrentBag<WagonSetModel>();

            string sourceFolder = folderModel.MstsContentFolder().ConsistsFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentBag<string> consistFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, "*.con"));

                await Parallel.ForEachAsync(consistFiles, cancellationToken, async (consistFile, token) =>
                {
                    Task<WagonSetModel> modelTask = Convert(consistFile, folderModel, cancellationToken);

                    WagonSetModel wagonSetModel = await modelTask.ConfigureAwait(false);
                    string key = wagonSetModel.Hierarchy();
                    results.Add(wagonSetModel);
                    modelTaskCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<WagonSetModel> result = results.ToFrozenSet();
            string key = folderModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            _ = collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<WagonSetModel> Convert(string filePath, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (File.Exists(filePath))
            {
                ConsistFile consistFile = new ConsistFile(filePath);

                List<WagonReferenceModel> trainCars = consistFile.Train.Wagons.OrderBy(w => w.UiD).Select((w, index) => new WagonReferenceModel()
                {
                    TrainCarType = w.IsEOT ? Common.TrainCarType.Eot : w.IsEngine ? Common.TrainCarType.Engine : Common.TrainCarType.Wagon,
                    Uid = index,//w.UiD,
                    Reverse = w.Flip,
                    Name = w.Name,
                    Reference = w.Folder,
                }).ToList();

                WagonReferenceModel locomotive = null;
                for (int i = 0; i < trainCars.Count; i++)
                {
                    if (trainCars[i].TrainCarType == Common.TrainCarType.Engine)
                    {
                        locomotive = trainCars[i];
                        string engineFileName = folderModel.MstsContentFolder().EngineFile(locomotive.Reference, locomotive.Name);

                        if (File.Exists(engineFileName))
                        {
                            EngineFile engFile = new EngineFile(engineFileName);
                            locomotive = !string.IsNullOrEmpty(engFile.CabViewFile)
                                ? (locomotive with
                                {
                                    Name = engFile.Name?.Trim(),
                                    Description = engFile.Description?.Trim(),
                                })
                                : locomotive;
                            trainCars[i] = locomotive;
                        }
                    }
                }

                WagonSetModel wagonSetModel = new WagonSetModel()
                {
                    Id = consistFile.Train.Id.Trim(),
                    Name = consistFile.Train.Name.Trim(),
                    MaximumSpeed = consistFile.Train.MaxVelocity.A,
                    AccelerationFactor = consistFile.Train.MaxVelocity.B,
                    Durability = consistFile.Train.Durability,
                    Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                    TrainCars = trainCars.ToFrozenSet(),
                };
                //this is the case where a file may have been renamed but not the consist id, ie. in case of copy cloning, so adopting the filename as id
                if (string.IsNullOrEmpty(wagonSetModel.Id) || !string.Equals(wagonSetModel.Tags[SourceNameKey].Trim(), wagonSetModel.Id, StringComparison.OrdinalIgnoreCase))
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
