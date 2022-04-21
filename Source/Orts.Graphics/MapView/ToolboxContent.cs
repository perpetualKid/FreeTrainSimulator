using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Widgets;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView
{
    public class ToolboxContent : ContentBase
    {
        private (double distance, INameValueInformationProvider statusItem) nearestSegmentForStatus;

        private readonly InsetComponent insetComponent;
        private readonly TrackNodeInfoProxy trackNodeInfo = new TrackNodeInfoProxy();

        //backreferences to draw whole TrackNode segment (all track vector sections) out of the current track vector section
        internal Dictionary<int, List<SegmentBase>> TrackNodeSegments { get; private set; }
        internal Dictionary<int, List<SegmentBase>> RoadTrackNodeSegments { get; private set; }

        public ToolboxContent(Game game) :
            base(game)
        {
            FormattingOptions.Add("Route Information", FormatOption.Bold);
            DebugInfo.Add("Route Information", null);
            DebugInfo["Route Name"] = RuntimeData.Instance.RouteName;
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
            TrackNodeInfo = trackNodeInfo;
        }

        public override async Task Initialize()
        {
            await Task.Run(() => AddTrackSegments()).ConfigureAwait(false);
            await Task.Run(() => AddTrackItems()).ConfigureAwait(false);

            ContentArea.Initialize();

            DebugInfo["Metric Scale"] = RuntimeData.Instance.UseMetricUnits.ToString();
            DebugInfo["Track Nodes"] = $"{TrackNodeSegments.Count}";
            DebugInfo["Track Segments"] = $"{contentItems[MapViewItemSettings.Tracks].ItemCount}";
            DebugInfo["Track End Segments"] = $"{contentItems[MapViewItemSettings.EndNodes].ItemCount}";
            DebugInfo["Junction Segments"] = $"{contentItems[MapViewItemSettings.JunctionNodes].ItemCount}";
            DebugInfo["Road Nodes"] = $"{RoadTrackNodeSegments.Count}";
            DebugInfo["Road Segments"] = $"{contentItems[MapViewItemSettings.Roads].ItemCount}";
            DebugInfo["Road End Segments"] = $"{contentItems[MapViewItemSettings.RoadEndNodes].ItemCount}";
            DebugInfo["Tiles"] = $"{contentItems[MapViewItemSettings.Grid].Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences)
        {
            if (null == colorPreferences)
                throw new ArgumentNullException(nameof(colorPreferences));

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ContentArea.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]));
            }
        }

        internal override void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight)
        {
            nearestSegmentForStatus = (float.NaN, null);
            GridTile nearestGridTile = contentItems[MapViewItemSettings.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapViewItemSettings.Grid])
            {
                nearestItems[MapViewItemSettings.Grid] = nearestGridTile;
            }

            foreach (MapViewItemSettings viewItemSettings in EnumExtension.GetValues<MapViewItemSettings>())
            {
                double distance = double.MaxValue;
                if (viewItemSettings == MapViewItemSettings.Grid)
                    //already drawn above
                    continue;
                if (viewSettings[viewItemSettings] && contentItems[viewItemSettings] != null)
                {
                    foreach (ITileCoordinate<Tile> item in contentItems[viewItemSettings].BoundingBox(bottomLeft, topRight))
                    {
                        if (item is VectorWidget vectorWidget)
                        {
                            double itemDistance = vectorWidget.DistanceSquared(position);
                            if (itemDistance < distance)
                            {
                                nearestItems[viewItemSettings] = vectorWidget;
                                distance = itemDistance;
                            }
                        }
                        else if (item is PointWidget pointWidget)
                        {
                            double itemDistance = pointWidget.Location.DistanceSquared(position);
                            if (itemDistance < distance)
                            {
                                nearestItems[viewItemSettings] = pointWidget;
                                distance = itemDistance;
                            }
                        }
                    }
                }
                if (distance < 100)
                {
                    if (distance < 1 || distance < nearestSegmentForStatus.distance)
                        nearestSegmentForStatus = (distance, nearestItems[viewItemSettings] as INameValueInformationProvider);
                }
                else
                    nearestItems[viewItemSettings] = null;
            }
            trackNodeInfo.Source = nearestSegmentForStatus.statusItem;
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            foreach (MapViewItemSettings viewItemSettings in EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[viewItemSettings] && contentItems[viewItemSettings] != null)
                {
                    foreach (ITileCoordinate<Tile> item in contentItems[viewItemSettings].BoundingBox(bottomLeft, topRight))
                    {
                        // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                        // but the performance impact/overhead seems invariant
                        if (item is VectorWidget vectorwidget && ContentArea.InsideScreenArea(vectorwidget))
                            (vectorwidget).Draw(ContentArea);
                        else if (item is PointWidget pointWidget && ContentArea.InsideScreenArea(pointWidget))
                            (pointWidget).Draw(ContentArea);
                    }
                }
            }
            if (null != nearestItems[MapViewItemSettings.Tracks])
            {
                foreach (SegmentBase segment in TrackNodeSegments[(nearestItems[MapViewItemSettings.Tracks] as SegmentBase).TrackNodeIndex])
                {
                    segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                }
            }
            if (null != nearestItems[MapViewItemSettings.Roads])
            {
                foreach (SegmentBase segment in RoadTrackNodeSegments[(nearestItems[MapViewItemSettings.Roads] as SegmentBase).TrackNodeIndex])
                {
                    segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                }
            }

            foreach (MapViewItemSettings viewItemSettings in EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[viewItemSettings] && nearestItems[viewItemSettings] != null)
                {
                    if (nearestItems[viewItemSettings] is VectorWidget vectorwidget && ContentArea.InsideScreenArea(vectorwidget))
                        (vectorwidget).Draw(ContentArea, ColorVariation.Complement);
                    else if (nearestItems[viewItemSettings] is PointWidget pointWidget && ContentArea.InsideScreenArea(pointWidget))
                        (pointWidget).Draw(ContentArea, ColorVariation.Complement);
                }
            }
        }

        #region additional content (Paths)
        public void InitializePath(PathFile path)
        {
            contentItems[MapViewItemSettings.Paths] = new TileIndexedList<TrainPath, Tile>(new List<TrainPath>() { new TrainPath(path) });
        }
        #endregion

        #region build content database
        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.Instance.TrackDB;
            RoadTrackDB roadTrackDB = RuntimeData.Instance.RoadTrackDB;
            TrackSectionsFile trackSectionsFile = RuntimeData.Instance.TSectionDat;
            if (null == trackSectionsFile)
                throw new ArgumentNullException(nameof(trackSectionsFile));

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<TrackEndSegment> endSegments = new ConcurrentBag<TrackEndSegment>();
            ConcurrentBag<JunctionSegment> junctionSegments = new ConcurrentBag<JunctionSegment>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            Parallel.ForEach(trackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
            {
                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = trackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        endSegments.Add(new TrackEndSegment(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        break;
                    case TrackVectorNode trackVectorNode:
                        int i = 0;
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            trackSegments.Add(new TrackSegment(trackVectorSection, trackSectionsFile.TrackSections, trackVectorNode.Index, i++));
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        junctionSegments.Add(new JunctionSegment(trackJunctionNode));
                        break;
                }
            });

            insetComponent?.SetTrackSegments(trackSegments);

            contentItems[MapViewItemSettings.Tracks] = new TileIndexedList<TrackSegment, Tile>(trackSegments);
            contentItems[MapViewItemSettings.JunctionNodes] = new TileIndexedList<JunctionSegment, Tile>(junctionSegments);
            contentItems[MapViewItemSettings.EndNodes] = new TileIndexedList<TrackEndSegment, Tile>(endSegments);
            TrackNodeSegments = trackSegments.Cast<SegmentBase>().GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.OrderBy(t => t.TrackVectorSectionIndex).ToList());

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

            contentItems[MapViewItemSettings.Roads] = new TileIndexedList<RoadSegment, Tile>(roadSegments);
            contentItems[MapViewItemSettings.RoadEndNodes] = new TileIndexedList<RoadEndSegment, Tile>(roadEndSegments);
            RoadTrackNodeSegments = roadSegments.Cast<SegmentBase>().GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.ToList());

            // indentify all tiles by looking at tracks and roads and their respective end segments
            contentItems[MapViewItemSettings.Grid] = new TileIndexedList<GridTile, Tile>(
                contentItems[MapViewItemSettings.Tracks].Select(d => d.Tile as ITile).Distinct()
                .Union(contentItems[MapViewItemSettings.EndNodes].Select(d => d.Tile as ITile).Distinct())
                .Union(contentItems[MapViewItemSettings.Roads].Select(d => d.Tile as ITile).Distinct())
                .Union(contentItems[MapViewItemSettings.RoadEndNodes].Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            IEnumerable<TrackItemBase> trackItems = TrackItemBase.CreateTrackItems(RuntimeData.Instance.TrackDB?.TrackItems, RuntimeData.Instance.SignalConfigFile, RuntimeData.Instance.TrackDB).Concat(TrackItemBase.CreateRoadItems(RuntimeData.Instance.RoadTrackDB?.TrackItems));

            List<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackItems.OfType<PlatformTrackItem>(), TrackNodeSegments);
            contentItems[MapViewItemSettings.Platforms] = new TileIndexedList<PlatformPath, Tile>(platforms);

            List<SidingPath> sidings = SidingPath.CreateSidings(trackItems.OfType<SidingTrackItem>(), TrackNodeSegments);
            contentItems[MapViewItemSettings.Sidings] = new TileIndexedList<SidingPath, Tile>(sidings);

            contentItems[MapViewItemSettings.Signals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));
            contentItems[MapViewItemSettings.OtherSignals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => !s.Normal));
            contentItems[MapViewItemSettings.MilePosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => s.MilePost));
            contentItems[MapViewItemSettings.SpeedPosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => !s.MilePost));
            contentItems[MapViewItemSettings.CrossOvers] = new TileIndexedList<CrossOverTrackItem, Tile>(trackItems.OfType<CrossOverTrackItem>());
            contentItems[MapViewItemSettings.RoadCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => s.RoadLevelCrossing));
            contentItems[MapViewItemSettings.LevelCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => !s.RoadLevelCrossing));
            contentItems[MapViewItemSettings.Hazards] = new TileIndexedList<HazardTrackItem, Tile>(trackItems.OfType<HazardTrackItem>());
            contentItems[MapViewItemSettings.Pickups] = new TileIndexedList<PickupTrackItem, Tile>(trackItems.OfType<PickupTrackItem>());
            contentItems[MapViewItemSettings.SoundRegions] = new TileIndexedList<SoundRegionTrackItem, Tile>(trackItems.OfType<SoundRegionTrackItem>());
            contentItems[MapViewItemSettings.CarSpawners] = new TileIndexedList<CarSpawnerTrackItem, Tile>(trackItems.OfType<CarSpawnerTrackItem>());
            contentItems[MapViewItemSettings.Empty] = new TileIndexedList<EmptyTrackItem, Tile>(trackItems.OfType<EmptyTrackItem>());

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName);
            contentItems[MapViewItemSettings.StationNames] = new TileIndexedList<StationNameItem, Tile>(StationNameItem.CreateStationItems(stations));
            contentItems[MapViewItemSettings.PlatformNames] = new TileIndexedList<PlatformNameItem, Tile>(platforms.Select(p => new PlatformNameItem(p)));
            contentItems[MapViewItemSettings.SidingNames] = new TileIndexedList<SidingNameItem, Tile>(sidings.Select(p => new SidingNameItem(p)));
        }
        #endregion

        private protected class TrackNodeInfoProxy : TrackNodeInfoProxyBase
        {
            internal INameValueInformationProvider Source;

            public override NameValueCollection DebugInfo => Source?.DebugInfo;

            public override Dictionary<string, FormatOption> FormattingOptions => Source?.FormattingOptions;
        }
    }
}
