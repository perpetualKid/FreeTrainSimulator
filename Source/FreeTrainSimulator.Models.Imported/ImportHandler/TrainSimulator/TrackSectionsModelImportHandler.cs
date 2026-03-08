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
    internal class TrackSectionsModelImportHandler : ContentHandlerBase<TrackSectionsModel>
    {
        public static Task<TrackSectionsModel> ExpandTrackSectionModel(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            Task<TrackSectionsModel> modelTask = Convert(routeModel, cancellationToken);
            modelTaskCache[routeModel.Id] = modelTask;
            return modelTask;
        }

        private static async Task<TrackSectionsModel> Convert(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            TrackSectionsFile trackSectionsFile = new TrackSectionsFile(routeModel.MstsRouteFolder().TrackSectionFile);
            trackSectionsFile.AddRouteTSectionDatFile(routeModel.MstsRouteFolder().RouteTrackSectionFile);

            TrackSectionsModel trackSectionModel = new TrackSectionsModel()
            {
                Id = routeModel.Id,
                TrackSections = trackSectionsFile.TrackSections?.Where(t => t.Value.Length > 0 || t.Value.Angle > 0).Select(trackSection => new TrackSection()
                {
                    SectionIndex = trackSection.Key,
                    Angle = trackSection.Value.Angle,
                    Radius = trackSection.Value.Radius,
                    Curved = trackSection.Value.Curved,
                    Length = trackSection.Value.Length,
                    Gauge = trackSection.Value.Width,
                }).ToImmutableDictionary((trackSection) => trackSection.SectionIndex),
                TrackShapes = trackSectionsFile.TrackShapes?.Where(t => !string.IsNullOrEmpty(t.Value.FileName)).Select(trackShape => new TrackShape()
                {
                    ShapeIndex = trackShape.Key,
                    FileName = trackShape.Value.FileName,
                    ClearanceDistance = (float)trackShape.Value.ClearanceDistance,
                    MainRoute = trackShape.Value.MainRoute,
                    ShapeType = trackShape.Value.TunnelShape ? ShapeType.Tunnel : trackShape.Value.RoadShape ? ShapeType.Road : ShapeType.None,
                }).ToImmutableDictionary((trackShape) => trackShape.ShapeIndex),
                TrackSectionIndices = trackSectionsFile.TrackShapes?.Where(t => !string.IsNullOrEmpty(t.Value.FileName)).
                        ToDictionary(trackShape => trackShape.Key, trackShape => trackShape.Value.SectionIndices.Select(sectionIndex => new TrackSectionIndex()
                        {
                            TrackSections = sectionIndex.TrackSections.ToImmutableArray(),
                            ShapeOffset = new TrackShapeOffset(sectionIndex.Offset, sectionIndex.AngularOffset)
                        }).ToImmutableArray()).
                Concat(
                    trackSectionsFile.TrackSectionIndex?.Where(t => t.Value.TrackSections?.Length > 0).
                        ToDictionary(dynamicTrackSection => dynamicTrackSection.Key, dynamicTrackSection => ImmutableArray.Create(new TrackSectionIndex()
                        {
                            TrackSections = dynamicTrackSection.Value.TrackSections.ToImmutableArray(),
                        })) ??
                        ImmutableDictionary<int, ImmutableArray<TrackSectionIndex>>.Empty.ToDictionary()
                        ).ToImmutableDictionary(),
            };

            await Create(trackSectionModel, routeModel, cancellationToken).ConfigureAwait(false);

            return trackSectionModel;
        }
    }
}
