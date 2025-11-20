using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal class GlobalTrackSectionModelImportHandler : ContentHandlerBase<GlobalTrackSectionModel>
    {
        private const string hierarchyKey = "Global";
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> currentWriters = new ConcurrentDictionary<string, SemaphoreSlim>();

        public static Task<GlobalTrackSectionModel> ExpandTrackSectionModel(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            return Convert(folderModel, cancellationToken);
        }

        private static async Task<GlobalTrackSectionModel> Convert(FolderModel folderModel, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder contentFolder = folderModel.MstsContentFolder();

            int trackSectionVersionInt = TrackSectionsFile.TrackSectionVersion(contentFolder.TrackSectionFile);
            if (trackSectionVersionInt < 0)
                return null;

            string trackSectionVersion = TrackSectionModelExtensions.GlobalTrackSectionId(trackSectionVersionInt);

            if (trackSectionVersion == TrackSectionModelExtensions.TrackSectionIdEmpty)
                trackSectionVersion = folderModel.Id;

            GlobalTrackSectionModel trackSectionModel = (await folderModel.GetTrackSectionModels(cancellationToken).ConfigureAwait(false)).GetById(trackSectionVersion);
            if (trackSectionModel == null)
            {
                if (!currentWriters.TryGetValue(trackSectionVersion, out SemaphoreSlim semaphoreSlim))
                {
                    _ = currentWriters.TryAdd(trackSectionVersion, new SemaphoreSlim(1));
                    semaphoreSlim = currentWriters[trackSectionVersion];
                }

                try
                {
                    await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                    // after acquiring the lock, check if someone else in the meanwhile may have added the file, and return early
                    trackSectionModel = (await folderModel.GetTrackSectionModels(cancellationToken).ConfigureAwait(false)).GetById(trackSectionVersion);
                    if (trackSectionModel != null)
                        return trackSectionModel;

                    TrackSectionsFile trackSectionsFile = new TrackSectionsFile(contentFolder.TrackSectionFile);

                    trackSectionModel = new GlobalTrackSectionModel()
                    {
                        Id = trackSectionVersion,
                        BuildVersion = trackSectionsFile.Version,
                        TrackSections = trackSectionsFile.TrackSections.Select(trackSection => new TrackSection()
                        {
                            SectionIndex = trackSection.Key,
                            Angle = trackSection.Value.Angle,
                            Radius = trackSection.Value.Radius,
                            Curved = trackSection.Value.Curved,
                            Length = trackSection.Value.Length,
                            Gauge = trackSection.Value.Width,
                        }).ToImmutableArray(),
                    };

                    if (trackSectionVersion == folderModel.Id)
                    {
                        Trace.TraceInformation($"Cannot determine version for Global TrackSection in file {contentFolder.TrackSectionFile}. Creating a route-local tsection.dat");
                        await Create(trackSectionModel, folderModel, true, false, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await Create(trackSectionModel, (FolderModel)null, true, false, cancellationToken).ConfigureAwait(false);
                    }

                    modelTaskCache[trackSectionVersion] = Task.FromResult(trackSectionModel);
                    collectionUpdateRequired[hierarchyKey] = true;
                }
                finally
                {
                    _ = semaphoreSlim.Release();
                }
            }

            return trackSectionModel;
        }
    }
}
