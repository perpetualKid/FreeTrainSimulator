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
    public class DispatcherContent : ContentBase
    {
        private TrackViewerViewSettings viewSettings = TrackViewerViewSettings.All;

        private readonly InsetComponent insetComponent;

        #region nearest items
        private GridTile nearestGridTile;
        private TrackSegment nearestTrackSegment;
        #endregion

        internal TileIndexedList<TrackSegment, Tile> TrackSegments { get; private set; }
        internal TileIndexedList<TrackEndSegment, Tile> TrackEndSegments { get; private set; }
        internal TileIndexedList<JunctionSegment, Tile> JunctionSegments { get; private set; }
        internal TileIndexedList<GridTile, Tile> Tiles { get; private set; }
        internal Dictionary<uint, List<TrackSegment>> TrackNodeSegments { get; private set; }

        internal TileIndexedList<TrainCarWidget, Tile> Trains { get; private set; }

        public DispatcherContent(Game game) :
            base(game)
        {
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
        }

        public override async Task Initialize()
        {
            await Task.Run(() => AddTrackSegments()).ConfigureAwait(false);

            ContentArea.Initialize();
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            if ((viewSettings & TrackViewerViewSettings.Tracks) == TrackViewerViewSettings.Tracks)
            {
                foreach (TrackSegment segment in TrackSegments.BoundingBox(bottomLeft, topRight))
                {
                    if (ContentArea.InsideScreenArea(segment))
                        segment.Draw(ContentArea);
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
            if (null != Trains)
            { 
                foreach(TrainCarWidget trainCar in Trains.BoundingBox(bottomLeft, topRight))
                    trainCar.Draw(ContentArea);
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
            foreach (TrackSegment trackSegment in TrackSegments[nearestGridTile.Tile])
            {
                double itemDistance = position.DistanceToLineSegmentSquared(trackSegment.Location, trackSegment.Vector);
                if (itemDistance < distance)
                {
                    nearestTrackSegment = trackSegment;
                    distance = itemDistance;
                }
            }
        }

        public void UpdateTrainPositions(ICollection<TrainCarWidget> trainCars)
        {
            Trains = new TileIndexedList<TrainCarWidget, Tile>(trainCars);
        }

        public void UpdateItemVisiblity(TrackViewerViewSettings viewSettings)
        {
            this.viewSettings = viewSettings;
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

        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.Instance.TrackDB;
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


            Tiles = new TileIndexedList<GridTile, Tile>(
                TrackSegments.Select(d => d.Tile as ITile).Distinct()
                .Union(TrackEndSegments.Select(d => d.Tile as ITile).Distinct())
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

    }
}
