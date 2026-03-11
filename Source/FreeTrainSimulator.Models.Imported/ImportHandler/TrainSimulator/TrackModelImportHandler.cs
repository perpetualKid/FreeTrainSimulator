using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
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
                        Trace.TraceInformation($"Road Database File not found in {rdbFile}");
                        return;
                    }
                    roadTrackDB = new RoadDatabaseFile(rdbFile).RoadTrackDB;
                }, cancellationToken)
            };

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            ImmutableArray<Track.TrackNode> trackNodes = ConvertTrackNodes(trackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Track);
            ImmutableArray<Track.TrackNode> roadNodes = ConvertTrackNodes(roadTrackDB?.TrackNodes, routeModelExtended, TrackDataBaseType.Road);
            ImmutableDictionary<int, TrackItemIndex> trackItemSelectors = ConvertTrackSelectors(trackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Track);
            ImmutableDictionary<int, TrackItemIndex> roadItemSelectors = ConvertTrackSelectors(roadTrackDB?.TrackNodes, routeModelExtended, TrackDataBaseType.Road);

            TrackModel trackModel = new TrackModel()
            {
                Id = routeModel.Id,
                TrackDatabase = new TrackDatabase()
                {
                    TrackDataBaseType = TrackDataBaseType.Track,
                    TrackNodeConnectors = ConvertTrackNodeConnectors(trackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Track),
                    TrackNodes = trackNodes,
                    TrackItemsSelectors = trackItemSelectors,
                    TrackItems = ConvertTrackItems(trackDB.TrackItems, trackNodes, trackItemSelectors, routeModelExtended),
                },
                RoadDatabase = roadTrackDB?.TrackNodes != null ? new TrackDatabase()
                {
                    TrackDataBaseType = TrackDataBaseType.Road,
                    TrackNodeConnectors = ConvertTrackNodeConnectors(roadTrackDB.TrackNodes, routeModelExtended, TrackDataBaseType.Road),
                    TrackNodes = roadNodes,
                    TrackItemsSelectors = roadItemSelectors,
                    TrackItems = ConvertTrackItems(roadTrackDB.TrackItems, roadNodes, roadItemSelectors, routeModelExtended),
                } : null,
            };

            await Create(trackModel, routeModel, cancellationToken).ConfigureAwait(false);
            return trackModel;
        }

        private static ImmutableArray<ImmutableArray<TrackNodeConnector>> ConvertTrackNodeConnectors(TrackNodes trackNodes, RouteModel routeModel, TrackDataBaseType trackDataBaseType)
        {
            if (trackNodes == null)
                return ImmutableArray<ImmutableArray<TrackNodeConnector>>.Empty;

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

        private static ImmutableArray<Track.TrackNode> ConvertTrackNodes(TrackNodes trackNodes, RouteModel routeModel, TrackDataBaseType trackDataBaseType)
        {
            if (trackNodes == null)
                return ImmutableArray<Track.TrackNode>.Empty;

            ImmutableArray<Track.TrackNode> result = trackNodes.Select(trackNode =>
            {
                return trackNode switch
                {
                    TrackJunctionNode junctionNode => new JunctionNode(junctionNode.UiD.Location, junctionNode.UiD.WorldTile)
                    {
                        NodeIndex = junctionNode.Index,
                        WorldId = junctionNode.UiD.WorldId,
                        ShapeIndex = junctionNode.ShapeIndex,
                    } as Track.TrackNode,
                    TrackEndNode endNode => new EndNode(endNode.UiD.Location, endNode.UiD.WorldTile)
                    {
                        NodeIndex = endNode.Index,
                        WorldId = endNode.UiD.WorldId,
                    } as Track.TrackNode,
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
                    } as Track.TrackNode,
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

        private static ImmutableDictionary<int, TrackItemIndex> ConvertTrackSelectors(TrackNodes trackNodes, RouteModel routeModel, TrackDataBaseType trackDataBaseType)
        {
            return trackNodes == null
                ? ImmutableDictionary<int, TrackItemIndex>.Empty
                : trackNodes.OfType<TrackVectorNode>().Select(tvn => (tvn.Index, new TrackItemIndex()
            {
                TrackItems = tvn.TrackItemIndices.ToImmutableArray(),
            })).ToImmutableDictionary(item => item.Index, item => item.Item2);
        }

        private static ImmutableArray<TrackItemModel> ConvertTrackItems(List<TrackItem> trackItems, ImmutableArray<Track.TrackNode> trackNodes, ImmutableDictionary<int, TrackItemIndex> trackItemSelectors,
            RouteModel routeModel)
        {
            if (trackItems == null)
                return ImmutableArray<TrackItemModel>.Empty;

            int[] trackNodeReferences = new int[trackItems.Count];
            //temporary map reverse-linking TrackItems to TrackNodes
            foreach (KeyValuePair<int, TrackItemIndex> item in trackItemSelectors)
            {
                foreach (int itemIndex in item.Value.TrackItems)
                {
                    trackNodeReferences[itemIndex] = trackNodes[item.Key].NodeIndex;
                }
            }

            List<TrackItemModel> result = new List<TrackItemModel>();

            uint flags;

            foreach (TrackItem trackItem in trackItems)
            {
                switch (trackItem)
                {
                    case SidingItem sidingItem:
                        result.Add(new SidingTrackItem(sidingItem.Location)
                        {
                            NodeIndex = trackNodeReferences[sidingItem.TrackItemId],
                            SectionDistance = sidingItem.SData1,
                            Flags = uint.TryParse(sidingItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = sidingItem.TrackItemId,
                            LinkedSidingItem = sidingItem.LinkedSidingId,
                            SidingName = sidingItem.ItemName,
                            SidingFlags = uint.TryParse(sidingItem.Flags1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                        });
                        break;
                    case PlatformItem platformItem:
                        result.Add(new PlatformTrackItem(platformItem.Location)
                        {
                            NodeIndex = trackNodeReferences[platformItem.TrackItemId],
                            SectionDistance = platformItem.SData1,
                            Flags = uint.TryParse(platformItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = platformItem.TrackItemId,
                            LinkedPlatformItem = platformItem.LinkedPlatformItemId,
                            PlatformName = platformItem.ItemName,
                            StationName = platformItem.Station,
                            PlatformFlags = uint.TryParse(platformItem.Flags1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            MinWaitingTime = platformItem.PlatformMinWaitingTime,
                            PassengersWaiting = platformItem.PlatformNumPassengersWaiting,
                        });
                        break;
                    case SpeedPostItem speedPostItem:
                        result.Add(speedPostItem.IsMilePost ? new MilepostTrackItem(speedPostItem.Location)
                        {
                            NodeIndex = trackNodeReferences[speedPostItem.TrackItemId],
                            SectionDistance = speedPostItem.SData1,
                            Flags = uint.TryParse(speedPostItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = speedPostItem.TrackItemId,
                            DistanceValue = speedPostItem.Distance,
                        } : new SpeedpostTrackItem(speedPostItem.Location)
                        {
                            NodeIndex = trackNodeReferences[speedPostItem.TrackItemId],
                            SectionDistance = speedPostItem.SData1,
                            Flags = uint.TryParse(speedPostItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = speedPostItem.TrackItemId,
                            SpeedValue = speedPostItem.Distance,
                            AlternativeSpeedValue = speedPostItem.NumberShown,
                            SpeedpostType = GetSpeedpostType(speedPostItem),
                            Angle = speedPostItem.Angle,
                        });
                        break;
                    case HazardItem hazardItem:
                        result.Add(new HazardTrackItem(hazardItem.Location)
                        {
                            NodeIndex = trackNodeReferences[hazardItem.TrackItemId],
                            SectionDistance = hazardItem.SData1,
                            Flags = uint.TryParse(hazardItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = hazardItem.TrackItemId,
                        });
                        break;
                    case PickupItem pickupItem:
                        result.Add(new PickupTrackItem(pickupItem.Location)
                        {
                            NodeIndex = trackNodeReferences[pickupItem.TrackItemId],
                            SectionDistance = pickupItem.SData1,
                            Flags = uint.TryParse(pickupItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = pickupItem.TrackItemId,
                        });
                        break;
                    case LevelCrossingItem levelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(levelCrossingItem.Location)
                        {
                            NodeIndex = trackNodeReferences[levelCrossingItem.TrackItemId],
                            SectionDistance = levelCrossingItem.SData1,
                            Flags = uint.TryParse(levelCrossingItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = levelCrossingItem.TrackItemId,
                        });
                        break;
                    case RoadLevelCrossingItem roadLevelCrossingItem: // road level crossings are not really useful and no route seems to contain them, but we'll just treat them as LevelCrossings
                        result.Add(new RoadLevelCrossingTrackItem(roadLevelCrossingItem.Location)
                        {
                            NodeIndex = trackNodeReferences[roadLevelCrossingItem.TrackItemId],
                            SectionDistance = roadLevelCrossingItem.SData1,
                            Flags = uint.TryParse(roadLevelCrossingItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = roadLevelCrossingItem.TrackItemId,
                        });
                        break;
                    case SoundRegionItem soundRegionItem:
                        result.Add(new SoundRegionTrackItem(soundRegionItem.Location)
                        {
                            NodeIndex = trackNodeReferences[soundRegionItem.TrackItemId],
                            SectionDistance = soundRegionItem.SData1,
                            Flags = uint.TryParse(soundRegionItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = soundRegionItem.TrackItemId,
                            SoundRegionData1 = soundRegionItem.SoundRegionData1,
                            SoundRegionData2 = soundRegionItem.SoundRegionData2,
                            SoundRegionData3 = soundRegionItem.SoundRegionData3,
                        });
                        break;
                    case SignalItem signalItem:
                        result.Add(new SignalTrackItem(signalItem.Location)
                        {
                            NodeIndex = trackNodeReferences[signalItem.TrackItemId],
                            SectionDistance = signalItem.SData1,
                            Flags = uint.TryParse(signalItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = signalItem.TrackItemId,
                            SignalData = signalItem.SignalData,
                            SignalFlags = uint.TryParse(signalItem.Flags1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            SignalType = signalItem.SignalType,
                            Direction = signalItem.Direction,
                            SignalDirection = signalItem.SignalDirections?.Count > 0 ? 
                                new SignalDirection() 
                                { 
                                    JunctionPath = signalItem.SignalDirections[0].LinkLRPath,
                                } : null,
                        });
                        break;
                    case CrossoverItem crossOverItem:
                        result.Add(new CrossoverTrackItem(crossOverItem.Location)
                        {
                            NodeIndex = trackNodeReferences[crossOverItem.TrackItemId],
                            SectionDistance = crossOverItem.SData1,
                            Flags = uint.TryParse(crossOverItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = crossOverItem.TrackItemId,
                            ShapeIndex = crossOverItem.ShapeId,
                        });
                        break;
                    case RoadCarSpawnerItem carSpawner:
                        result.Add(new CarSpawnerTrackItem(carSpawner.Location)
                        {
                            NodeIndex = trackNodeReferences[carSpawner.TrackItemId],
                            SectionDistance = carSpawner.SData1,
                            Flags = uint.TryParse(carSpawner.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = carSpawner.TrackItemId,
                        });
                        break;
                    case EmptyItem emptyItem:
                        result.Add(new EmptyTrackItem(emptyItem.Location)
                        {
                            NodeIndex = trackNodeReferences[emptyItem.TrackItemId],
                            SectionDistance = emptyItem.SData1,
                            Flags = uint.TryParse(emptyItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = emptyItem.TrackItemId,
                        });
                        break;
                    default:
                        Trace.TraceWarning($"{trackItem.GetType().Name} Index #{trackItem.TrackItemId} not supported for Track Items in track database {routeModel.MstsRouteFolder().TrackDatabaseFile(routeModel.RouteKey)}");
                        break;
                }
            }
            return result.ToImmutableArray();
        }

        private static SpeedpostType GetSpeedpostType(SpeedPostItem speedPostItem)
        {
            SpeedpostType result = SpeedpostType.None;
            if (speedPostItem.IsWarning)
                result |= SpeedpostType.Warning;
            if (speedPostItem.IsLimit)
                result |= SpeedpostType.Limit;
            if (speedPostItem.IsResume)
                result |= SpeedpostType.Resume;
            if (speedPostItem.IsPassenger)
                result |= SpeedpostType.Passenger;
            if (speedPostItem.IsFreight)
                result |= SpeedpostType.Freight;
            if (!speedPostItem.IsMPH)
                result |= SpeedpostType.Metric;
            if (speedPostItem.ShowNumber)
                result |= SpeedpostType.ShowNumber;
            if (speedPostItem.ShowDot)
                result |= SpeedpostType.ShowDot;
            return result;
        }
    }
}
