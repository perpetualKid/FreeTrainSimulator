using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

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
            await Task.Run(AddTrackSegments).ConfigureAwait(false);
            await Task.Run(AddTrackItems).ConfigureAwait(false);

            ContentArea.Initialize();
        }

        internal override void Draw(in Tile bottomLeft, in Tile topRight)
        {
            foreach (MapContentType MapViewItemSettings in drawItems) // EnumExtension.GetValues<MapViewItemSettings>())
            {
                if (viewSettings[MapViewItemSettings] && ContentByTile[MapViewItemSettings] != null)
                {
                    foreach (ITileCoordinate item in ContentByTile[MapViewItemSettings].BoundingBox(bottomLeft, topRight))
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
            GridTile nearestGridTile = ContentByTile[MapContentType.Grid].FindNearest(position, bottomLeft, topRight).First() as GridTile;
            if (nearestGridTile != nearestItems[MapContentType.Grid] as GridTile)
                nearestItems[MapContentType.Grid] = nearestGridTile;

            double distance = 400; // max 20m (sqrt(400)
            nearestDispatchItem = null;
            foreach (JunctionNode junction in ContentByTile[MapContentType.JunctionNodes][nearestGridTile.Tile].Cast<JunctionNode>())
            {
                double itemDistance = junction.Location.DistanceSquared(position);
                if (itemDistance < distance)
                {
                    nearestDispatchItem = junction;
                    distance = itemDistance;
                }
            }
            foreach (SignalTrackItem signal in ContentByTile[MapContentType.Signals][nearestGridTile.Tile].Cast<SignalTrackItem>())
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

        public void UpdateTrainPath(TrackTraveller trackTraveller)
        {
            float remainingPathLength = 2000;
            PathSegments.Clear();
            if (trackWorld == null || !trackTraveller.OnTrack)
                return;

            TrackSegmentBase sourceSegment = ResolveTrackSegment(trackTraveller.CurrentSection);

            // PathSegment startOffset is in metres for straight sections and radians for curved sections,
            // while TrackTraveller.SectionOffset is always in metres — convert curved sections here.
            double sectionOffset = trackTraveller.SectionOffset;
            if (trackWorld.SectionGeometry.TryGetValue(trackTraveller.CurrentSection, out SectionGeometry geometry) && geometry.Curved)
                sectionOffset /= geometry.Radius;

            if (sourceSegment != null)
            {
                PathSegments.Add(new PathSegment(sourceSegment, remainingPathLength, (float)sectionOffset, trackTraveller.Direction == TrackDirection.Reverse));
                remainingPathLength -= PathSegments[^1].Length;
            }

            while (remainingPathLength > 0)
            {
                if (trackTraveller.IsTrailingMisalignedSwitch(out Models.Track.JunctionNode junctionNode))
                {
                    PathSegments.Add(new BrokenPathSegment(junctionNode.Location));
                    break;
                }

                if (trackTraveller.AdvanceToNextSection() is not TrackTraveller next)
                    break;
                trackTraveller = next;

                sourceSegment = ResolveTrackSegment(trackTraveller.CurrentSection);
                if (sourceSegment != null)
                {
                    PathSegments.Add(new PathSegment(sourceSegment, remainingPathLength, 0, trackTraveller.Direction == TrackDirection.Reverse));
                    remainingPathLength -= PathSegments[^1].Length;
                }
            }
        }

        /// <summary>
        /// Resolves a <see cref="TrackSegmentBase"/> for a <see cref="Models.Track.VectorSectionNode"/>
        /// by looking up its <see cref="SectionGeometry"/> in <see cref="TrackWorld"/> and mapping back
        /// to the corresponding 2D segment via <see cref="TrackWorld.SegmentSections"/>.
        /// </summary>
        private static TrackSegmentBase ResolveTrackSegment(Models.Track.VectorSectionNode section)
        {
            if (section == null)
                return null;
            TrackWorld trackWorld = TrackWorld.Instance;
            return !trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry sectionGeometry)
                ? null
                : (trackWorld.SegmentSections[sectionGeometry.Node.NodeIndex]?.SectionSegments[sectionGeometry.SectionIndex]);
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
            Models.Track.TrackDatabase trackDatabase = RuntimeDataResolver.GameInstance(game).TrackWorld.TrackModel.TrackDatabase;

            ConcurrentBag<TrackSegment> trackSegments = new ConcurrentBag<TrackSegment>();
            ConcurrentBag<EndNode> endSegments = new ConcurrentBag<Widgets.EndNode>();
            ConcurrentBag<JunctionNode> junctionSegments = new ConcurrentBag<Widgets.JunctionNode>();
            ConcurrentBag<RoadSegment> roadSegments = new ConcurrentBag<RoadSegment>();
            ConcurrentBag<RoadEndSegment> roadEndSegments = new ConcurrentBag<RoadEndSegment>();

            if (trackDatabase != null)
            {
                Parallel.ForEach(trackDatabase.TrackNodes, trackNode =>
                {
                    switch (trackNode)
                    {
                        case Models.Track.EndNode endNode:
                            endSegments.Add(new EndNode(endNode));
                            break;
                        case Models.Track.VectorNode trackVectorNode:
                            foreach ((Models.Track.VectorSectionNode section, int index) in trackVectorNode.VectorSections.IndexedSelect())
                            {
                                trackSegments.Add(new TrackSegment(section, trackVectorNode.NodeIndex, index));
                            }
                            break;
                        case Models.Track.JunctionNode trackJunctionNode:
                            junctionSegments.Add(new ActiveJunctionSegment(trackJunctionNode, 
                                trackDatabase.TrackNodeConnectors[trackJunctionNode.NodeIndex].OutConnectors[trackJunctionNode.MainRoute].Link));
                            break;
                    }
                });
            }

            insetComponent?.SetTrackSegments(trackSegments);

            trackWorld = RuntimeDataResolver.GameInstance(game).TrackWorld;
            trackModel = TrackModel.Reset(game, RuntimeDataResolver.GameInstance(game));
            trackModel.InitializeRailTrack(trackSegments, junctionSegments, endSegments);
            trackWorld.SetSegmentSections(trackSegments.GroupBy(t => t.TrackNodeIndex).Select(g => new TrackSegmentSection(g.Key, g)));

            ContentByTile[MapContentType.Tracks] = new TileIndexedList<TrackSegmentBase>(trackSegments);
            ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNodeBase>(trackModel.Junctions);
            ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNodeBase>(trackModel.EndNodes);

            ContentByTile[MapContentType.Grid] = new TileIndexedList<GridTile>(
                ContentByTile[MapContentType.Tracks].Select(d => d.Tile).Distinct()
                .Union(ContentByTile[MapContentType.EndNodes].Select(d => d.Tile).Distinct())
                .Select(t => new GridTile(t)));

            InitializeBounds();
        }

        private void AddTrackItems()
        {
            IEnumerable<TrackItemBase> trackItems = TrackItemWidget.CreateTrackItems(
                RuntimeDataResolver.GameInstance(game).TrackWorld.TrackModel.TrackDatabase,
                trackWorld).Concat(TrackItemWidget.CreateRoadItems(RuntimeDataResolver.GameInstance(game).TrackWorld.TrackModel.RoadDatabase));

            IEnumerable<PlatformPath> platforms = PlatformPath.CreatePlatforms(trackWorld, trackItems.OfType<PlatformTrackItem>());
            ContentByTile[MapContentType.Platforms] = new TileIndexedList<PlatformPath>(platforms);

            IEnumerable<SidingPath> sidings = SidingPath.CreateSidings(trackWorld, trackItems.OfType<SidingTrackItem>());
            ContentByTile[MapContentType.Sidings] = new TileIndexedList<SidingPath>(sidings);

            ContentByTile[MapContentType.Signals] = new TileIndexedList<SignalTrackItem>(trackItems.OfType<SignalTrackItem>().Where(s => s.Normal));

            IEnumerable<IGrouping<string, PlatformPath>> stations = platforms.GroupBy(p => p.StationName, StringComparer.OrdinalIgnoreCase);
            ContentByTile[MapContentType.StationNames] = new TileIndexedList<StationNameItem>(StationNameItem.CreateStationItems(stations));
            ContentByTile[MapContentType.PlatformNames] = new TileIndexedList<PlatformNameItem>(platforms.Select(p => new PlatformNameItem(p)));
            ContentByTile[MapContentType.SidingNames] = new TileIndexedList<SidingNameItem>(sidings.Select(p => new SidingNameItem(p)));

        }
    }
}
