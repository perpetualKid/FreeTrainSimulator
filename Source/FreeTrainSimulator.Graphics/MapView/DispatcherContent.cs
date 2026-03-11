using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Imported.Runtime;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Graphics.MapView
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

        internal override void Draw(in Tile bottomLeft, in Tile topRight)
        {
            foreach (MapContentType MapViewItemSettings in drawItems) // EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[MapViewItemSettings] && trackModel.ContentByTile[MapViewItemSettings] != null)
                {
                    foreach (ITileCoordinate item in trackModel.ContentByTile[MapViewItemSettings].BoundingBox(bottomLeft, topRight))
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

        internal override void UpdatePointerLocation(in PointD position, in Tile bottomLeft, in Tile topRight)
        {
            GridTile nearestGridTile = trackModel.ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid] as GridTile)
                nearestItems[MapContentType.Grid] = nearestGridTile;

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
            ArgumentNullException.ThrowIfNull(colorPreferences);

            foreach (ColorSetting setting in EnumExtension.GetValues<ColorSetting>())
            {
                ContentArea.UpdateColor(setting, ColorExtension.FromName(colorPreferences[setting]), false);
            }
        }

        public ISignal SignalSelected => (nearestDispatchItem as SignalTrackItem)?.Signal;
        public IJunction SwitchSelected => (nearestDispatchItem as ActiveJunctionSegment)?.Junction;
        public ITrain TrainSelected => nearestTrain?.Train;

        private void AddTrackSegments()
        {
            Models.Track.TrackDatabase trackDatabase = RuntimeData.GameInstance(game).TrackModel.TrackDatabase;
            Models.Track.TrackSectionsModel trackSections = RuntimeData.GameInstance(game).TrackSections;

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
                        case Models.Track.VectorNode trackVectorNode:
                            int i = 0;
                            foreach (Models.Track.VectorSectionNode trackVectorSection in trackVectorNode.VectorSections)
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

            trackModel = TrackModel.Reset(game, RuntimeData.GameInstance(game));
            trackModel.InitializeRailTrack(trackSegments, junctionSegments, endSegments);

            trackModel.ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile>(
                trackModel.ContentByTile[MapContentType.Tracks].Select(d => d.Tile).Distinct()
                .Union(trackModel.ContentByTile[MapContentType.EndNodes].Select(d => d.Tile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);

            IEnumerable<TrackItemBase> trackItems = TrackItemWidget.CreateTrackItems(
                runtimeData.TrackModel.TrackDatabase,
                runtimeData.SignalConfigFile,
                trackModel.SegmentSections).Concat(TrackItemWidget.CreateRoadItems(runtimeData.TrackModel.RoadDatabase));

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackModel, trackItems.OfType<PlatformTrackItem>());
            trackModel.ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackModel, trackItems.OfType<SidingTrackItem>());
            trackModel.ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath>(sidings);

            trackModel.ContentByTile[MapContentType.Signals] = new TileIndexedList<SignalTrackItem>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            trackModel.ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem>(StationNameItem.CreateStationItems(stations));
            trackModel.ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem>(platforms.Select(p => new PlatformNameItem(p)));
            trackModel.ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem>(sidings.Select(p => new SidingNameItem(p)));

        }
    }
}
