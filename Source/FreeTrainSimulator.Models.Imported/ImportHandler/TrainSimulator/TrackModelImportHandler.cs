using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal class TrackModelImportHandler : ContentHandlerBase<TrackModel>
    {
        public static Task<TrackModel> ExpandTrackSectionModel(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            Task<TrackModel> modelTask = Convert(routeModel, cancellationToken);
            modelTaskCache[routeModel.Id] = modelTask;
            return modelTask;
        }

        private static async Task<TrackModel> Convert(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            RouteModel routeModelExtended = await routeModel.GetExtended(cancellationToken).ConfigureAwait(false);

            TrackDB trackDB = null;
            RoadTrackDB roadTrackDB = null;

            List<Task> loadTasks = new List<Task>
            {
                Task.Run(() =>
                {
                    string tdbFile = routeModel.MstsRouteFolder().TrackDatabaseFile(routeModelExtended.RouteKey);
                    if (!System.IO.File.Exists(tdbFile))
                    {
                        Trace.TraceError($"Track Database File not found in {tdbFile}");
                        return;
                    }
                trackDB = new TrackDatabaseFile(tdbFile).TrackDB;
                }, cancellationToken),
                Task.Run(() =>
                {
                    string rdbFile = routeModel.MstsRouteFolder().RoadTrackDatabaseFile(routeModelExtended.RouteKey);
                    if (!System.IO.File.Exists(rdbFile))
                    {
                        Trace.TraceWarning($"Road Database File not found in {rdbFile}");
                        return;
                    }
                    roadTrackDB = new RoadDatabaseFile(rdbFile).RoadTrackDB;
                }, cancellationToken)
            };

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            TrackModel trackModel = new TrackModel()
            {
                Id = routeModel.Id,
                TrackDatabase = new TrackDatabase()
                {
                    TrackDataBaseType = TrackDataBaseType.Track,
                    TrackNodeConnectors = ConvertTrackNodeConnectors(trackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Track),
                    TrackNodes =ConvertTrackNodes(trackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Track),
                },
                RoadDatabase = roadTrackDB?.TrackNodes != null ? new TrackDatabase()
                {
                    TrackDataBaseType = TrackDataBaseType.Road,
                    TrackNodeConnectors = ConvertTrackNodeConnectors(roadTrackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Road),
                    TrackNodes = ConvertTrackNodes(roadTrackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Road),
                } : null,
            };

            await Create(trackModel, routeModel, cancellationToken).ConfigureAwait(false);
            return trackModel;
        }

        private static ImmutableArray<ImmutableArray<TrackNodeConnector>> ConvertTrackNodeConnectors(TrackNodes trackNodes, RouteModel routeModel, TrackDataBaseType trackDataBaseType)
        {
            ImmutableArray<ImmutableArray<TrackNodeConnector>> result = trackNodes.Select(node => node?.TrackPins.Select((pin, index) => new TrackNodeConnector()
            {
                NodeIndex = node.Index,
                ConnectorType = index < node.InPins ? ConnectorType.InPin : ConnectorType.OutPin,
                Direction = pin.Direction,
                Link = pin.Link,
            }).ToImmutableArray() ?? ImmutableArray<TrackNodeConnector>.Empty).
            ToImmutableArray();

            if (result.Length <= result[^1][0].NodeIndex)
            {
                switch (trackDataBaseType)
                {
                    case TrackDataBaseType.Road:
                        Trace.TraceError($"Non-consecutive tracknode indexes found in road database {routeModel.MstsRouteFolder().RoadTrackDatabaseFile(routeModel.RouteKey)}");
                        break;
                    case TrackDataBaseType.Track:
                        Trace.TraceError($"Non-consecutive tracknode indexes found in track database {routeModel.MstsRouteFolder().TrackDatabaseFile(routeModel.RouteKey)}");
                        break;
                }
            }

            return result;
        }

        private static ImmutableArray<Models.Track.TrackNode> ConvertTrackNodes(TrackNodes trackNodes, RouteModel routeModel, TrackDataBaseType trackDataBaseType)
        {
            ImmutableArray<Models.Track.TrackNode> result = trackNodes.Select(trackNode =>
            {
                return trackNode switch
                {
                    TrackJunctionNode junctionNode => new JunctionNode(junctionNode.UiD.Location, junctionNode.UiD.WorldTile)
                    {
                        NodeIndex = junctionNode.Index,
                        WorldId = junctionNode.UiD.WorldId,
                        ShapeIndex = junctionNode.ShapeIndex,
                    } as Models.Track.TrackNode,
                    TrackEndNode endNode => new EndNode(endNode.UiD.Location, endNode.UiD.WorldTile)
                    {
                        NodeIndex = endNode.Index,
                        WorldId = endNode.UiD.WorldId,
                    } as Models.Track.TrackNode,
                    TrackVectorNode vectorNode => new VectorNode(WorldLocation.None, Tile.Zero)
                    {
                        NodeIndex = vectorNode.Index,
                        VectorSections = vectorNode.TrackVectorSections.Select(tvs =>
                        new VectorSectionNode(tvs.Location, tvs.WorldTile, tvs.Direction)
                        {
                            NodeIndex = tvs.SectionIndex,
                            ShapeIndex = tvs.ShapeIndex,
                            WorldId = (int)tvs.WorldFileUiD,
                        }).ToImmutableArray(),
                    } as Models.Track.TrackNode,
                    _ => null,
                };
            }).ToImmutableArray();


            if (result.Length <= result[^1].NodeIndex)
            {
                switch (trackDataBaseType)
                {
                    case TrackDataBaseType.Road:
                        Trace.TraceError($"Non-consecutive tracknode indexes found in road database {routeModel.MstsRouteFolder().RoadTrackDatabaseFile(routeModel.RouteKey)}");
                        break;
                    case TrackDataBaseType.Track:
                        Trace.TraceError($"Non-consecutive tracknode indexes found in track database {routeModel.MstsRouteFolder().TrackDatabaseFile(routeModel.RouteKey)}");
                        break;
                }
            }
            return result;
        }
    }
}
