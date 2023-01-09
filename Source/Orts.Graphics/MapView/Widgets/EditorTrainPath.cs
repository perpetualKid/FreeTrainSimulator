using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorTrainPath : TrackSegmentPathBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
    {
        private readonly List<EditorPathItem> pathPoints = new List<EditorPathItem>();

        public int SelectedNodeIndex { get; set; } = -1;

        public EditorPathItem SelectedNode => (SelectedNodeIndex >= 0 && SelectedNodeIndex < pathPoints.Count) ? pathPoints[SelectedNodeIndex] : null;

        public TrainPath TrainPathModel { get; private set; }

        private class TrainPathSection : TrackSegmentSectionBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
        {
            public TrainPathSection(TrackModel trackModel, in PointD startLocation, in PointD endLocation) :
                base(startLocation, endLocation)
            {
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex) :
                base(trackModel, trackNodeIndex)
            {
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
            }

            public TrainPathSection(TrackModel trackModel, int startTrackNodeIndex, in PointD startLocation, int endTrackNodeIndex, in PointD endLocation) :
                base(trackModel, startTrackNodeIndex, startLocation, endTrackNodeIndex, endLocation)
            {
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                foreach (EditorTrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, scaleFactor);
                }
            }

            protected override EditorTrainPathSegment CreateItem(in PointD start, in PointD end)
            {
                return new EditorTrainPathSegment(start, end);
            }

            protected override EditorTrainPathSegment CreateItem(TrackSegmentBase source)
            {
                return new EditorTrainPathSegment(source);
            }

            protected override EditorTrainPathSegment CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
            {
                return new EditorTrainPathSegment(source, start, end);
            }
        }

        public EditorTrainPath(PathFile pathFile, Game game)
            : base(PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.Start).First().Location),
                  PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.End).First().Location))
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel trackModel = TrackModel.Instance<RailTrackModel>(game);

            TrainPathModel = new TrainPath(pathFile, game);

            PathNode previousNode = null;

            foreach (PathNode node in pathFile.PathNodes)
            {
                bool reverseDirection = false;
                PointD nodeLocation = PointD.FromWorldLocation(node.Location);


                TrackSegmentBase nodeSegment = trackModel.SegmentAt(nodeLocation);

                if (nodeSegment == null)
                {
                    Trace.TraceWarning($"Path node at {node.Location} not on any track section.");
                    pathPoints.Add(new EditorPathItem(nodeLocation, nodeSegment, node.NodeType, false));
                    continue;
                }

                // if either one is on a junction, first get the junction
                // get all the connected track nodes
                // and find the connecting track nodes
                if (node.NextMainNode > -1)
                {
                    if (previousNode == null || node.NextMainNode > previousNode.NextMainNode)
                        previousNode = node;
                    // valid cases
                    // both points are on a (the same) tracksegment
                    // one node is a junction
                    // both nodes are a junction
                    // in either variant, there could be any number of trailing junctions in between
                    PathNode nextNode = pathFile.PathNodes[node.NextMainNode];
                    PointD nextNodeLocation = PointD.FromWorldLocation(nextNode.Location);

                    //PathSections.Add(new TrainPathSection(trackModel, nodeLocation, nextNodeLocation));

                    JunctionNodeBase junctionNode = node.Junction ? trackModel.JunctionAt(nodeLocation) : null;
                    JunctionNodeBase nextJunctionNode = nextNode.Junction ? trackModel.JunctionAt(nextNodeLocation) : null;

                    //if (junctionNode != null && nextJunctionNode != null)
                    //{
                    //    // check if both junctions are connected on the same tracknode.
                    //    // may be >1 (two) nodes if this is a passing path, in this case (if there's no intermediary) we use the (starting) switch's main route

                    //    List<TrackSegmentBase> trackSegments = trackModel.SegmentsAt(nodeLocation).IntersectBy(trackModel.SegmentsAt(nextNodeLocation).Select(s => s.TrackNodeIndex), (s => s.TrackNodeIndex)).ToList();

                    //    switch (trackSegments.Count)
                    //    {
                    //        case 0:
                    //            Trace.TraceWarning($"Invalid Data.");
                    //            PathSections.Add(new TrainPathSection(trackModel, nodeLocation, nextNodeLocation));
                    //            break;
                    //        case 1:
                    //            nodeSegment = trackSegments[0];
                    //            PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, nodeLocation, nextNodeLocation));
                    //            break;
                    //        default:
                    //            nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == junctionNode.MainRoute).First();
                    //            PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, nodeLocation, nextNodeLocation));
                    //            break;
                    //    }
                    //}
                    //else
                    if (node.Junction && nextNode.Junction)
                    {
                        TrackSegmentBase nextNodeSegment = trackModel.SegmentAt(nextNodeLocation);
                        var nextNodeSegments = trackModel.SegmentsAt(nextNodeLocation).ToList();
                        var nodeSegments = trackModel.SegmentsAt(nodeLocation).ToList();
                        var intersects = nodeSegments.IntersectBy(nextNodeSegments.Select(s => s.TrackNodeIndex), (s => s.TrackNodeIndex));
                        nodeSegment = intersects.Count() > 1 ? intersects.Where(s => s.TrackNodeIndex == junctionNode.MainRoute).First()
                            : intersects.First();
                        PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, nodeLocation, nextNodeLocation));
                        reverseDirection = nextNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                            (nextNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                            nextNodeLocation.DistanceSquared(nodeSegment.Location) < nodeLocation.DistanceSquared(nodeSegment.Location));

                        TrackPin[] trackPins = runtimeData.TrackDB.TrackNodes[junctionNode.TrackNodeIndex].TrackPins.
                            Intersect(runtimeData.TrackDB.TrackNodes[nextJunctionNode.TrackNodeIndex].TrackPins, TrackPinComparer.LinkOnlyComparer).ToArray();
                        if (trackPins.Length == 1)
                        {
                            int trackNodeIndex = trackModel.SegmentSections[trackPins[0].Link].TrackNodeIndex;
                            PathSections.Add(new TrainPathSection(trackModel, trackNodeIndex));
                            nodeSegment = trackModel.SegmentsAt(nodeLocation).Where(segment => segment.TrackNodeIndex == trackNodeIndex).First();
                            reverseDirection = nodeSegment.TrackVectorSectionIndex > 0 || nodeLocation.DistanceSquared(nodeSegment.Location) > ProximityTolerance;
                        }
                        else
                        {
                            Trace.TraceWarning($"Invalid Data.");
                        }
                    }
                    else if (node.Junction)
                    {
                        TrackSegmentBase nextNodeSegment = trackModel.SegmentAt(nextNodeLocation);
                        var nextNodeSegments = trackModel.SegmentsAt(nextNodeLocation).ToList();
                        var nodeSegments = trackModel.SegmentsAt(nodeLocation).ToList();
                        var intersects = nodeSegments.IntersectBy(nextNodeSegments.Select(s => s.TrackNodeIndex), (s => s.TrackNodeIndex));
                        nodeSegment = intersects.Count() > 1 ? intersects.Where(s => s.TrackNodeIndex == junctionNode.MainRoute).First()
                            : intersects.First();
                        //                        nodeSegment = trackModel.SegmentsAt(nodeLocation).Where(segment => segment.TrackNodeIndex == nextNodeSegment.TrackNodeIndex).First();
                        PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, nodeLocation, nextNodeLocation));
                        reverseDirection = nextNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                            (nextNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                            nextNodeLocation.DistanceSquared(nodeSegment.Location) < nodeLocation.DistanceSquared(nodeSegment.Location));
                    }
                    else if (nextNode.Junction)
                    {
                        PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, nodeLocation, nextNodeLocation));
                        TrackSegmentBase nextNodeSegment = trackModel.SegmentsAt(nextNodeLocation).Where(segment => segment.TrackNodeIndex == nodeSegment.TrackNodeIndex).First();
                        reverseDirection = nextNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                            (nextNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                            nextNodeLocation.DistanceSquared(nodeSegment.Location) < nodeLocation.DistanceSquared(nodeSegment.Location));
                    }
                    else
                    {
                        TrackSegmentBase nextNodeSegment = trackModel.SegmentAt(nextNodeLocation);
                        if (nodeSegment.TrackNodeIndex != nextNodeSegment.TrackNodeIndex)
                        {
                            Trace.TraceWarning($"Invalid Data.");
                        }
                        else
                        {
                            PathSections.Add(new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, PointD.FromWorldLocation(node.Location), PointD.FromWorldLocation(nextNode.Location)));
                            reverseDirection = nextNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                                (nextNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                                nextNodeLocation.DistanceSquared(nodeSegment.Location) < nodeLocation.DistanceSquared(nodeSegment.Location));
                        }
                    }
                }
                else
                {
                    PointD previousNodeLocation = PointD.FromWorldLocation(previousNode.Location);
                    TrackSegmentBase previousNodeSegment = trackModel.SegmentAt(previousNodeLocation);
                    reverseDirection = nodeSegment.TrackVectorSectionIndex < previousNodeSegment.TrackVectorSectionIndex ||
                        (nodeSegment.TrackVectorSectionIndex == previousNodeSegment.TrackVectorSectionIndex &&
                        nodeSegment.DistanceSquared(previousNodeSegment.Location) > nodeLocation.DistanceSquared(previousNodeSegment.Location));
                }
                pathPoints.Add(new EditorPathItem(nodeLocation, nodeSegment, node.NodeType, reverseDirection));
            }
            SetBounds();
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainPathSection pathSection in PathSections)
            {
                pathSection.Draw(contentArea, colorVariation, scaleFactor);
            }
            foreach (EditorPathItem pathItem in pathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }

            if (SelectedNodeIndex >= 0 && SelectedNodeIndex < pathPoints.Count)
            {
                pathPoints[SelectedNodeIndex].Draw(contentArea, ColorVariation.Highlight, 5);
            }
        }

        protected override TrackSegmentSectionBase<EditorTrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new TrainPathSection(trackModel, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<EditorTrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            return new TrainPathSection(trackModel, trackNodeIndex);
        }
    }
}
