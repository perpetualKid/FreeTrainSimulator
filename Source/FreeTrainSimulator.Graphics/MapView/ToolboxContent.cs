using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;
using FreeTrainSimulator.Common.Input;

namespace FreeTrainSimulator.Graphics.MapView
{
    public enum ToolboxContentMode
    {
        ViewRoute,
        ViewPath,
        EditPath,
    }

    public class ToolboxContent : ContentBase, IPathEditorContext, IPathEditorContextServicesAccessor, ITrackNodeInfoContext, ITrackItemInfoContext
    {
        private (double distance, INameValueInformationProvider statusItem) nearestSegmentForStatus;
        private (double distance, INameValueInformationProvider statusItem) nearestItemForStatus;

        private ToolboxContentMode contentMode;

        public PathEditorBase PathEditor { get; set; }

        public INameValueInformationProvider TrackNodeInfo { get; } = new DetailInfoProxy();

        public INameValueInformationProvider TrackItemInfo { get; } = new DetailInfoProxy();

        public ToolboxContentMode ContentMode
        {
            get => contentMode;
            set
            {
                contentMode = value;
                if (value == ToolboxContentMode.ViewPath)
                    viewSettings[MapContentType.Paths] = true;
            }
        }

        private readonly IPathEditorServices pathEditorServices;

        internal ToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapSessionComposer sessionComposer, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null) :
            base(game, mouseInputGameComponent, sessionComposer, insetHost, textureHelperHost)
        {
            pathEditorServices = new PathEditorServices(game);
            FormattingOptions.Add("Route Information", FormatOption.Bold);
            DetailInfo.Add("Route Information", null);
            DetailInfo["Route Name"] = RuntimeDataResolver.GameInstance(game).RouteData.Name;
        }

        public override async Task Initialize()
        {
            await Task.Run(AddTrackSegments).ConfigureAwait(true);
            await Task.Run(AddTrackItems).ConfigureAwait(true);

            ShellServices.Initialize();
            //just put an empty list so the draw method does not skip the paths
            ContentByTile[MapContentType.Paths] = new TileIndexedList<EditorTrainPath>(new List<EditorTrainPath>() { });

            DetailInfo["Metric Scale"] = RuntimeDataResolver.GameInstance(game).MetricUnits.ToString();
            DetailInfo["Track Nodes"] = $"{trackWorld.SegmentSections.Length}";
            DetailInfo["Track Segments"] = $"{ContentByTile[MapContentType.Tracks].ItemCount}";
            DetailInfo["Track End Segments"] = $"{ContentByTile[MapContentType.EndNodes].ItemCount}";
            DetailInfo["Junction Segments"] = $"{ContentByTile[MapContentType.JunctionNodes].ItemCount}";
            DetailInfo["Road Nodes"] = $"{trackWorld.RoadSegmentSections.Length}";
            DetailInfo["Road Segments"] = $"{ContentByTile[MapContentType.Roads].ItemCount}";
            DetailInfo["Road End Segments"] = $"{ContentByTile[MapContentType.RoadEndNodes].ItemCount}";
            DetailInfo["Tiles"] = $"{ContentByTile[MapContentType.Grid].Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences, bool fontOutlining, bool limitTrackWidth)
        {
            ArgumentNullException.ThrowIfNull(colorPreferences);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ShellServices.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]), fontOutlining);
            }
            ShellServices.UpdateTrackWidthSettings(limitTrackWidth);
        }

        IPathEditorServices IPathEditorContextServicesAccessor.Services => pathEditorServices;

        IMapRenderer IPathEditorContext.Renderer => Renderer;

        IMapViewport IPathEditorContext.Viewport => Viewport;

        ToolboxContentMode IPathEditorContext.ContentMode
        {
            get => ContentMode;
            set => ContentMode = value;
        }

        PathEditorBase IPathEditorContext.PathEditor
        {
            get => PathEditor;
            set => PathEditor = value;
        }

        IMapViewport ITrackNodeInfoContext.Viewport => Viewport;

        IMapHostControl ITrackNodeInfoContext.HostControl => HostControl;

        ToolboxContent ITrackNodeInfoContext.Content => this;

        INameValueInformationProvider ITrackItemInfoContext.TrackItemInfo => TrackItemInfo;

        IMapViewport ITrackItemInfoContext.Viewport => Viewport;

        internal override void UpdatePointerLocation(in PointD position, in Tile bottomLeft, in Tile topRight)
        {
            nearestSegmentForStatus = (float.MaxValue, null);
            nearestItemForStatus = (float.MaxValue, null);
            GridTile nearestGridTile = ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid] as GridTile)
                nearestItems[MapContentType.Grid] = nearestGridTile;

            foreach (MapContentType viewItem in EnumExtension.GetValues<MapContentType>())
            {
                double distanceSquared = double.MaxValue;
                if (viewItem == MapContentType.Grid)
                    //already checked above
                    continue;
                if (viewSettings[viewItem] && ContentByTile[viewItem] != null)
                {
                    foreach (ITileCoordinate item in ContentByTile[viewItem].BoundingBox(bottomLeft, topRight))
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
                ShellServices.RequestRedraw();
            }
        }

        internal override void Draw(in Tile bottomLeft, in Tile topRight)
        {
            foreach (MapContentType viewItemSetting in EnumExtension.GetValues<MapContentType>())
            {
                if (viewSettings[viewItemSetting] && ContentByTile[viewItemSetting] != null)
                {
                    if (viewItemSetting == MapContentType.Paths)
                        PathEditor?.Draw();
                    else
                    {
                        foreach (ITileCoordinate item in ContentByTile[viewItemSetting].BoundingBox(bottomLeft, topRight))
                        {
                            // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                            // but the performance impact/overhead seems invariant
                            if (item is VectorPrimitive vectorPrimitive && Viewport.InsideScreenArea(vectorPrimitive))
                                (item as IDrawable<VectorPrimitive>).Draw(Renderer);
                            else if (item is PointPrimitive pointPrimitive && Viewport.InsideScreenArea(pointPrimitive))
                            {
                                (item as IDrawable<PointPrimitive>).Draw(Renderer);
                            }
                        }
                    }
                }
            }
            if (ContentMode == ToolboxContentMode.ViewRoute || !viewSettings[MapContentType.Paths])
            {
                if (null != nearestItems[MapContentType.Tracks])
                {
                    foreach (TrackSegmentBase segment in trackWorld.SegmentSections[(nearestItems[MapContentType.Tracks] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.ComplementHighlight);
                    }
                }
                if (null != nearestItems[MapContentType.Roads])
                {
                    foreach (TrackSegmentBase segment in trackWorld.RoadSegmentSections[(nearestItems[MapContentType.Roads] as TrackSegmentBase).TrackNodeIndex].SectionSegments)
                    {
                        (segment as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.ComplementHighlight);
                    }
                }

                foreach (MapContentType viewItemSettings in EnumExtension.GetValues<MapContentType>())
                {
                    if (viewSettings[viewItemSettings] && nearestItems[viewItemSettings] != null)
                    {
                        if (nearestItems[viewItemSettings] is VectorPrimitive vectorPrimitive && Viewport.InsideScreenArea(vectorPrimitive))
                            (vectorPrimitive as IDrawable<VectorPrimitive>).Draw(Renderer, ColorVariation.Complement);
                        else if (nearestItems[viewItemSettings] is PointPrimitive pointPrimitive && Viewport.InsideScreenArea(pointPrimitive))
                            (pointPrimitive as IDrawable<PointPrimitive>).Draw(Renderer, ColorVariation.Complement);
                    }
                }
            }
        }

        #region build content database
        private void AddTrackSegments()
        {
            RuntimeDataResolver runtimeData = RuntimeDataResolver.GameInstance(game);
            TrackDatabase trackDatabase = runtimeData.TrackWorld.TrackDatabase;
            TrackDatabase roadDatabase = runtimeData.TrackWorld.RoadDatabase;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<Widgets.EndNode> endSegments = new ConcurrentBag<Widgets.EndNode>();
            ConcurrentBag<Widgets.JunctionNode> junctionSegments = new ConcurrentBag<Widgets.JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            if (trackDatabase != null)
            {
                Parallel.ForEach(trackDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode endNode:
                            endSegments.Add(new Widgets.EndNode(endNode));
                            break;
                        case VectorNode trackVectorNode:
                            foreach ((VectorSectionNode section, int index) in trackVectorNode.VectorSections.IndexedSelect())
                            {
                                trackSegments.Add(new TrackSegment(section, trackVectorNode.NodeIndex, index));
                            }
                            break;
                        case Models.Track.JunctionNode trackJunctionNode:
                            junctionSegments.Add(new Widgets.JunctionNode(trackJunctionNode,
                                trackDatabase.TrackNodeConnectors[trackJunctionNode.NodeIndex].OutConnectors[trackJunctionNode.MainRoute].Link));
                            break;
                    }
                });
            }

            InsetHost?.SetTrackSegments(trackSegments);

            ContentByTile[MapContentType.Tracks] = new TileIndexedList<TrackSegmentBase>(trackSegments);
            ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNodeBase>(junctionSegments);
            ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNodeBase>(endSegments);

            if (roadDatabase != null)
            {
                Parallel.ForEach(roadDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode trackEndNode:
                            roadEndSegments.Add(new Widgets.RoadEndSegment(trackEndNode));
                            break;
                        case VectorNode trackVectorNode:
                            foreach ((VectorSectionNode section, int index) in trackVectorNode.VectorSections.IndexedSelect())
                            {
                                roadSegments.Add(new RoadSegment(section, trackVectorNode.NodeIndex, index));
                            }
                            break;
                    }
                });
            }

            ContentByTile[MapContentType.Roads] = new TileIndexedList<TrackSegmentBase>(roadSegments);
            ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNodeBase>(roadEndSegments);

            trackWorld = runtimeData.TrackWorld;
            trackWorld.SetSegmentSections(trackSegments.GroupBy(t => t.TrackNodeIndex).Select(group => new TrackSegmentSection(group.Key, group)));
            trackWorld.SetRoadSegmentSections(roadSegments.GroupBy(t => t.TrackNodeIndex).Select(group => new TrackSegmentSection(group.Key, group)));
            trackWorld.SetJunctions(junctionSegments);

            // identify all tiles
            ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile>(
                ContentByTile[MapContentType.Tracks].Select(d => d.Tile).Distinct()
                .Union(ContentByTile[MapContentType.EndNodes].Select(d => d.Tile).Distinct())
                .Union(ContentByTile[MapContentType.Roads].Select(d => d.Tile).Distinct())
                .Union(ContentByTile[MapContentType.RoadEndNodes].Select(d => d.Tile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            // Materialized once to avoid repeated enumeration of the concatenated sequence (CA1851).
            List<TrackItemWidget> trackItems = TrackItemWidget.CreateTrackItems(
                trackWorld.TrackDatabase,
                trackWorld).Concat(TrackItemWidget.CreateRoadItems(trackWorld.RoadDatabase)).ToList();

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackWorld, trackItems.OfType<Widgets.PlatformTrackItem>());
            ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackWorld, trackItems.OfType<Widgets.SidingTrackItem>());
            ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath>(sidings);

            ContentByTile[MapContentType.Signals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => s.Normal));
            ContentByTile[MapContentType.OtherSignals] = new TileIndexedList<Widgets.SignalTrackItem>(trackItems.OfType<Widgets.SignalTrackItem>().Where(s => !s.Normal));

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem>(StationNameItem.CreateStationItems(stations));
            ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem>(platforms.Select(p => new PlatformNameItem(p)));
            ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem>(sidings.Select(p => new SidingNameItem(p)));
            ContentByTile[MapContentType.LevelCrossings] = new TileIndexedList<Widgets.LevelCrossingTrackItem>(trackItems.OfType<Widgets.LevelCrossingTrackItem>());
            ContentByTile[MapContentType.SpeedPosts] = new TileIndexedList<SpeedPostTrackItem>(trackItems.OfType<SpeedPostTrackItem>());
            ContentByTile[MapContentType.MilePosts] = new TileIndexedList<MilePostTrackItem>(trackItems.OfType<MilePostTrackItem>());
            ContentByTile[MapContentType.Hazards] = new TileIndexedList<Widgets.HazardTrackItem>(trackItems.OfType<Widgets.HazardTrackItem>());
            ContentByTile[MapContentType.Pickups] = new TileIndexedList<Widgets.PickupTrackItem>(trackItems.OfType<Widgets.PickupTrackItem>());
            ContentByTile[MapContentType.SoundRegions] = new TileIndexedList<Widgets.SoundRegionTrackItem>(trackItems.OfType<Widgets.SoundRegionTrackItem>());
            ContentByTile[MapContentType.CarSpawners] = new TileIndexedList<Widgets.CarSpawnerTrackItem>(trackItems.OfType<Widgets.CarSpawnerTrackItem>());
            ContentByTile[MapContentType.RoadCrossings] = new TileIndexedList<CrossOverTrackItem>(trackItems.OfType<CrossOverTrackItem>());
        }

        private sealed class DetailInfoProxy : DetailInfoProxyBase
        {
            public INameValueInformationProvider Source { get; set; }

            public override InformationDictionary DetailInfo => Source?.DetailInfo;

            public override Dictionary<string, FormatOption> FormattingOptions => Source?.FormattingOptions;
        }
        #endregion
    }
}
