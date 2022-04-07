using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly MapViewItemSettings[] drawItems = { MapViewItemSettings.Platforms, MapViewItemSettings.Tracks, MapViewItemSettings.EndNodes, MapViewItemSettings.JunctionNodes, MapViewItemSettings.Signals};

        private readonly InsetComponent insetComponent;

        #region nearest items
        private PointWidget nearestDispatchItem;
        #endregion

        internal Dictionary<int, List<SegmentBase>> TrackNodeSegments { get; private set; }

        internal TileIndexedList<TrainCarWidget, Tile> Trains { get; private set; }

        internal List<PathSegment> PathSegments { get; } = new List<PathSegment>();

        public DispatcherContent(Game game) :
            base(game)
        {
            insetComponent = ContentArea.Game.Components.OfType<InsetComponent>().FirstOrDefault();
        }

        public override async Task Initialize()
        {
            await Task.Run(() => AddTrackSegments()).ConfigureAwait(false);
            await Task.Run(() => AddTrackItems()).ConfigureAwait(false);

            ContentArea.Initialize();
        }

        internal override void Draw(ITile bottomLeft, ITile topRight)
        {
            foreach (MapViewItemSettings MapViewItemSettings in drawItems) // EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[MapViewItemSettings] && contentItems[MapViewItemSettings] != null)
                {
                    foreach (ITileCoordinate<Tile> item in contentItems[MapViewItemSettings].BoundingBox(bottomLeft, topRight))
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
            foreach (PathSegment segment in PathSegments)
            {
                if (ContentArea.InsideScreenArea(segment))
                    segment.Draw(ContentArea, ColorVariation.None, 1.5);
            }
            if (null != Trains)
            {
                foreach (TrainCarWidget trainCar in Trains.BoundingBox(bottomLeft, topRight))
                    trainCar.Draw(ContentArea);
            }
            nearestDispatchItem?.Draw(ContentArea, ColorVariation.Highlight, 1.5);
        }

        internal override void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight)
        {
            GridTile nearestGridTile = contentItems[MapViewItemSettings.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapViewItemSettings.Grid])
            {
                nearestItems[MapViewItemSettings.Grid] = nearestGridTile;
            }

            double distance = 400; // max 20m (sqrt(400)
            nearestDispatchItem = null;
            foreach (JunctionSegment junction in contentItems[MapViewItemSettings.JunctionNodes][nearestGridTile.Tile])
            {
                double itemDistance = junction.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestDispatchItem = junction;
                    distance = itemDistance;
                }
            }
            foreach (SignalTrackItem signal in contentItems[MapViewItemSettings.Signals][nearestGridTile.Tile])
            {
                double itemDistance = signal.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestDispatchItem = signal;
                    distance = itemDistance;
                }
            }
        }

        public void UpdateTrainTrackingPoint(in WorldLocation location)
        {
            ContentArea.SetTrackingPosition(location);
        }

        public void UpdateTrainPositions(ICollection<TrainCarWidget> trainCars)
        {
            Trains = new TileIndexedList<TrainCarWidget, Tile>(trainCars);
        }

        // TODO 20220311 PoC code
        public void UpdateTrainPath(Traveller trainTraveller)
        {
            float remainingPathLength = 2000;
            PathSegments.Clear();
            if (null == TrackNodeSegments)
                return;
            Traveller traveller = new Traveller(trainTraveller);
            if (traveller.TrackNodeType == TrackNodeType.Track && TrackNodeSegments.TryGetValue(traveller.TrackNode.Index, out List<SegmentBase> trackSegments))
            {
                PathSegments.Add(new PathSegment(trackSegments[traveller.TrackVectorSectionIndex], remainingPathLength, traveller.TrackSectionOffset, traveller.Direction == Direction.Backward));
                remainingPathLength -= PathSegments[^1].Length;
            }
            while (traveller.TrackNodeType != TrackNodeType.End && remainingPathLength > 0)
            {
                traveller.NextSection();
                switch (traveller.TrackNodeType)
                {
                    case TrackNodeType.Track:
                        if (TrackNodeSegments.TryGetValue(traveller.TrackNode.Index, out trackSegments))
                        {
                            PathSegments.Add(new PathSegment(trackSegments[traveller.TrackVectorSectionIndex], remainingPathLength, 0, traveller.Direction == Direction.Backward));
                            remainingPathLength -= PathSegments[^1].Length;
                        }
                        break;
                    case TrackNodeType.Junction:
                        TrackJunctionNode junctionNode = traveller.TrackNode as TrackJunctionNode;
                        //check on trailing switches (previous pathnode is linked to an outpin) have correct selection set
                        Debug.Assert(junctionNode.InPins == 1);
                        if (junctionNode.TrackPins[0].Link != PathSegments[^1].TrackNodeIndex && junctionNode.TrackPins[junctionNode.InPins + junctionNode.SelectedRoute].Link != PathSegments[^1].TrackNodeIndex)
                        {
                            PathSegments.Add(new BrokenPathSegment(junctionNode.UiD.Location));
                            return;
                        }
                        break;
                }
            }
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

        public ISignal SignalSelected => (nearestDispatchItem as SignalTrackItem)?.Signal;
        public IJunction SwitchSelected => (nearestDispatchItem as ActiveJunctionSegment)?.Junction;

        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.Instance.TrackDB;
            TrackSectionsFile trackSectionsFile = RuntimeData.Instance.TSectionDat;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<TrackEndSegment> endSegments = new ConcurrentBag<TrackEndSegment>();
            ConcurrentBag<JunctionSegment> junctionSegments = new ConcurrentBag<JunctionSegment>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            Parallel.ForEach(trackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
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
                            vectorNodes.Add(trackDB.TrackNodes[pin.Link] as TrackVectorNode);
                        }
                        junctionSegments.Add(new ActiveJunctionSegment(trackJunctionNode, vectorNodes, trackSectionsFile.TrackSections));
                        break;
                }
            });

            insetComponent?.SetTrackSegments(trackSegments);

            contentItems[MapViewItemSettings.Tracks] = new TileIndexedList<TrackSegment, Tile>(trackSegments);
            contentItems[MapViewItemSettings.JunctionNodes] = new TileIndexedList<JunctionSegment, Tile>(junctionSegments);
            contentItems[MapViewItemSettings.EndNodes] = new TileIndexedList<TrackEndSegment, Tile>(endSegments);
            TrackNodeSegments = trackSegments.Cast<SegmentBase>().GroupBy(t => t.TrackNodeIndex).ToDictionary(i => i.Key, i => i.OrderBy(t => t.TrackVectorSectionIndex).ToList());

            contentItems[MapViewItemSettings.Grid] = new TileIndexedList<GridTile, Tile>(
                contentItems[MapViewItemSettings.Tracks].Select(d => d.Tile as ITile).Distinct()
                .Union(contentItems[MapViewItemSettings.EndNodes].Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            IEnumerable<TrackItemBase> trackItems = TrackItemBase.CreateTrackItems(RuntimeData.Instance.TrackDB?.TrackItems, RuntimeData.Instance.SignalConfigFile, RuntimeData.Instance.TrackDB).Concat(TrackItemBase.CreateRoadItems(RuntimeData.Instance.RoadTrackDB?.TrackItems));

            contentItems[MapViewItemSettings.Signals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));
            contentItems[MapViewItemSettings.Platforms] = new TileIndexedList<PlatformPath, Tile>(PlatformPath.CreatePlatforms(trackItems.OfType<PlatformTrackItem>(), TrackNodeSegments));

        }

    }
}
