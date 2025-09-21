using System.Collections.Immutable;
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
        public static async Task<GlobalTrackSectionModel> ConvertGlobal(FolderModel folderModel, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder contentFolder = folderModel.MstsContentFolder();

            int trackSectionVersion = TrackSectionsFile.TrackSectionVersion(contentFolder.TrackSectionFile);
            
            TrackSectionsFile trackSectionsFile = new TrackSectionsFile(contentFolder.TrackSectionFile);

            GlobalTrackSectionModel trackSectionModel = new GlobalTrackSectionModel()
            {
                Id = TrackSectionModelExtensions.GlobalTrackSectionId(trackSectionVersion),
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

            await Create(trackSectionModel, folderModel, true, false, cancellationToken).ConfigureAwait(false);

            return trackSectionModel;
        }
    }
}
