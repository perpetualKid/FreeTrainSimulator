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
using Orts.Models.Track;

namespace Orts.Graphics.MapView
{
    public class DispatcherContent : ContentBase
    {
        private readonly MapContentType[] drawItems = {
            MapContentType.Platforms,
            MapContentType.Sidings,
            MapContentType.Tracks,
            MapContentType.EndNodes,
            MapContentType.JunctionNodes,
            MapContentType.Signals,
            MapContentType.StationNames,
            MapContentType.PlatformNames,
            MapContentType.SidingNames};

        private readonly InsetComponent insetComponent;

        private PointPrimitive nearestDispatchItem;
        private TrainWidget nearestTrain;

        public Dictionary<int, TrainWidget> Trains { get; } = new Dictionary<int, TrainWidget>();

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
            foreach (MapContentType MapViewItemSettings in drawItems) // EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[MapViewItemSettings] && trackModel.ContentByTile[MapViewItemSettings] != null)
                {
                    foreach (ITileCoordinate<Tile> item in trackModel.ContentByTile[MapViewItemSettings].BoundingBox(bottomLeft, topRight))
                    {
                        // this could also be resolved otherwise also if rather vectorwidget & pointwidget implement InsideScreenArea() function
                        // but the performance impact/overhead seems invariant
                        if (item is VectorPrimitive vectorPrimitive && ContentArea.InsideScreenArea(vectorPrimitive))
                            (vectorPrimitive as IDrawable<VectorPrimitive>).Draw(ContentArea);
                        else if (item is PointPrimitive pointPrimitive && ContentArea.InsideScreenArea(pointPrimitive))
                            (pointPrimitive as IDrawable<PointPrimitive>).Draw(ContentArea);
                    }
                }
            }
            foreach (PathSegment segment in PathSegments)
            {
                if (ContentArea.InsideScreenArea(segment))
                    segment.Draw(ContentArea, ColorVariation.None, 1.5);
            }
            foreach (TrainWidget train in Trains.Values)
            {
                if (ContentArea.InsideScreenArea(train))
                {
                    train.Draw(ContentArea, ColorVariation.None);
                    if (viewSettings[MapContentType.TrainNames])
                        train.DrawName(ContentArea);
                }
            }
            (nearestDispatchItem as IDrawable<PointPrimitive>)?.Draw(ContentArea, ColorVariation.Highlight, 1.5);
            nearestTrain?.Draw(ContentArea, ColorVariation.Highlight, 1.5);
        }

        internal override void UpdatePointerLocation(in PointD position, ITile bottomLeft, ITile topRight)
        {
            GridTile nearestGridTile = trackModel.ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid])
            {
                nearestItems[MapContentType.Grid] = nearestGridTile;
            }

            double distance = 400; // max 20m (sqrt(400)
            nearestDispatchItem = null;
            foreach (JunctionNode junction in trackModel.ContentByTile[MapContentType.JunctionNodes][nearestGridTile.Tile])
            {
                double itemDistance = junction.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestDispatchItem = junction;
                    distance = itemDistance;
                }
            }
            foreach (SignalTrackItem signal in trackModel.ContentByTile[MapContentType.Signals][nearestGridTile.Tile])
            {
                double itemDistance = signal.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestDispatchItem = signal;
                    distance = itemDistance;
                }
            }
            distance = 2500;
            nearestTrain = null;
            foreach (TrainWidget train in Trains.Values)
            {
                double itemDistance = train.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    distance = itemDistance;
                    nearestTrain = train;
                }
            }
        }

        public void UpdateTrainTrackingPoint(in WorldLocation location)
        {
            ContentArea.SetTrackingPosition(location);
        }

        // TODO 20220311 PoC code
        public void UpdateTrainPath(Traveller trainTraveller)
        {
            float remainingPathLength = 2000;
            PathSegments.Clear();
            if (trackModel == null || trackModel.SegmentSections.Count == 0)
                return;
            Traveller traveller = new Traveller(trainTraveller);
            IReadOnlyList<TrackSegmentBase> trackSegments;
            if (traveller.TrackNodeType == TrackNodeType.Track && (trackSegments = trackModel.SegmentSections[traveller.TrackNode.Index]?.SectionSegments) != null)
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
                        if ((trackSegments = trackModel.SegmentSections[traveller.TrackNode.Index]?.SectionSegments) != null)
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
        public ITrain TrainSelected => nearestTrain?.Train;

        private void AddTrackSegments()
        {
            TrackDB trackDB = RuntimeData.GameInstance(game).TrackDB;
            TrackSectionsFile trackSectionsFile = RuntimeData.GameInstance(game).TSectionDat;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<EndNode> endSegments = new ConcurrentBag<EndNode>();
            ConcurrentBag<JunctionNode> junctionSegments = new ConcurrentBag<JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            Parallel.ForEach(trackDB?.TrackNodes ?? Enumerable.Empty<TrackNode>(), trackNode =>
            {
                if (null == trackSectionsFile)
                    throw new ArgumentNullException(nameof(trackSectionsFile));

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
                            vectorNodes.Add(trackDB.TrackNodes[pin.Link] as TrackVectorNode);
                        }
                        junctionSegments.Add(new ActiveJunctionSegment(trackJunctionNode, trackSectionsFile.TrackShapes[trackJunctionNode.ShapeIndex].MainRoute, vectorNodes, trackSectionsFile.TrackSections));
                        break;
                }
            });

            insetComponent?.SetTrackSegments(trackSegments);

            trackModel = TrackModel.Reset(game, RuntimeData.GameInstance(game));
            trackModel.InitializeRailTrack(trackSegments, junctionSegments, endSegments);

            trackModel.ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile, Tile>(
                trackModel.ContentByTile[MapContentType.Tracks].Select(d => d.Tile as ITile).Distinct()
                .Union(trackModel.ContentByTile[MapContentType.EndNodes].Select(d => d.Tile as ITile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);

            IEnumerable<TrackItemBase> trackItems = TrackItemWidget.CreateTrackItems(
                runtimeData.TrackDB?.TrackItems,
                runtimeData.SignalConfigFile,
                runtimeData.TrackDB,
                trackModel.SegmentSections).Concat(TrackItemWidget.CreateRoadItems(runtimeData.RoadTrackDB?.TrackItems));

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackModel, trackItems.OfType<PlatformTrackItem>());
            trackModel.ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath, Tile>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackModel, trackItems.OfType<SidingTrackItem>());
            trackModel.ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath, Tile>(sidings);

            trackModel.ContentByTile[MapContentType.Signals] = new TileIndexedList<SignalTrackItem, Tile>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName);
            trackModel.ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem, Tile>(StationNameItem.CreateStationItems(stations));
            trackModel.ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem, Tile>(platforms.Select(p => new PlatformNameItem(p)));
            trackModel.ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem, Tile>(sidings.Select(p => new SidingNameItem(p)));

        }
    }
}
