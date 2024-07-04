using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Widgets;
using Orts.Graphics.Xna;
using Orts.Models.Track;

namespace Orts.Graphics.MapView
{
    public enum ToolboxContentMode
    {
        ViewRoute,
        ViewPath,
        EditPath,
    }

    public class ToolboxContent : ContentBase
    {
        private (double distance, INameValueInformationProvider statusItem) nearestSegmentForStatus;
        private (double distance, INameValueInformationProvider statusItem) nearestItemForStatus;

        private readonly InsetComponent insetComponent;
        private ToolboxContentMode contentMode;

        internal PathEditorBase PathEditor { get; set; }

        public INameValueInformationProvider TrackNodeInfo { get; } = new DetailInfoProxy();

        public INameValueInformationProvider TrackItemInfo { get; } = new DetailInfoProxy();

        public ToolboxContentMode ContentMode
        {
            get => contentMode;
            internal set
            {
                contentMode = value;
                if (value == ToolboxContentMode.ViewPath)
                    viewSettings[MapContentType.Paths] = true;
            }
        }

        public ToolboxContent(Game game) :
            base(game)
        {
            FormattingOptions.Add("Route Information", FormatOption.Bold);
            DetailInfo.Add("Route Information", null);
            DetailInfo["Route Name"] = RuntimeData.GameInstance(game).RouteName;
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
        }

        public override async Task Initialize()
        {
            await Task.Run(() => AddTrackSegments()).ConfigureAwait(false);
            await Task.Run(() => AddTrackItems()).ConfigureAwait(false);

            ContentArea.Initialize();
            //just put an empty list so the draw method does not skip the paths
            trackModel.ContentByTile[MapContentType.Paths] = new TileIndexedList<EditorTrainPath, Tile>(new List<EditorTrainPath>() { });

            DetailInfo["Metric Scale"] = RuntimeData.GameInstance(game).UseMetricUnits.ToString();
            DetailInfo["Track Nodes"] = $"{trackModel.SegmentSections.Count}";
            DetailInfo["Track Segments"] = $"{trackModel.ContentByTile[MapContentType.Tracks].ItemCount}";
            DetailInfo["Track End Segments"] = $"{trackModel.ContentByTile[MapContentType.EndNodes].ItemCount}";
            DetailInfo["Junction Segments"] = $"{trackModel.ContentByTile[MapContentType.JunctionNodes].ItemCount}";
            DetailInfo["Road Nodes"] = $"{trackModel.RoadSegmentSections.Count}";
            DetailInfo["Road Segments"] = $"{trackModel.ContentByTile[MapContentType.Roads].ItemCount}";
            DetailInfo["Road End Segments"] = $"{trackModel.ContentByTile[MapContentType.RoadEndNodes].ItemCount}";
            DetailInfo["Tiles"] = $"{trackModel.ContentByTile[MapContentType.Grid].Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences)
        {
            ArgumentNullException.ThrowIfNull(colorPreferences);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ContentArea.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]));
            }
        }

        internal override void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight)
        {
            nearestSegmentForStatus = (float.MaxValue, null);
            nearestItemForStatus = (float.MaxValue, null);
            GridTile nearestGridTile = trackModel.ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid])
            {
                nearestItems[MapContentType.Grid] = nearestGridTile;
            }

            foreach (MapContentType viewItem in EnumExtension.GetValues<MapContentType>())
            {
                double distanceSquared = double.MaxValue;
                if (viewItem == MapContentType.Grid)
                    //already checked above
                    continue;
                if (viewSettings[viewItem] && trackModel.ContentByTile[viewItem] != null)
                {
                    foreach (ITileCoordinate<Tile> item in trackModel.ContentByTile[viewItem].BoundingBox(bottomLeft, topRight))
                    {
                        if (item is VectorPrimitive vectorPrimitive)
                        {
                            double itemDistance = vectorPrimitive.DistanceSquared(position);
                            if (itemDistance < distanceSquared)
                            {
                                nearestItems[viewItem] = vectorPrimitive;
                                distanceSquared = itemDistance;
                            }
                        }
                        else if (item is PointPrimitive pointPrimitive)
                        {
                            double itemDistance = pointPrimitive.Location.DistanceSquared(position);
                            if (itemDistance < distanceSquared)
                            {
                                nearestItems[viewItem] = pointPrimitive;
                                distanceSquared = itemDistance;
                            }
                        }
                    }
                }
                if (distanceSquared < 1000)
                {
                    switch (viewItem)
                    {
                        case MapContentType.Tracks:
                        case MapContentType.JunctionNodes:
                        case MapContentType.EndNodes:
                        case MapContentType.Roads:
                        case MapContentType.RoadCrossings:
                        case MapContentType.RoadEndNodes:
                            if (distanceSquared < 1 || distanceSquared < nearestSegmentForStatus.distance)
                                nearestSegmentForStatus = (distanceSquared, nearestItems[viewItem] as INameValueInformationProvider);
                            break;
                        default:
                            if (distanceSquared < 1 || distanceSquared < nearestItemForStatus.distance)
                                nearestItemForStatus = (distanceSquared, nearestItems[viewItem] as INameValueInformationProvider);
                            break;
                    }
                }
                else
                    nearestItems[viewItem] = null;
            }

            (TrackNodeInfo as DetailInfoProxy).Source = nearestSegmentForStatus.statusItem;
            (TrackItemInfo as DetailInfoProxy).Source = nearestItemForStatus.statusItem;

            if (ContentMode == ToolboxContentMode.EditPath)
            {
                PathEditor?.UpdatePointerLocation(position, nearestItems[MapContentType.Tracks] as TrackSegment);
                ContentArea.SuppressDrawing = false;
            }
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            foreach (MapContentType viewItemSetting in EnumExtension.GetValues<MapContentType>())
            {
                if (viewSettings[viewItemSetting] && trackModel.ContentByTile[viewItemSetting] != null)
                {
                    if (viewItemSetting == MapContentType.Paths)
                    {
                        PathEditor?.Draw();
                    }
                    else
                    {
                        foreach (ITileCoordinate<Tile> item in trackModel.ContentByTile[viewItemSetting].BoundingBox(bottomLeft, topRight))
                        {
                            // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                            // but the performance impact/overhead seems invariant
                            if (item is VectorPrimitive vectorPrimitive && ContentArea.InsideScreenArea(vectorPrimitive))
                            {
                                (item as IDrawable<VectorPrimitive>).Draw(ContentArea);
                            }
                            else if (item is PointPrimitive pointPrimitive && ContentArea.InsideScreenArea(pointPrimitive))
                            {
                                (item as IDrawable<PointPrimitive>).Draw(ContentArea);
                            }
                        }
                    }
                }
            }
            if (ContentMode == ToolboxContentMode.ViewRoute || !viewSettings[MapContentType.Paths])
            {
                if (null != nearestItems[MapContentType.Tracks])
                {
                    foreach (TrackSegmentBase segment in trackModel.SegmentSections[(nearestItems[MapContentType.Tracks] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(ContentArea, ColorVariation.ComplementHighlight);
                    }
                }
                if (null != nearestItems[MapContentType.Roads])
                {
                    foreach (TrackSegmentBase segment in trackModel.RoadSegmentSections[(nearestItems[MapContentType.Roads] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(ContentArea, ColorVariation.ComplementHighlight);
                    }
                }

                foreach (MapContentType viewItemSettings in EnumExtension.GetValues<MapContentType>())
                {
                    if (viewSettings[viewItemSettings] && nearestItems[viewItemSettings] != null)
                    {
                        if (nearestItems[viewItemSettings] is VectorPrimitive vectorPrimitive && ContentArea.InsideScreenArea(vectorPrimitive))
                            (vectorPrimitive as IDrawable<VectorPrimitive>).Draw(ContentArea, ColorVariation.Complement);
                        else if (nearestItems[viewItemSettings] is PointPrimitive pointPrimitive && ContentArea.InsideScreenArea(pointPrimitive))
                            (pointPrimitive as IDrawable<PointPrimitive>).Draw(ContentArea, ColorVariation.Complement);
                    }
                }
            }
        }

        #region build content database
        private void AddTrackSegments()
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackDB trackDB = runtimeData.TrackDB;
            RoadTrackDB roadTrackDB = runtimeData.RoadTrackDB;
            TrackSectionsFile trackSectionsFile = runtimeData.TSectionDat;
            if (null == trackSectionsFile)
                throw new ArgumentNullException(nameof(trackSectionsFile));

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<EndNode> endSegments = new ConcurrentBag<EndNode>();
            ConcurrentBag<JunctionNode> junctionSegments = new ConcurrentBag<JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            Parallel.ForEach(trackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = trackDB.TrackNodes.VectorNodes[trackEndNode.TrackPins[0].Link];
                        endSegments.Add(new EndNode(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        break;
                    case TrackVectorNode trackVectorNode:
                        int i = 0;
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            trackSegments.Add(new TrackSegment(trackVectorSection, trackSectionsFile.TrackSections, trackVectorNode.Index, i++));
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        List<TrackVectorNode> vectorNodes = new List<TrackVectorNode>();
                        foreach (TrackPin pin in trackJunctionNode.TrackPins)
                        {
                            vectorNodes.Add(trackDB.TrackNodes.VectorNodes[pin.Link]);
                        }
                        junctionSegments.Add(new JunctionNode(trackJunctionNode, trackSectionsFile.TrackShapes[trackJunctionNode.ShapeIndex].MainRoute, vectorNodes, trackSectionsFile.TrackSections));
                        break;
                }
            });

            insetComponent?.SetTrackSegments(trackSegments);

            trackModel = TrackModel.Reset(game, runtimeData);
            trackModel.InitializeRailTrack(trackSegments, junctionSegments, endSegments);

            Parallel.ForEach(roadTrackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = roadTrackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        roadEndSegments.Add(new RoadEndSegment(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        break;
                    case TrackVectorNode trackVectorNode:
                        int i = 0;
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            roadSegments.Add(new RoadSegment(trackVectorSection, trackSectionsFile.TrackSections, trackVectorNode.Index, i++));
                        }
                        break;
                }
            });

            trackModel.InitializeRoadTrack(roadSegments, roadEndSegments);

            // identify all tiles by looking at tracks and roads and their respective end segments
            trackModel.ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile, Tile>(
                trackModel.ContentByTile[MapContentType.Tracks].Select(d => d.Tile as ITile).Distinct()
                .Union(trackModel.ContentByTile[MapContentType.EndNodes].Select(d => d.Tile as ITile).Distinct())
                .Union(trackModel.ContentByTile[MapContentType.Roads].Select(d => d.Tile as ITile).Distinct())
                .Union(trackModel.ContentByTile[MapContentType.RoadEndNodes].Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            trackModel.ContentByTile[MapContentType.Grid] = trackModel.ContentByTile[MapContentType.Grid];
            InitializeBounds();
        }

        private void AddTrackItems()
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);

            IEnumerable<TrackItemBase> trackItems = TrackItemWidget.CreateTrackItems(
                runtimeData.TrackDB?.TrackItems,
                runtimeData.SignalConfigFile,
                runtimeData.TrackDB, trackModel.SegmentSections).Concat(TrackItemWidget.CreateRoadItems(runtimeData.RoadTrackDB?.TrackItems));

            trackModel.InitializeTrackItems(trackItems);

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackModel, trackItems.OfType<PlatformTrackItem>());
            trackModel.ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath, Tile>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackModel, trackItems.OfType<SidingTrackItem>());
            trackModel.ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath, Tile>(sidings);

            IEnumerable<SignalTrackItem> signals = trackItems.OfType<SignalTrackItem>();

            trackModel.ContentByTile[MapContentType.Signals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));
            trackModel.ContentByTile[MapContentType.OtherSignals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => !s.Normal));
            trackModel.ContentByTile[MapContentType.MilePosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => s.MilePost));
            trackModel.ContentByTile[MapContentType.SpeedPosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => !s.MilePost));
            trackModel.ContentByTile[MapContentType.CrossOvers] = new TileIndexedList<CrossOverTrackItem, Tile>(trackItems.OfType<CrossOverTrackItem>());
            trackModel.ContentByTile[MapContentType.RoadCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => s.RoadLevelCrossing));
            trackModel.ContentByTile[MapContentType.LevelCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => !s.RoadLevelCrossing));
            trackModel.ContentByTile[MapContentType.Hazards] = new TileIndexedList<HazardTrackItem, Tile>(trackItems.OfType<HazardTrackItem>());
            trackModel.ContentByTile[MapContentType.Pickups] = new TileIndexedList<PickupTrackItem, Tile>(trackItems.OfType<PickupTrackItem>());
            trackModel.ContentByTile[MapContentType.SoundRegions] = new TileIndexedList<SoundRegionTrackItem, Tile>(trackItems.OfType<SoundRegionTrackItem>());
            trackModel.ContentByTile[MapContentType.CarSpawners] = new TileIndexedList<CarSpawnerTrackItem, Tile>(trackItems.OfType<CarSpawnerTrackItem>());
            trackModel.ContentByTile[MapContentType.Empty] = new TileIndexedList<EmptyTrackItem, Tile>(trackItems.OfType<EmptyTrackItem>());

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            trackModel.ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem, Tile>(StationNameItem.CreateStationItems(stations));
            trackModel.ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem, Tile>(platforms.Select(p => new PlatformNameItem(p)));
            trackModel.ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem, Tile>(sidings.Select(p => new SidingNameItem(p)));
        }
        #endregion

        private protected class DetailInfoProxy : DetailInfoProxyBase
        {
            internal INameValueInformationProvider Source;

            public override InformationDictionary DetailInfo => Source?.DetailInfo;

            public override Dictionary<string, FormatOption> FormattingOptions => Source?.FormattingOptions;
        }
    }
}
