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
    public class TrackContent : ContentBase
    {
        #region nearest items
        private (double distance, INameValueInformationProvider statusItem) nearestSegmentForStatus;
        #endregion

        private readonly InsetComponent insetComponent;
        private readonly TrackNodeInfoProxy trackNodeInfo = new TrackNodeInfoProxy();

        //backreferences to draw whole TrackNode segment (all track vector sections) out of the current track vector section
        internal Dictionary<int, List<TrackSegment>> TrackNodeSegments { get; private set; }
        internal Dictionary<int, List<RoadSegment>> RoadTrackNodeSegments { get; private set; }

        public TrackContent(Game game) :
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
            DebugInfo["Track Segments"] = $"{contentItems[ContentItemType.Tracks].ItemCount}";
            DebugInfo["Track End Segments"] = $"{contentItems[ContentItemType.EndNodes].ItemCount}";
            DebugInfo["Junction Segments"] = $"{contentItems[ContentItemType.JunctionNodes].ItemCount}";
            DebugInfo["Road Nodes"] = $"{RoadTrackNodeSegments.Count}";
            DebugInfo["Road Segments"] = $"{contentItems[ContentItemType.Roads].ItemCount}";
            DebugInfo["Road End Segments"] = $"{contentItems[ContentItemType.RoadEndNodes].ItemCount}";
            DebugInfo["Tiles"] = $"{contentItems[ContentItemType.Grid].Count}";
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
            GridTile nearestGridTile = contentItems[ContentItemType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[ContentItemType.Grid])
            {
                nearestItems[ContentItemType.Grid] = nearestGridTile;
            }

            foreach (ContentItemType contentItemType in EnumExtension.GetValues<ContentItemType>())
            {
                double distance = double.MaxValue;
                if (contentItemType == ContentItemType.Grid)
                    //already drawn above
                    continue;
                if (contentItemsSettings[contentItemType] && contentItems[contentItemType] != null)
                {
                    foreach (ITileCoordinate<Tile> item in contentItems[contentItemType].BoundingBox(bottomLeft, topRight))
                    {
                        if (item is VectorWidget vectorWidget)
                        {
                            double itemDistance = vectorWidget.DistanceSquared(position);
                            if (itemDistance < distance)
                            {
                                nearestItems[contentItemType] = vectorWidget;
                                distance = itemDistance;
                            }
                        }
                        else if (item is PointWidget pointWidget)
                        {
                            double itemDistance = pointWidget.Location.DistanceSquared(position);
                            if (itemDistance < distance)
                            {
                                nearestItems[contentItemType] = pointWidget;
                                distance = itemDistance;
                            }
                        }
                    }
                }
                if (distance < 100)
                {
                    if (distance < 1 || distance < nearestSegmentForStatus.distance)
                        nearestSegmentForStatus = (distance, nearestItems[contentItemType] as INameValueInformationProvider);
                }
                else
                    nearestItems[contentItemType] = null;
            }
            trackNodeInfo.Source = nearestSegmentForStatus.statusItem;
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            foreach (ContentItemType contentItemType in EnumExtension.GetValues<ContentItemType>())
            {
                if (contentItemsSettings[contentItemType] && contentItems[contentItemType] != null)
                {
                    foreach (ITileCoordinate<Tile> item in contentItems[contentItemType].BoundingBox(bottomLeft, topRight))
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
            if (null != nearestItems[ContentItemType.Tracks])
            {
                foreach (TrackSegment segment in TrackNodeSegments[(nearestItems[ContentItemType.Tracks] as TrackSegment).TrackNodeIndex])
                {
                    segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                }
            }
            if (null != nearestItems[ContentItemType.Roads])
            {
                foreach (TrackSegment segment in RoadTrackNodeSegments[(nearestItems[ContentItemType.Roads] as TrackSegment).TrackNodeIndex])
                {
                    segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                }
            }

            foreach (ContentItemType contentItemType in EnumExtension.GetValues<ContentItemType>())
            {
                if (contentItemsSettings[contentItemType] && nearestItems[contentItemType] != null)
                {
                    if (nearestItems[contentItemType] is VectorWidget vectorwidget && ContentArea.InsideScreenArea(vectorwidget))
                        (vectorwidget).Draw(ContentArea, ColorVariation.Complement);
                    else if (nearestItems[contentItemType] is PointWidget pointWidget && ContentArea.InsideScreenArea(pointWidget))
                        (pointWidget).Draw(ContentArea, ColorVariation.Complement);
                }
            }

            //foreach (TrackItemBase trackItem in TrackItems.BoundingBox(bottomLeft, topRight))
            //{
            //    if (trackItem.ShouldDraw(viewSettings) && ContentArea.InsideScreenArea(trackItem))
            //        trackItem.Draw(ContentArea);
            //}
            //if (nearestTrackItem?.ShouldDraw(viewSettings) ?? false)
            //    nearestTrackItem.Draw(ContentArea, ColorVariation.Highlight);
        }

        #region build content database
        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.Instance.TrackDB;
            RoadTrackDB roadTrackDB = RuntimeData.Instance.RoadTrackDB;
            TrackSectionsFile trackSectionsFile = RuntimeData.Instance.TSectionDat;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

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

            contentItems[ContentItemType.Tracks] = new TileIndexedList<TrackSegment, Tile>(trackSegments);
            contentItems[ContentItemType.JunctionNodes] = new TileIndexedList<JunctionSegment, Tile>(junctionSegments);
            contentItems[ContentItemType.EndNodes] = new TileIndexedList<TrackEndSegment, Tile>(endSegments);
            TrackNodeSegments = trackSegments.GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.OrderBy(t => t.TrackVectorSectionIndex).ToList());

            Parallel.ForEach(roadTrackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
            {
                if (null == trackSectionsFile)
                    throw new ArgumentNullException(nameof(trackSectionsFile));

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

            contentItems[ContentItemType.Roads] = new TileIndexedList<RoadSegment, Tile>(roadSegments);
            contentItems[ContentItemType.RoadEndNodes] = new TileIndexedList<RoadEndSegment, Tile>(roadEndSegments);
            RoadTrackNodeSegments = roadSegments.GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.ToList());

            contentItems[ContentItemType.Grid] = new TileIndexedList<GridTile, Tile>(
                contentItems[ContentItemType.Tracks].Select(d => d.Tile as ITile).Distinct()
                .Union(contentItems[ContentItemType.EndNodes].Select(d => d.Tile as ITile).Distinct())
                .Union(contentItems[ContentItemType.Roads].Select(d => d.Tile as ITile).Distinct())
                .Union(contentItems[ContentItemType.RoadEndNodes].Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            if (contentItems[ContentItemType.Grid].Count == 1)
            {
                foreach (TrackEndSegment trackEndSegment in contentItems[ContentItemType.EndNodes])
                {
                    minX = Math.Min(minX, trackEndSegment.Location.X);
                    minY = Math.Min(minY, trackEndSegment.Location.Y);
                    maxX = Math.Max(maxX, trackEndSegment.Location.X);
                    maxY = Math.Max(maxY, trackEndSegment.Location.Y);
                }
            }
            else
            {
                minX = Math.Min(minX, (contentItems[ContentItemType.Grid] as TileIndexedList<GridTile, Tile>)[0][0].Tile.X);
                maxX = Math.Max(maxX, (contentItems[ContentItemType.Grid] as TileIndexedList<GridTile, Tile>)[^1][0].Tile.X);
                foreach (GridTile tile in contentItems[ContentItemType.Grid])
                {
                    minY = Math.Min(minY, tile.Tile.Z);
                    maxY = Math.Max(maxY, tile.Tile.Z);
                }
                minX = minX * WorldLocation.TileSize - WorldLocation.TileSize / 2;
                maxX = maxX * WorldLocation.TileSize + WorldLocation.TileSize / 2;
                minY = minY * WorldLocation.TileSize - WorldLocation.TileSize / 2;
                maxY = maxY * WorldLocation.TileSize + WorldLocation.TileSize / 2;
            }
            Bounds = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }

        private void AddTrackItems()
        {
            IEnumerable<TrackItemBase> trackItems = TrackItemBase.CreateTrackItems(RuntimeData.Instance.TrackDB?.TrackItems, RuntimeData.Instance.SignalConfigFile, RuntimeData.Instance.TrackDB).Concat(TrackItemBase.CreateRoadItems(RuntimeData.Instance.RoadTrackDB?.TrackItems));

            contentItems[ContentItemType.Platforms] = new TileIndexedList<PlatformPath, Tile>(PlatformPath.CreatePlatforms(trackItems.OfType<PlatformTrackItem>(), TrackNodeSegments));
            contentItems[ContentItemType.Signals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));
            contentItems[ContentItemType.OtherSignals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => !s.Normal));
            contentItems[ContentItemType.Sidings] = new TileIndexedList<SidingTrackItem, Tile>(trackItems.OfType<SidingTrackItem>());
            contentItems[ContentItemType.MilePosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => s.MilePost));
            contentItems[ContentItemType.SpeedPosts] = new TileIndexedList<SpeedPostTrackItem, Tile>(trackItems.OfType<SpeedPostTrackItem>().Where(s => !s.MilePost));
            contentItems[ContentItemType.CrossOvers] = new TileIndexedList<CrossOverTrackItem, Tile>(trackItems.OfType<CrossOverTrackItem>());
            contentItems[ContentItemType.RoadCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => s.RoadLevelCrossing));
            contentItems[ContentItemType.LevelCrossings] = new TileIndexedList<LevelCrossingTrackItem, Tile>(trackItems.OfType<LevelCrossingTrackItem>().Where(s => !s.RoadLevelCrossing));
            contentItems[ContentItemType.Hazards] = new TileIndexedList<HazardTrackItem, Tile>(trackItems.OfType<HazardTrackItem>());
            contentItems[ContentItemType.Pickups] = new TileIndexedList<PickupTrackItem, Tile>(trackItems.OfType<PickupTrackItem>());
            contentItems[ContentItemType.SoundRegions] = new TileIndexedList<SoundRegionTrackItem, Tile>(trackItems.OfType<SoundRegionTrackItem>());
            contentItems[ContentItemType.CarSpawners] = new TileIndexedList<CarSpawnerTrackItem, Tile>(trackItems.OfType<CarSpawnerTrackItem>());
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
