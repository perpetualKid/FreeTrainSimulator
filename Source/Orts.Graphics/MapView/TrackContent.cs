using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Widgets;
using Orts.Graphics.Xna;

namespace Orts.Graphics.MapView
{
    public class TrackContent: ContentBase
    {
        #region nearest items
        private GridTile nearestGridTile;
        private TrackItemBase nearestTrackItem;
        private TrackSegment nearestTrackSegment;
        private RoadSegment nearesRoadSegment;
        #endregion

        private readonly InsetComponent insetComponent;

        internal TileIndexedList<TrackSegment, Tile> TrackSegments { get; private set; }
        internal TileIndexedList<TrackEndSegment, Tile> TrackEndSegments { get; private set; }
        internal TileIndexedList<JunctionSegment, Tile> JunctionSegments { get; private set; }
        internal TileIndexedList<TrackItemBase, Tile> TrackItems { get; private set; }
        internal TileIndexedList<GridTile, Tile> Tiles { get; private set; }
        internal TileIndexedList<RoadSegment, Tile> RoadSegments { get; private set; }
        internal TileIndexedList<RoadEndSegment, Tile> RoadEndSegments { get; private set; }
        internal Dictionary<int, List<TrackSegment>> TrackNodeSegments { get; private set; }
        internal Dictionary<int, List<TrackSegment>> RoadTrackNodeSegments { get; private set; }

        public TrackContent(Game game) :
            base(game)
        {
            DebugInfo["Route Name"] = RuntimeData.Instance.RouteName;
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
        }

        public override async Task Initialize()
        {
            await Task.Run(() => AddTrackSegments()).ConfigureAwait(false);
            await Task.Run(() => AddTrackItems()).ConfigureAwait(false);

            ContentArea.Initialize();

            DebugInfo["Metric Scale"] = RuntimeData.Instance.UseMetricUnits.ToString();
            DebugInfo["Track Nodes"] = $"{TrackNodeSegments.Count}";
            DebugInfo["Track Segments"] = $"{TrackSegments.ItemCount}";
            DebugInfo["Track End Segments"] = $"{TrackEndSegments.ItemCount}";
            DebugInfo["Junction Segments"] = $"{JunctionSegments.ItemCount}";
            DebugInfo["Track Items"] = $"{TrackItems.ItemCount}";
            DebugInfo["Road Nodes"] = $"{RoadTrackNodeSegments.Count}";
            DebugInfo["Road Segments"] = $"{RoadSegments.ItemCount}";
            DebugInfo["Road End Segments"] = $"{RoadEndSegments.ItemCount}";
            DebugInfo["Tiles"] = $"{Tiles.Count}";
        }

        public void UpdateWidgetColorSettings(EnumArray<string, ColorSetting> colorPreferences)
        {
            if (null == colorPreferences)
                throw new ArgumentNullException(nameof(colorPreferences));

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]));
            }
        }

        public void UpdateColor(ColorSetting setting, Color color)
        {
            switch (setting)
            {
                case ColorSetting.Background:
                    insetComponent?.UpdateColor(color);
                    break;
                case ColorSetting.RailTrack:
                    PointWidget.UpdateColor<TrackSegment>(color);
                    break;
                case ColorSetting.RailTrackEnd:
                    PointWidget.UpdateColor<TrackEndSegment>(color);
                    break;
                case ColorSetting.RailTrackJunction:
                    PointWidget.UpdateColor<JunctionSegment>(color);
                    break;
                case ColorSetting.RailTrackCrossing:
                    PointWidget.UpdateColor<CrossOverTrackItem>(color);
                    break;
                case ColorSetting.RailLevelCrossing:
                    PointWidget.UpdateColor<LevelCrossingTrackItem>(color);
                    break;
                case ColorSetting.RoadTrack:
                    PointWidget.UpdateColor<RoadSegment>(color);
                    break;
                case ColorSetting.RoadTrackEnd:
                    PointWidget.UpdateColor<RoadEndSegment>(color);
                    break;
                case ColorSetting.PlatformItem:
                    PointWidget.UpdateColor<PlatformTrackItem>(color);
                    break;
                case ColorSetting.SidingItem:
                    PointWidget.UpdateColor<SidingTrackItem>(color);
                    break;
                case ColorSetting.SpeedPostItem:
                    PointWidget.UpdateColor<SpeedPostTrackItem>(color);
                    break;
            }
        }

        internal override void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight)
        {
            IEnumerable<ITileCoordinate<Tile>> result = Tiles.FindNearest(position, bottomLeft, topRight);
            if (result.First() != nearestGridTile)
            {
                nearestGridTile = result.First() as GridTile;
            }
            double distance = double.MaxValue;
            foreach (TrackItemBase trackItem in TrackItems[nearestGridTile.Tile])
            {
                double itemDistance = trackItem.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestTrackItem = trackItem;
                    distance = itemDistance;
                }
            }
            distance = double.MaxValue;
            foreach (TrackSegment trackSegment in TrackSegments[nearestGridTile.Tile])
            {
                double itemDistance = position.DistanceToLineSegmentSquared(trackSegment.Location, trackSegment.Vector);
                if (itemDistance < distance)
                {
                    nearestTrackSegment = trackSegment;
                    distance = itemDistance;
                }
            }
            distance = double.MaxValue;
            foreach (RoadSegment trackSegment in RoadSegments[nearestGridTile.Tile])
            {
                double itemDistance = position.DistanceToLineSegmentSquared(trackSegment.Location, trackSegment.Vector);
                if (itemDistance < distance)
                {
                    nearesRoadSegment = trackSegment;
                    distance = itemDistance;
                }
            }
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            if ((viewSettings & TrackViewerViewSettings.Grid) == TrackViewerViewSettings.Grid)
            {
                foreach (GridTile tile in Tiles.BoundingBox(bottomLeft, topRight))
                {
                    tile.Draw(ContentArea);
                }
                nearestGridTile?.Draw(ContentArea, ColorVariation.Complement);
            }
            if ((viewSettings & TrackViewerViewSettings.Tracks) == TrackViewerViewSettings.Tracks)
            {
                foreach (TrackSegment segment in TrackSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(segment))
                        segment.Draw(ContentArea);
                }
                if (nearestTrackSegment != null)
                {
                    foreach (TrackSegment segment in TrackNodeSegments[nearestTrackSegment.TrackNodeIndex])
                    {
                        segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                    }
                    nearestTrackSegment.Draw(ContentArea, ColorVariation.Complement);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.EndsNodes) == TrackViewerViewSettings.EndsNodes)
            {
                foreach (TrackEndSegment endNode in TrackEndSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(endNode))
                        endNode.Draw(ContentArea);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.JunctionNodes) == TrackViewerViewSettings.JunctionNodes)
            {
                foreach (JunctionSegment junctionNode in JunctionSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(junctionNode))
                        junctionNode.Draw(ContentArea);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.Roads) == TrackViewerViewSettings.Roads)
            {
                foreach (RoadSegment segment in RoadSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(segment))
                        segment.Draw(ContentArea);
                }
                if (nearesRoadSegment != null)
                {
                    foreach (RoadSegment segment in RoadTrackNodeSegments[nearesRoadSegment.TrackNodeIndex])
                    {
                        segment.Draw(ContentArea, ColorVariation.ComplementHighlight);
                    }
                    nearesRoadSegment.Draw(ContentArea, ColorVariation.Complement);
                }
            }
            if ((viewSettings & TrackViewerViewSettings.RoadEndNodes) == TrackViewerViewSettings.RoadEndNodes)
            {
                foreach (RoadEndSegment endNode in RoadEndSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(endNode))
                        endNode.Draw(ContentArea);
                }
            }
            foreach (TrackItemBase trackItem in TrackItems.BoundingBox(bottomLeft, topRight))
            {
                if (trackItem.ShouldDraw(viewSettings) && ContentArea.InsideScreenArea(trackItem))
                    trackItem.Draw(ContentArea);
            }
            if (nearestTrackItem?.ShouldDraw(viewSettings) ?? false)
                nearestTrackItem.Draw(ContentArea, ColorVariation.Highlight);
        }

        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.Instance.TrackDB;
            RoadTrackDB roadTrackDB = RuntimeData.Instance.RoadTrackDB;
            TrackSectionsFile trackSectionsFile = RuntimeData.Instance.TSectionDat;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            List<TrackSegment> trackSegments = new List<TrackSegment>();
            List<TrackEndSegment> endSegments = new List<TrackEndSegment>();
            List<JunctionSegment> junctionSegments = new List<JunctionSegment>();
            List<TrackSegment> roadSegments = new List<TrackSegment>();
            List<TrackEndSegment> roadEndSegments = new List<TrackEndSegment>();
            foreach (TrackNode trackNode in trackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>())
            {
                if (null == trackSectionsFile)
                    throw new ArgumentNullException(nameof(trackSectionsFile));

                switch (trackNode)
                {
                    case TrackEndNode trackEndNode:
                        TrackVectorNode connectedVectorNode = trackDB.TrackNodes[trackEndNode.TrackPins[0].Link] as TrackVectorNode;
                        endSegments.Add(new TrackEndSegment(trackEndNode, connectedVectorNode, trackSectionsFile.TrackSections));
                        break;
                    case TrackVectorNode trackVectorNode:
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            TrackSection trackSection = trackSectionsFile.TrackSections.TryGet(trackVectorSection.SectionIndex);
                            if (trackSection != null)
                                trackSegments.Add(new TrackSegment(trackVectorSection, trackSection, trackVectorNode.Index));
                        }
                        break;
                    case TrackJunctionNode trackJunctionNode:
                        foreach (TrackPin pin in trackJunctionNode.TrackPins)
                        {
                            if (trackDB.TrackNodes[pin.Link] is TrackVectorNode vectorNode && vectorNode.TrackVectorSections.Length > 0)
                            {
                                TrackVectorSection item = pin.Direction == Common.TrackDirection.Reverse ? vectorNode.TrackVectorSections.First() : vectorNode.TrackVectorSections.Last();
                            }
                        }
                        junctionSegments.Add(new JunctionSegment(trackJunctionNode));
                        break;
                }
            }

            insetComponent?.SetTrackSegments(trackSegments);

            TrackSegments = new TileIndexedList<TrackSegment, Tile>(trackSegments);
            JunctionSegments = new TileIndexedList<JunctionSegment, Tile>(junctionSegments);
            TrackEndSegments = new TileIndexedList<TrackEndSegment, Tile>(endSegments);
            TrackNodeSegments = trackSegments.GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.ToList());

            foreach (TrackNode trackNode in roadTrackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>())
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
                        foreach (TrackVectorSection trackVectorSection in trackVectorNode.TrackVectorSections)
                        {
                            TrackSection trackSection = trackSectionsFile.TrackSections.TryGet(trackVectorSection.SectionIndex);
                            if (trackSection != null)
                                roadSegments.Add(new RoadSegment(trackVectorSection, trackSection, trackVectorNode.Index));
                        }
                        break;
                }
            }

            RoadSegments = new TileIndexedList<RoadSegment, Tile>(roadSegments);
            RoadEndSegments = new TileIndexedList<RoadEndSegment, Tile>(roadEndSegments);
            RoadTrackNodeSegments = roadSegments.GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.ToList());

            Tiles = new TileIndexedList<GridTile, Tile>(
                TrackSegments.Select(d => d.Tile as ITile).Distinct()
                .Union(TrackEndSegments.Select(d => d.Tile as ITile).Distinct())
                .Union(RoadSegments.Select(d => d.Tile as ITile).Distinct())
                .Union(RoadEndSegments.Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            if (Tiles.Count == 1)
            {
                foreach (TrackEndSegment trackEndSegment in TrackEndSegments)
                {
                    minX = Math.Min(minX, trackEndSegment.Location.X);
                    minY = Math.Min(minY, trackEndSegment.Location.Y);
                    maxX = Math.Max(maxX, trackEndSegment.Location.X);
                    maxY = Math.Max(maxY, trackEndSegment.Location.Y);
                }
            }
            else
            {
                minX = Math.Min(minX, Tiles[0][0].Tile.X);
                maxX = Math.Max(maxX, Tiles[^1][0].Tile.X);
                foreach (GridTile tile in Tiles)
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
            TrackItems = new TileIndexedList<TrackItemBase, Tile>(TrackItemBase.Create(RuntimeData.Instance.TrackDB?.TrackItems, RuntimeData.Instance.SignalConfigFile, RuntimeData.Instance.TrackDB).Concat(TrackItemBase.Create(RuntimeData.Instance.RoadTrackDB?.TrackItems)));
        }

    }
}
