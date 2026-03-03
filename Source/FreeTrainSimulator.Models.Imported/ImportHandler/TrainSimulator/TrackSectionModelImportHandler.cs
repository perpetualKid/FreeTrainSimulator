using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Track;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal class TrackSectionModelImportHandler : ContentHandlerBase<TrackSectionModel>
    {
        public static Task<TrackSectionModel> ExpandTrackSectionModel(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            return Convert(routeModel, cancellationToken);
        }

        private static async Task<TrackSectionModel> Convert(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            TrackSectionsFile trackSectionsFile = new TrackSectionsFile(routeModel.MstsRouteFolder().TrackSectionFile);
            trackSectionsFile.AddRouteTSectionDatFile(routeModel.MstsRouteFolder().RouteTrackSectionFile);

            TrackSectionModel trackSectionModel = new TrackSectionModel()
            {
                Id = routeModel.Id,
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

            await Create(trackSectionModel, routeModel, true, false, cancellationToken).ConfigureAwait(false);

            return trackSectionModel;
        }
    }
}
