using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Imported.Runtime;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Graphics.MapView
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
            DetailInfo["Route Name"] = RuntimeData.GameInstance(game).RouteData.Name;
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
        }

        public override async Task Initialize()
        {
            await Task.Run(AddTrackSegments).ConfigureAwait(false);
            await Task.Run(AddTrackItems).ConfigureAwait(false);

            ContentArea.Initialize();
            //just put an empty list so the draw method does not skip the paths
            trackModel.ContentByTile[MapContentType.Paths] = new TileIndexedList<EditorTrainPath>(new List<EditorTrainPath>() { });

            DetailInfo["Metric Scale"] = RuntimeData.GameInstance(game).MetricUnits.ToString();
            DetailInfo["Track Nodes"] = $"{trackModel.SegmentSections.Count}";
            DetailInfo["Track Segments"] = $"{trackModel.ContentByTile[MapContentType.Tracks].ItemCount}";
            DetailInfo["Track End Segments"] = $"{trackModel.ContentByTile[MapContentType.EndNodes].ItemCount}";
            DetailInfo["Junction Segments"] = $"{trackModel.ContentByTile[MapContentType.JunctionNodes].ItemCount}";
            DetailInfo["Road Nodes"] = $"{trackModel.RoadSegmentSections.Count}";
            DetailInfo["Road Segments"] = $"{trackModel.ContentByTile[MapContentType.Roads].ItemCount}";
            DetailInfo["Road End Segments"] = $"{trackModel.ContentByTile[MapContentType.RoadEndNodes].ItemCount}";
            DetailInfo["Tiles"] = $"{trackModel.ContentByTile[MapContentType.Grid].Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences, bool fontOutlining, bool limitTrackWidth)
        {
            ArgumentNullException.ThrowIfNull(colorPreferences);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ContentArea.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]), fontOutlining);
            }
            ContentArea.UpdateTrackWidthSettings(limitTrackWidth);
        }

        internal override void UpdatePointerLocation(in PointD position, in Tile bottomLeft, in Tile topRight)
        {
            nearestSegmentForStatus = (float.MaxValue, null);
            nearestItemForStatus = (float.MaxValue, null);
            GridTile nearestGridTile = trackModel.ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid] as GridTile)
                nearestItems[MapContentType.Grid] = nearestGridTile;

            foreach (MapContentType viewItem in EnumExtension.GetValues<MapContentType>())
            {
                double distanceSquared = double.MaxValue;
                if (viewItem == MapContentType.Grid)
                    //already checked above
                    continue;
                if (viewSettings[viewItem] && trackModel.ContentByTile[viewItem] != null)
                {
                    foreach (ITileCoordinate item in trackModel.ContentByTile[viewItem].BoundingBox(bottomLeft, topRight))
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

        internal override void Draw(in Tile bottomLeft, in Tile topRight)
        {
            foreach (MapContentType viewItemSetting in EnumExtension.GetValues<MapContentType>())
            {
                if (viewSettings[viewItemSetting] && trackModel.ContentByTile[viewItemSetting] != null)
                {
                    if (viewItemSetting == MapContentType.Paths)
                        PathEditor?.Draw();
                    else
                    {
                        foreach (ITileCoordinate item in trackModel.ContentByTile[viewItemSetting].BoundingBox(bottomLeft, topRight))
                        {
                            // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                            // but the performance impact/overhead seems invariant
                            if (item is VectorPrimitive vectorPrimitive && ContentArea.InsideScreenArea(vectorPrimitive))
                                (item as IDrawable<VectorPrimitive>).Draw(ContentArea);
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
            TrackSectionsModel trackSections = RuntimeData.GameInstance(game).TrackSections;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<Widgets.EndNode> endSegments = new ConcurrentBag<Widgets.EndNode>();
            ConcurrentBag<Widgets.JunctionNode> junctionSegments = new ConcurrentBag<Widgets.JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            if (runtimeData.TrackModel.TrackDatabase != null)
            {
                Parallel.ForEach(runtimeData.TrackModel.TrackDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode endNode:
                            endSegments.Add(new Widgets.EndNode(endNode));
                            break;
                        case VectorNode trackVectorNode:
                            int i = 0;
                            foreach (VectorSectionNode trackVectorSection in trackVectorNode.VectorSections)
                            {
                                trackSegments.Add(new TrackSegment(trackVectorSection, trackVectorNode.NodeIndex, i++));
                            }
                            break;
                        case Models.Track.JunctionNode trackJunctionNode:
                            junctionSegments.Add(new Widgets.JunctionNode(trackJunctionNode, trackSections.TrackShapes[trackJunctionNode.ShapeIndex].MainRoute));
                            break;
                    }
                });
            }

            insetComponent?.SetTrackSegments(trackSegments);

            trackModel = Models.Imported.Runtime.TrackModel.Reset(game, runtimeData);
            trackModel.InitializeRailTrack(trackSegments, junctionSegments, endSegments);

            if (runtimeData.TrackModel.RoadDatabase != null)
            {
                Parallel.ForEach(runtimeData.TrackModel.RoadDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode trackEndNode:
                            roadEndSegments.Add(new Widgets.RoadEndSegment(trackEndNode));
                            break;
                        case VectorNode trackVectorNode:
                            int i = 0;
                            foreach (VectorSectionNode trackVectorSection in trackVectorNode.VectorSections)
                            {
                                roadSegments.Add(new RoadSegment(trackVectorSection, trackVectorNode.NodeIndex, i++));
                            }
                            break;
                    }
                });
            }

            trackModel.InitializeRoadTrack(roadSegments, roadEndSegments);

            // identify all tiles by looking at tracks and roads and their respective end segments
            trackModel.ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile>(
                trackModel.ContentByTile[MapContentType.Tracks].Select(d => d.Tile).Distinct()
                .Union(trackModel.ContentByTile[MapContentType.EndNodes].Select(d => d.Tile).Distinct())
                .Union(trackModel.ContentByTile[MapContentType.Roads].Select(d => d.Tile).Distinct())
                .Union(trackModel.ContentByTile[MapContentType.RoadEndNodes].Select(d => d.Tile).Distinct())
                .Select(t => new GridTile(t)));

            trackModel.ContentByTile[MapContentType.Grid] = trackModel.ContentByTile[MapContentType.Grid];
            InitializeBounds();
        }

        private void AddTrackItems()
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);

            IEnumerable<TrackItemBase> trackItems = TrackItemWidget.CreateTrackItems(
                runtimeData.TrackModel.TrackDatabase,
                runtimeData.SignalConfigFile,
                trackModel.SegmentSections).Concat(TrackItemWidget.CreateRoadItems(runtimeData.TrackModel.RoadDatabase));

            trackModel.InitializeTrackItems(trackItems);

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackModel, trackItems.OfType<Widgets.PlatformTrackItem>());
            trackModel.ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackModel, trackItems.OfType<Widgets.SidingTrackItem>());
            trackModel.ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath>(sidings);

            IEnumerable<Widgets.SignalTrackItem> signals = trackItems.OfType<Widgets.SignalTrackItem>();

            trackModel.ContentByTile[MapContentType.Signals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => s.Normal));
            trackModel.ContentByTile[MapContentType.OtherSignals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => !s.Normal));
            trackModel.ContentByTile[MapContentType.MilePosts] = new TileIndexedList<SpeedPostTrackItem>(trackItems.OfType<SpeedPostTrackItem>());
            trackModel.ContentByTile[MapContentType.SpeedPosts] = new TileIndexedList<MilePostTrackItem>(trackItems.OfType<MilePostTrackItem>());
            trackModel.ContentByTile[MapContentType.Crossovers] = new TileIndexedList<CrossOverTrackItem>(trackItems.OfType<CrossOverTrackItem>());
            trackModel.ContentByTile[MapContentType.RoadCrossings] = new TileIndexedList<Widgets.LevelCrossingTrackItem>(trackItems.OfType<Widgets.LevelCrossingTrackItem>().Where(s => s.RoadLevelCrossing));
            trackModel.ContentByTile[MapContentType.LevelCrossings] = new TileIndexedList<Widgets.LevelCrossingTrackItem>(trackItems.OfType<Widgets.LevelCrossingTrackItem>().Where(s => !s.RoadLevelCrossing));
            trackModel.ContentByTile[MapContentType.Hazards] = new TileIndexedList<Widgets.HazardTrackItem>(trackItems.OfType<Widgets.HazardTrackItem>());
            trackModel.ContentByTile[MapContentType.Pickups] = new TileIndexedList<Widgets.PickupTrackItem>(trackItems.OfType<Widgets.PickupTrackItem>());
            trackModel.ContentByTile[MapContentType.SoundRegions] = new TileIndexedList<Widgets.SoundRegionTrackItem>(trackItems.OfType<Widgets.SoundRegionTrackItem>());
            trackModel.ContentByTile[MapContentType.CarSpawners] = new TileIndexedList<Widgets.CarSpawnerTrackItem>(trackItems.OfType<Widgets.CarSpawnerTrackItem>());
            trackModel.ContentByTile[MapContentType.Empty] = new TileIndexedList<Widgets.EmptyTrackItem>(trackItems.OfType<Widgets.EmptyTrackItem>());

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            trackModel.ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem>(StationNameItem.CreateStationItems(stations));
            trackModel.ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem>(platforms.Select(p => new PlatformNameItem(p)));
            trackModel.ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem>(sidings.Select(p => new SidingNameItem(p)));
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
