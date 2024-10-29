using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal class WagonModelHandler : ContentHandlerBase<WagonReferenceModel>
    {
        internal const string SourceNameKey = "MstsSourceTrainCar";

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
                ConcurrentDictionary<string, string> wagonFiles = new ConcurrentDictionary<string, string>(
                    new[] { "*.wag", "*.eng" }.SelectMany(extension =>
                        Directory.EnumerateFiles(sourceFolder, extension, new EnumerationOptions()
                        {
                            MaxRecursionDepth = 1,
                            RecurseSubdirectories = true
                        })).
                    ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                //load existing path models, and compare if the corresponding path file folder still exists.
                if (Directory.Exists(wagonsFolder))
                {
                    //FrozenSet<WagonSetModel> existingWagonSets = await GetWagonSets(folderModel, cancellationToken).ConfigureAwait(false);
                    //foreach (WagonSetModel pathModel in existingWagonSets)
                    //{
                    //    if (consistFiles.TryRemove(pathModel?.Tags[SourceNameKey], out string filePath)) //
                    //    {
                    //        results.Add(pathModel);
                    //    }
                    //}
                }

                //    //for any new MSTS consist (remaining in the preloaded dictionary), create a WagonSet model
                //    await Parallel.ForEachAsync(consistFiles, cancellationToken, async (path, token) =>
                //    {
                //        Lazy<Task<WagonSetModel>> modelTask = new Lazy<Task<WagonSetModel>>(Cast(Convert(path.Value, folderModel, cancellationToken)));

                //        WagonSetModel wagonSetModel = await modelTask.Value.ConfigureAwait(false);
                //        string key = wagonSetModel.Hierarchy();
                //        results.Add(wagonSetModel);
                //        taskLazyCache[key] = modelTask;
                //    }).ConfigureAwait(false);
            }
            FrozenSet<WagonReferenceModel> result = results.ToFrozenSet();
            //string key = folderModel.Hierarchy();
            //Lazy<Task<FrozenSet<WagonSetModel>>> modelSetTask;
            //taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<WagonSetModel>>>(Task.FromResult(result));
            //collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static Task<WagonReferenceModel> Convert(WagonReferenceModel wagonModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonModel, nameof(wagonModel));

            return Convert(Path.Combine(wagonModel.Parent.Parent.MstsContentFolder().TrainSetsFolder, wagonModel.Tags[SourceNameKey]), wagonModel.Parent.Parent, cancellationToken);
        }

        private static Task<WagonReferenceModel> Convert(string filePath, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (File.Exists(filePath))
            {
                return Task.FromResult(new WagonReferenceModel());
            }
            else
            {
                Trace.TraceWarning($"Consist file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
