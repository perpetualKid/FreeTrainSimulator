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

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal class TrackModelImportHandler : ContentHandlerBase<TrackModel>
    {
        public static Task<TrackModel> ExpandTrackModel(RouteModelHeader routeModel, CancellationToken cancellationToken)
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
            string tdbFile = string.Empty;
            string rdbFile = string.Empty;

            List<Task> loadTasks = new List<Task>
            {
                Task.Run(() =>
                {
                    tdbFile = routeModel.MstsRouteFolder().TrackDatabaseFile(routeModelExtended.RouteKey);
                    if (!System.IO.File.Exists(tdbFile))
                    {
                        Trace.TraceError($"Track Database File not found in {tdbFile}");
                        return;
                    }
                trackDB = new TrackDatabaseFile(tdbFile).TrackDB;
                }, cancellationToken),
                Task.Run(() =>
                {
                    rdbFile = routeModel.MstsRouteFolder().RoadTrackDatabaseFile(routeModelExtended.RouteKey);
                    if (!System.IO.File.Exists(rdbFile))
                    {
                        Trace.TraceInformation($"Road Database File not found in {rdbFile}");
                        return;
                    }
                    roadTrackDB = new RoadDatabaseFile(rdbFile).RoadTrackDB;
                }, cancellationToken)
            };

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            TrackSectionModel trackSections = await routeModel.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);

            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackDataBaseType = TrackDataBaseType.Rail,
                TrackNodeConnectors = ConvertTrackNodeConnectors(trackDB.TrackNodes, tdbFile),
                TrackNodes = ConvertTrackNodes(trackDB.TrackNodes, trackSections, tdbFile),
                TrackItemSelectors = ConvertTrackSelectors(trackDB.TrackNodes),
            };
            TrackDatabase roadDatabase = roadTrackDB?.TrackNodes == null ? null : new TrackDatabase()
            {
                TrackDataBaseType = TrackDataBaseType.Road,
                TrackNodeConnectors = ConvertTrackNodeConnectors(roadTrackDB.TrackNodes, rdbFile),
                TrackNodes = ConvertTrackNodes(roadTrackDB?.TrackNodes, trackSections, rdbFile),
                TrackItemSelectors = ConvertTrackSelectors(roadTrackDB?.TrackNodes),
            };

            TrackModel trackModel = new TrackModel()
            {
                Id = routeModel.Id,
                TrackDatabase = trackDatabase with
                {
                    TrackItems = ConvertTrackItems(trackDB.TrackItems, trackDatabase, trackSections, tdbFile),
                },
                RoadDatabase = roadDatabase == null ? null : roadDatabase with
                {
                    TrackItems = ConvertTrackItems(roadTrackDB.TrackItems, roadDatabase, trackSections, rdbFile),
                },
            };

            await Create(trackModel, routeModel, cancellationToken).ConfigureAwait(false);
            return trackModel;
        }

        private static ImmutableArray<TrackNodeConnectorIndex> ConvertTrackNodeConnectors(TrackNodes trackNodes, string trackdatabaseFile)
        {
            if (trackNodes == null)
                return ImmutableArray<TrackNodeConnectorIndex>.Empty;

            ImmutableArray<TrackNodeConnectorIndex> result = trackNodes.Select(node => node == null ? new TrackNodeConnectorIndex() : new TrackNodeConnectorIndex()
            {
                InboundCount = node.InPins,
                NodeIndex = node.Index,
                TrackNodeConnectors = node?.TrackPins.Select((pin, index) => new TrackNodeConnector()
                {
                    ConnectorType = index < node.InPins ? ConnectorType.InPin : ConnectorType.OutPin,
                    Direction = pin.Direction,
                    Link = pin.Link,
                }).ToImmutableArray() ?? ImmutableArray<TrackNodeConnector>.Empty,
            }).ToImmutableArray();

            if (result.Length <= result[^1].NodeIndex)
            {
                Trace.TraceError($"Non-consecutive tracknode indexes found in track database {trackdatabaseFile}");
            }
            return result;
        }

        private static ImmutableArray<TrackNodeBase> ConvertTrackNodes(TrackNodes trackNodes, TrackSectionModel trackSections, string trackdatabaseFile)
        {
            if (trackNodes == null)
                return ImmutableArray<TrackNodeBase>.Empty;

            ImmutableArray<TrackNodeBase> result = trackNodes.Select(trackNode =>
            {
                return trackNode switch
                {
                    TrackJunctionNode junctionNode => ConvertJunctionNode(junctionNode, trackSections, trackdatabaseFile),
                    TrackEndNode endNode => new EndNode(endNode.UiD.Location, endNode.UiD.WorldTile, endNode.UiD.Direction)
                    {
                        NodeIndex = endNode.Index,
                        WorldId = endNode.UiD.WorldId,
                    } as TrackNodeBase,
                    TrackVectorNode vectorNode => ConvertVectorNode(vectorNode, trackNodes, trackSections, trackdatabaseFile),
                    _ => null,
                };
            }).ToImmutableArray();

            foreach (TrackNodeBase trackNode in result)
            {
                switch (trackNode)
                {
                    case JunctionNode junctionNode:
                        break;
                    case EndNode endNode:
                        break;
                    case VectorNode vectorNode:
                        if (vectorNode.VectorSections.Length == 0)
                        {
                            Trace.TraceWarning($"Vector Node #{vectorNode.NodeIndex} in track database {trackdatabaseFile} has no vector sections.");
                        }
                        else
                        {
                            for (int i = 0; i < vectorNode.VectorSections.Length; i++)
                            {
                                if (!trackSections.TrackSections.ContainsKey(vectorNode.VectorSections[i].NodeIndex))
                                    Trace.TraceWarning($"Vector Node #{vectorNode.NodeIndex} Section {i} SectionIndex {vectorNode.VectorSections[i].NodeIndex} in track database {trackdatabaseFile} not found.");
                            }
                        }
                        break;
                }

            }

            if (result.Length <= result[^1].NodeIndex)
            {
                Trace.TraceError($"Non-consecutive tracknode indexes found in track database {trackdatabaseFile}");
            }
            return result;
        }

        private static JunctionNode ConvertJunctionNode(TrackJunctionNode junctionNode, TrackSectionModel trackSections, string trackdatabaseFile)
        {
            if (!trackSections.TrackShapes.TryGetValue(junctionNode.ShapeIndex, out Track.TrackShape trackShape) ||
                !trackSections.TrackShapePaths.TryGetValue(junctionNode.ShapeIndex, out ImmutableArray<TrackShapePath> shapePaths))
            {
                Trace.TraceWarning($"Track Shape Node #{junctionNode.ShapeIndex} not found for Junction Node {junctionNode.Index} in track database {trackdatabaseFile}, can not determine switch characteristics.");
                shapePaths = ImmutableArray<TrackShapePath>.Empty;
            }

            float angle = 0;
            /// Get the angle (direction in 2D) of the current junction diverging from main route
            for (int index = 0; index < shapePaths.Length; index++)
            {
                // The main route is considered the straight direction, so we look for the first diverging path to determine the angle of the junction.
                if (index == trackShape?.MainRoute)
                    continue;

                foreach (int sid in shapePaths[index].TrackSections)
                {
                    Track.TrackSection section = trackSections.TrackSections[sid];

                    if (section.Curved)
                    {
                        angle = section.Angle;
                        break;
                    }
                }
            }

            return new JunctionNode(junctionNode.UiD.Location, junctionNode.UiD.WorldTile, junctionNode.UiD.Direction)
            {
                NodeIndex = junctionNode.Index,
                WorldId = junctionNode.UiD.WorldId,
                OpeningAngle = angle,
                MainRoute = trackShape?.MainRoute ?? 0,
                ClearanceDistance = trackShape?.ClearanceDistance ?? 0,
                ShapeIndex = junctionNode.ShapeIndex,
            };
        }

        private static VectorNode ConvertVectorNode(TrackVectorNode vectorNode, TrackNodes trackNodes, TrackSectionModel trackSections, string trackdatabaseFile)
        {
            ImmutableArray<VectorSectionNode> sections = vectorNode.TrackVectorSections.Select((tvs, i) =>
                new VectorSectionNode(tvs.Location, tvs.WorldTile, tvs.Direction,
                    ComputeEndLocation(vectorNode.TrackVectorSections, i, vectorNode, trackNodes, trackSections, trackdatabaseFile))
                {
                    NodeIndex = tvs.SectionIndex,
                    ShapeIndex = tvs.ShapeIndex,
                    WorldId = (int)tvs.WorldFileUiD,
                    Flag1 = tvs.Flag1,
                    Flag2 = tvs.Flag2,
                }).ToImmutableArray();

            WorldLocation startLocation = sections.Length > 0 ? sections[0].Location : WorldLocation.None;
            Tile worldTile = sections.Length > 0 ? sections[0].WorldTile : Tile.Zero;
            WorldLocation endLocation = sections.Length > 0 ? sections[^1].EndLocation : WorldLocation.None;

            return new VectorNode(startLocation, worldTile, endLocation)
            {
                NodeIndex = vectorNode.Index,
                VectorSections = sections,
            };
        }

        private static ImmutableDictionary<int, TrackItemIndex> ConvertTrackSelectors(TrackNodes trackNodes)
        {
            return trackNodes == null
                ? ImmutableDictionary<int, TrackItemIndex>.Empty
                : trackNodes.OfType<TrackVectorNode>().Select(tvn => (tvn.Index, new TrackItemIndex()
                {
                    TrackItems = tvn.TrackItemIndices.ToImmutableArray(),
                })).ToImmutableDictionary(item => item.Index, item => item.Item2);
        }

        private static ImmutableArray<TrackItemBase> ConvertTrackItems(List<TrackItem> trackItems, TrackDatabase trackDatabase, TrackSectionModel trackSections,
            string trackdatabaseFile)
        {
            if (trackItems == null)
                return ImmutableArray<TrackItemBase>.Empty;

            //temporary map reverse-linking TrackItems to TrackNodes
            int[] trackNodeReferences = new int[trackItems.Count];
            foreach (KeyValuePair<int, TrackItemIndex> item in trackDatabase.TrackItemSelectors)
            {
                foreach (int itemIndex in item.Value.TrackItems)
                {
                    trackNodeReferences[itemIndex] = trackDatabase.TrackNodes[item.Key].NodeIndex;
                }
            }

            List<TrackItemBase> result = new List<TrackItemBase>();

            uint flags;

            foreach (TrackItem trackItem in trackItems)
            {
                WorldLocation? itemLocation = null;
                if (trackItem.Location == WorldLocation.None && trackItem is not EmptyItem)
                {
                    int trackNodeIndex;
                    if ((trackNodeIndex = trackNodeReferences[trackItem.TrackItemId]) == 0)
                    {   // nothing we can do with this, but we'll add an empty item to at least preserve the index
                        result.Add(new EmptyTrackItem()
                        {
                            TrackItemIndex = trackItem.TrackItemId,
                        });
                        //Trace.TraceWarning($"Track {trackItem.GetType().Name} #{trackItem.TrackItemId} in track database {trackdatabaseFile} has no (valid) location nor is related to a track vector.");
                        continue;
                    }
                    else
                    {
                        // Use the related TrackNode, assuming it's a VectorNode, and the TrackItem.SectionDistance to determine the location of the TrackItem, as some
                        // items have been found to have an invalid location but a valid related TrackNode and SectionDistance that can be used to determine the correct location.
                        itemLocation = WorldLocation.None;
                        if (trackDatabase.TrackNodes[trackNodeIndex] is not VectorNode vectorNode)
                            continue; // again, nothing we can do with this, but we'll add an empty item to at least preserve the index
                        float distance = trackItem.SData1;
                        for (int i = 0; i < vectorNode.VectorSections.Length; i++)
                        {
                            VectorSectionNode sectionNode = vectorNode.VectorSections[i];
                            if (!trackSections.TrackSections.TryGetValue(sectionNode.NodeIndex, out Track.TrackSection trackSection))
                                break;

                            if (distance > trackSection.Length)
                            {
                                distance -= trackSection.Length;
                                continue;
                            }
                            else
                            {
                                ref readonly WorldLocation endLocation = ref (i + 1 < vectorNode.VectorSections.Length) ? ref vectorNode.VectorSections[i + 1].Location :
                                    ref trackDatabase.TrackNodes[trackDatabase.TrackNodeConnectors[vectorNode.NodeIndex].TrackNodeConnectors[1].Link].Location;

                                itemLocation = trackSection.Curved
                                    ? WorldLocation.PointAlongArc(sectionNode.Location, endLocation, MathHelper.ToRadians(trackSection.Angle), trackSection.Radius, distance / trackSection.Radius)
                                    : WorldLocation.PointAlongDirection(sectionNode.Location, endLocation, distance);
                                break;
                            }
                        }
                    }
                }

                switch (trackItem)
                {
                    case SidingItem sidingItem:
                        result.Add(new SidingTrackItem(itemLocation ?? sidingItem.Location)
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
                        result.Add(new PlatformTrackItem(itemLocation ?? platformItem.Location)
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
                        result.Add(speedPostItem.IsMilePost ? new MilepostTrackItem(itemLocation ?? speedPostItem.Location)
                        {
                            NodeIndex = trackNodeReferences[speedPostItem.TrackItemId],
                            SectionDistance = speedPostItem.SData1,
                            Flags = uint.TryParse(speedPostItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = speedPostItem.TrackItemId,
                            DistanceValue = speedPostItem.Distance,
                        } : new SpeedpostTrackItem(itemLocation ?? speedPostItem.Location)
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
                        result.Add(new HazardTrackItem(itemLocation ?? hazardItem.Location)
                        {
                            NodeIndex = trackNodeReferences[hazardItem.TrackItemId],
                            SectionDistance = hazardItem.SData1,
                            Flags = uint.TryParse(hazardItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = hazardItem.TrackItemId,
                        });
                        break;
                    case PickupItem pickupItem:
                        result.Add(new PickupTrackItem(itemLocation ?? pickupItem.Location)
                        {
                            NodeIndex = trackNodeReferences[pickupItem.TrackItemId],
                            SectionDistance = pickupItem.SData1,
                            Flags = uint.TryParse(pickupItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = pickupItem.TrackItemId,
                        });
                        break;
                    case LevelCrossingItem levelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(itemLocation ?? levelCrossingItem.Location)
                        {
                            NodeIndex = trackNodeReferences[levelCrossingItem.TrackItemId],
                            SectionDistance = levelCrossingItem.SData1,
                            Flags = uint.TryParse(levelCrossingItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = levelCrossingItem.TrackItemId,
                        });
                        break;
                    case RoadLevelCrossingItem roadLevelCrossingItem: // road level crossings are not really useful and no route seems to contain them, but we'll just treat them as LevelCrossings
                        result.Add(new RoadLevelCrossingTrackItem(itemLocation ?? roadLevelCrossingItem.Location)
                        {
                            NodeIndex = trackNodeReferences[roadLevelCrossingItem.TrackItemId],
                            SectionDistance = roadLevelCrossingItem.SData1,
                            Flags = uint.TryParse(roadLevelCrossingItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = roadLevelCrossingItem.TrackItemId,
                        });
                        break;
                    case SoundRegionItem soundRegionItem:
                        result.Add(new SoundRegionTrackItem(itemLocation ?? soundRegionItem.Location)
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
                        result.Add(new SignalTrackItem(itemLocation ?? signalItem.Location)
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
                        result.Add(new CrossoverTrackItem(itemLocation ?? crossOverItem.Location)
                        {
                            NodeIndex = trackNodeReferences[crossOverItem.TrackItemId],
                            SectionDistance = crossOverItem.SData1,
                            Flags = uint.TryParse(crossOverItem.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = crossOverItem.TrackItemId,
                            ShapeIndex = crossOverItem.ShapeId,
                        });
                        break;
                    case RoadCarSpawnerItem carSpawner:
                        result.Add(new CarSpawnerTrackItem(itemLocation ?? carSpawner.Location)
                        {
                            NodeIndex = trackNodeReferences[carSpawner.TrackItemId],
                            SectionDistance = carSpawner.SData1,
                            Flags = uint.TryParse(carSpawner.SData2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out flags) ? flags : 0,
                            TrackItemIndex = carSpawner.TrackItemId,
                        });
                        break;
                    case EmptyItem emptyItem:
                        result.Add(new EmptyTrackItem()
                        {
                            TrackItemIndex = emptyItem.TrackItemId,
                        });
                        break;
                    default:
                        Trace.TraceWarning($"{trackItem.GetType().Name} Index #{trackItem.TrackItemId} not supported for Track Items in track database {trackdatabaseFile}");
                        break;
                }
            }

            //Validate linked platforms and sidings, and convert invalid linked items to empty items
            LinkSidingItems(result, trackdatabaseFile);
            LinkPlatformItems(result, trackdatabaseFile);
            return result.ToImmutableArray();
        }

        private static void LinkSidingItems(List<TrackItemBase> trackItems, string trackdatabaseFile)
        {
            Dictionary<int, SidingTrackItem> sidingItemMappings = trackItems.OfType<SidingTrackItem>().ToDictionary(p => p.TrackItemIndex);

            for (int i = 0; i < trackItems.Count; i++)
            {
                if (trackItems[i] is not SidingTrackItem start)
                    continue;

                if (sidingItemMappings.TryGetValue(start.LinkedSidingItem, out SidingTrackItem end))
                {
                    if (end.LinkedSidingItem == start.TrackItemIndex)
                    {
                        _ = sidingItemMappings.Remove(end.TrackItemIndex);
                        _ = sidingItemMappings.Remove(start.TrackItemIndex);

                        if (end.SidingName != start.SidingName)
                        {
                            string endName = end.SidingName.Trim();
                            string startName = start.SidingName.Trim();
                            switch (endName.Length.CompareTo(startName.Length))
                            {
                                case int n when n < 0:
                                    endName = startName;
                                    Trace.TraceWarning($"Siding Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                        $"from Source Id {start.TrackItemIndex} name \"{start.SidingName}\" to Target id {end.TrackItemIndex} name \"{end.SidingName}\". Using {startName}");
                                    break;
                                case int n when n > 0:
                                    startName = endName;
                                    Trace.TraceWarning($"Siding Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                        $"from Source Id {start.TrackItemIndex} name \"{start.SidingName}\" to Target id {end.TrackItemIndex} name \"{end.SidingName}\". Using {endName}");
                                    break;
                                case 0:
                                    break;
                            }
                            trackItems[start.TrackItemIndex] = start with { SidingName = startName };
                            trackItems[end.TrackItemIndex] = end with { SidingName = endName };
                        }
                    }
                    else
                    {
                        Trace.TraceWarning($"Siding Item pair in track database {trackdatabaseFile} has inconsistent linking " +
                            $"from Source Id {start.TrackItemIndex} to target {start.LinkedSidingItem} vs Target id {end.TrackItemIndex} to source {end.LinkedSidingItem}.");
                    }
                }
            }

            while (sidingItemMappings.Count > 0)
            {
                bool match = false;
                int sourceId = sidingItemMappings.Keys.First();
                SidingTrackItem start = sidingItemMappings[sourceId];
                _ = sidingItemMappings.Remove(sourceId);

                foreach (KeyValuePair<int, SidingTrackItem> item in sidingItemMappings)
                {
                    SidingTrackItem end = item.Value;
                    if (end.SidingName == start.SidingName)
                    {
                        _ = sidingItemMappings.Remove(end.TrackItemIndex);
                        trackItems[start.TrackItemIndex] = start with { LinkedSidingItem = end.TrackItemIndex };
                        trackItems[end.TrackItemIndex] = end with { LinkedSidingItem = start.TrackItemIndex };
                        Trace.TraceWarning($"Matching Siding Items in track database {trackdatabaseFile} by Name {start.SidingName} Id {start.TrackItemIndex} to target {start.LinkedSidingItem} vs Target id {item.Value.TrackItemIndex} to source {item.Value.LinkedSidingItem}.");
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    Trace.TraceWarning($"Linked Siding Item {start.LinkedSidingItem} for Siding Item {start.TrackItemIndex} not found in track database {trackdatabaseFile}.");
                    trackItems[start.TrackItemIndex] = new EmptyTrackItem() { TrackItemIndex = start.TrackItemIndex };
                }
            }
        }

        private static void LinkPlatformItems(List<TrackItemBase> trackItems, string trackdatabaseFile)
        {
            Dictionary<int, PlatformTrackItem> platformItemMappings = trackItems.OfType<PlatformTrackItem>().ToDictionary(p => p.TrackItemIndex);

            for (int i = 0; i < trackItems.Count; i++)
            {
                if (trackItems[i] is not PlatformTrackItem start)
                    continue;

                if (platformItemMappings.TryGetValue(start.LinkedPlatformItem, out PlatformTrackItem end))
                {
                    if (end.LinkedPlatformItem == start.TrackItemIndex)
                    {
                        _ = platformItemMappings.Remove(end.TrackItemIndex);
                        _ = platformItemMappings.Remove(start.TrackItemIndex);

                        if (end.StationName != start.StationName || end.PlatformName != start.PlatformName)
                        {
                            if (end.StationName != start.StationName)
                            {
                                string endName = end.StationName.Trim();
                                string startName = start.StationName.Trim();
                                switch (endName.Length.CompareTo(startName.Length))
                                {
                                    case int n when n < 0:
                                        endName = startName;
                                        end = end with { StationName = endName };
                                        Trace.TraceWarning($"Platform Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                            $"from Source Id {start.TrackItemIndex} name \"{start.PlatformName}\" at station \"{start.StationName}\" " +
                                            $"to Target id {end.TrackItemIndex} name \"{end.PlatformName}\" at station \"{end.StationName}\". Using {startName}");
                                        break;
                                    case int n when n > 0:
                                        startName = endName;
                                        start = start with { StationName = startName };
                                        Trace.TraceWarning($"Platform Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                            $"from Source Id {start.TrackItemIndex} name \"{start.PlatformName}\" at station \"{start.StationName}\" " +
                                            $"to Target id {end.TrackItemIndex} name \"{end.PlatformName}\" at station \"{end.StationName}\". Using {endName}");
                                        break;
                                    case 0:
                                        break;
                                }
                            }
                            else if (end.PlatformName != start.PlatformName)
                            {
                                string endName = end.PlatformName.Trim();
                                string startName = start.PlatformName.Trim();
                                switch (endName.Length.CompareTo(startName.Length))
                                {
                                    case int n when n < 0:
                                        endName = startName;
                                        end = end with { StationName = endName };
                                        Trace.TraceWarning($"Platform Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                            $"from Source Id {start.TrackItemIndex} name \"{start.PlatformName}\" at station \"{start.StationName}\" " +
                                            $"to Target id {end.TrackItemIndex} name \"{end.PlatformName}\" at station \"{end.StationName}\". Using {startName}");
                                        break;
                                    case int n when n > 0:
                                        startName = endName;
                                        start = start with { StationName = startName };
                                        Trace.TraceWarning($"Platform Item pair in track database {trackdatabaseFile} has inconsistent naming " +
                                            $"from Source Id {start.TrackItemIndex} name \"{start.PlatformName}\" at station \"{start.StationName}\" " +
                                            $"to Target id {end.TrackItemIndex} name \"{end.PlatformName}\" at station \"{end.StationName}\". Using {endName}");
                                        break;
                                    case 0:
                                        break;
                                }
                            }
                            trackItems[start.TrackItemIndex] = start;
                            trackItems[end.TrackItemIndex] = end;
                        }
                    }
                    else
                    {
                        Trace.TraceWarning($"Platform Item pair in track database {trackdatabaseFile} has inconsistent linking " +
                            $"from Source Id {start.TrackItemIndex} to target {start.LinkedPlatformItem} vs Target id {end.TrackItemIndex} to source {end.LinkedPlatformItem}.");
                    }
                }
            }

            while (platformItemMappings.Count > 0)
            {
                bool match = false;
                int sourceId = platformItemMappings.Keys.First();
                PlatformTrackItem start = platformItemMappings[sourceId];
                _ = platformItemMappings.Remove(sourceId);

                foreach (KeyValuePair<int, PlatformTrackItem> item in platformItemMappings)
                {
                    PlatformTrackItem end = item.Value;
                    if (end.PlatformName == start.PlatformName && item.Value.StationName == start.StationName)
                    {
                        _ = platformItemMappings.Remove(end.TrackItemIndex);
                        trackItems[start.TrackItemIndex] = start with { LinkedPlatformItem = end.TrackItemIndex };
                        trackItems[end.TrackItemIndex] = end with { LinkedPlatformItem = start.TrackItemIndex };
                        Trace.TraceWarning($"Matching Platform Items in track database {trackdatabaseFile} by Name " +
                                            $"from Source Id {start.TrackItemIndex} name \"{start.PlatformName}\" at station \"{start.StationName}\" " +
                                            $"to Target id {end.TrackItemIndex} name \"{end.PlatformName}\" at station \"{end.StationName}\".");
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    Trace.TraceWarning($"Linked Platform Item {start.LinkedPlatformItem} for Platform Item {start.TrackItemIndex} not found in track database {trackdatabaseFile}.");
                    trackItems[start.TrackItemIndex] = new EmptyTrackItem() { TrackItemIndex = start.TrackItemIndex };
                }
            }
        }

        /// <summary>
        /// Computes the 3D world location of the far end of a track vector section.
        /// The direction is determined by the start location of the next section (or the connected node for the last section).
        /// Falls back to <paramref name="nextLocation"/> when the section index is not found in <paramref name="trackSections"/>.
        /// </summary>
        private static WorldLocation ComputeEndLocation(TrackVectorSection[] sections, int index, TrackVectorNode vectorNode, TrackNodes trackNodes, TrackSectionModel trackSections, string trackdatabaseFile)
        {
            // The far end of this section is the start of the next one, or the connected node's location for the last section.
            WorldLocation nextLocation = index + 1 < sections.Length
                ? sections[index + 1].Location
                : trackNodes[vectorNode.TrackPins[vectorNode.InPins].Link]?.UiD.Location ?? WorldLocation.None;

            if (!trackSections.TrackSections.TryGetValue(sections[index].SectionIndex, out Track.TrackSection trackSection))
            {
                return nextLocation;
            }
            if (WorldLocation.GetDistanceSquared(sections[index].Location, nextLocation) < 0.001)
            {
                Trace.TraceWarning($"Vector Node {vectorNode.Index} section index {index} from {trackdatabaseFile} has degenerate next location - computing end position from direction and geometry");
                return ComputeEndLocationFromDirection(sections[index], trackSection);
            }

            return trackSection.Curved
                ? WorldLocation.PointAlongArc(sections[index].Location, nextLocation,
                    MathHelper.ToRadians(trackSection.Angle), trackSection.Radius, Math.Abs(MathHelper.ToRadians(trackSection.Angle)))
                : WorldLocation.PointAlongDirection(sections[index].Location, nextLocation, trackSection.Length);
        }

        /// <summary>
        /// Computes the end location of a track vector section using only its start position, heading direction, and section geometry.
        /// Used as a fallback when the next section's start location coincides with this section's start (degenerate graph data).
        /// Assumes the section lies in a horizontal plane — elevation (Y) is preserved from the start location.
        /// Mirrors the endpoint formulas used in <see cref="FreetrainSimulator.Runtime.Track.TrackSegmentBase"/>.
        /// </summary>
        private static WorldLocation ComputeEndLocationFromDirection(TrackVectorSection section, Track.TrackSection trackSection)
        {
            double cosA = Math.Cos(section.Direction.Y);
            double sinA = Math.Sin(section.Direction.Y);
            ref readonly WorldLocation start = ref section.Location;
            float endX, endZ;

            if (trackSection.Curved)
            {
                float arcAngle = MathHelper.ToRadians(trackSection.Angle);
                int sign = -Math.Sign(trackSection.Angle);
                double cosArotated = Math.Cos(section.Direction.Y + arcAngle);
                double sinArotated = Math.Sin(section.Direction.Y + arcAngle);
                double deltaX = sign * trackSection.Radius * (cosA - cosArotated);
                double deltaZ = sign * trackSection.Radius * (sinA - sinArotated);
                endX = start.Location.X - (float)deltaX;
                endZ = start.Location.Z + (float)deltaZ;
            }
            else
            {
                endX = start.Location.X + (float)(sinA * trackSection.Length);
                endZ = start.Location.Z + (float)(cosA * trackSection.Length);
            }

            return new WorldLocation(start.Tile, new Vector3(endX, start.Location.Y, endZ), true);
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
