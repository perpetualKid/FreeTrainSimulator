using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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

            Task<TrackSectionModel> modelTask = Convert(routeModel, cancellationToken);
            modelTaskCache[routeModel.Id] = modelTask;
            return modelTask;
        }

        private static async Task<TrackSectionModel> Convert(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            string trackSectionsFilePath = routeModel.MstsRouteFolder().TrackSectionFile;
            TrackSectionsFile trackSectionsFile = new TrackSectionsFile(trackSectionsFilePath);
            trackSectionsFile.AddRouteTSectionDatFile(routeModel.MstsRouteFolder().RouteTrackSectionFile);

            TrackSectionModel trackSectionModel = new TrackSectionModel()
            {
                Id = routeModel.Id,
                TrackSections = trackSectionsFile.TrackSections?.Select(trackSection => new TrackSection()
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
                    trackSectionsFile.TrackSectionIndex?.ToDictionary(dynamicTrackSection => dynamicTrackSection.Key, dynamicTrackSection => ImmutableArray.Create(new TrackSectionIndex()
                    {
                        TrackSections = dynamicTrackSection.Value.TrackSections.ToImmutableArray(),
                    })) ??
                        ImmutableDictionary<int, ImmutableArray<TrackSectionIndex>>.Empty.ToDictionary()
                        ).ToImmutableDictionary(),
            };

            IEnumerable<int> items = trackSectionsFile.TrackSections?.Values.Where(t => t.Length == 0 && t.Angle == 0).Select(t => t.SectionIndex);
            if (items?.Any() ?? false)
                Trace.TraceWarning($"Added track sections from {trackSectionsFilePath} with Length and Angle being 0 [{string.Join(", ", items)}]");
            items = trackSectionsFile.TrackSectionIndex?.Where(t => t.Value.TrackSections?.Length == 0).Select(t => t.Key);
            if (items?.Any() ?? false)
                Trace.TraceWarning($"Added track paths from {trackSectionsFilePath} with no elements [{string.Join(", ", items)}]");

            await Create(trackSectionModel, routeModel, cancellationToken).ConfigureAwait(false);

            return trackSectionModel;
        }
    }
}
