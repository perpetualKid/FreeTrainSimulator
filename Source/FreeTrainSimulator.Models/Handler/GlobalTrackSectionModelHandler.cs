using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Handler
{
    internal class GlobalTrackSectionModelHandler : ContentHandlerBase<GlobalTrackSectionModel>
    {
        private const string hierarchyKey = "Global";
        private static int currentVersion;

        public static async Task<bool> Contains(string versionId, CancellationToken cancellationToken)
        {
            return (await GetTrackSectionModels(cancellationToken).ConfigureAwait(false)).GetById(versionId) != null;
        }

        public static Task<GlobalTrackSectionModel> GetGlobal(CancellationToken cancellationToken)
        {
            if (currentVersion == 0 || collectionUpdateRequired.TryRemove(hierarchyKey, out _) || 
                !modelSetTaskCache.TryGetValue(hierarchyKey, out Task<ImmutableArray<GlobalTrackSectionModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[hierarchyKey] = LoadTrackSectionModels(true, cancellationToken);
            }
            return GetCore(currentVersion, cancellationToken);
        }

        public static Task<GlobalTrackSectionModel> GetCore(int version, CancellationToken cancellationToken)
        {

            string key = TrackSectionModelExtensions.GlobalTrackSectionId(version);

            if (!modelTaskCache.TryGetValue(key, out Task<GlobalTrackSectionModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile<GlobalTrackSectionModel>(TrackSectionModelExtensions.GlobalTrackSectionId(version), null, cancellationToken);
            }

            return modelTask;
        }

        public static Task<ImmutableArray<GlobalTrackSectionModel>> GetTrackSectionModels(CancellationToken cancellationToken)
        {
            if (collectionUpdateRequired.TryRemove(hierarchyKey, out _) || !modelSetTaskCache.TryGetValue(hierarchyKey, out Task<ImmutableArray<GlobalTrackSectionModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[hierarchyKey] = modelSetTask = LoadTrackSectionModels(false, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<GlobalTrackSectionModel>> LoadTrackSectionModels(bool currentOnly, CancellationToken cancellationToken)
        {
            string trackSectionsFolder = ModelFileResolver<GlobalTrackSectionModel>.FolderPath(null);
            string pattern = ModelFileResolver<GlobalTrackSectionModel>.WildcardSavePattern;

            ConcurrentBag<GlobalTrackSectionModel> results = new ConcurrentBag<GlobalTrackSectionModel>();

            //check for existing Global tracksections, and load the highest available, or all versions
            if (Directory.Exists(trackSectionsFolder))
            {
                int version = 0;
                if (currentOnly)
                {
                    currentVersion = Directory.EnumerateFiles(trackSectionsFolder, pattern).
                        Where(f => int.TryParse(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f)), out version)).Select(f => version).
                        OrderDescending().FirstOrDefault();

                    if (currentVersion > 0)
                        results.Add(await GetCore(version, cancellationToken).ConfigureAwait(false));
                }
                else
                {
                    await Parallel.ForEachAsync(Directory.EnumerateFiles(trackSectionsFolder, pattern), cancellationToken, async (file, token) =>
                    {
                        if (int.TryParse(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file)), out int version))
                        {
                            GlobalTrackSectionModel trackSectionModel = await GetCore(version, cancellationToken).ConfigureAwait(false);
                            if (null != trackSectionModel)
                            {
                                results.Add(trackSectionModel);
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }

            ImmutableArray<GlobalTrackSectionModel> result = results.OrderByDescending(g => g.BuildVersion).ToImmutableArray();
            currentVersion = result.Length > 0 ? result[0].BuildVersion : 0;
            return result;
        }
    }
}
