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
    internal class TrainPath : TrackSegmentPathBase<TrainPathSegment>, IDrawable<VectorPrimitive>
    {
        private readonly List<TrainPathItem> pathPoints = new List<TrainPathItem>();

        private class TrainPathSection : TrackSegmentSectionBase<TrainPathSegment>, IDrawable<VectorPrimitive>
        {
            public TrainPathSection(TrackModel trackModel, int trackNodeIndex) :
                base(trackModel, trackNodeIndex)
            {
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                foreach (TrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, scaleFactor);
                }
            }

            protected override TrainPathSegment CreateItem(in PointD start, in PointD end)
            {
                return new TrainPathSegment(start, end);
            }

            protected override TrainPathSegment CreateItem(TrackSegmentBase source)
            {
                return new TrainPathSegment(source);
            }

            protected override TrainPathSegment CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
            {
                return new TrainPathSegment(source, start, end);
            }
        }

        public TrainPath(PathFile pathFile, Game game)
            : base(PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.Start).First().Location),
                  PointD.FromWorldLocation(pathFile.PathNodes.Where(n => n.NodeType == PathNodeType.End).First().Location))
        {
            RuntimeData runtimeData = RuntimeData.GameInstance(game);
            TrackModel trackModel = TrackModel.Instance<RailTrackModel>(game);
            PathNode previousNode = null;
            foreach (PathNode node in pathFile.PathNodes)
            {
                bool reverseDirection = false;
                PointD nodeLocation = PointD.FromWorldLocation(node.Location);
                TrackSegmentBase nodeSegment = trackModel.SegmentAt(nodeLocation);

                if (nodeSegment == null)
                {
                    Trace.TraceWarning($"Path node at {node.Location} not on any track section.");

                    pathPoints.Add(new TrainPathItem(nodeLocation, nodeSegment, node.NodeType, false));
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

                    JunctionNodeBase junctionNode = node.Junction ? trackModel.JunctionAt(nodeLocation) : null;
                    JunctionNodeBase nextJunctionNode = nextNode.Junction ? trackModel.JunctionAt(nextNodeLocation) : null;

                    if (node.Junction && nextNode.Junction)
                    {
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
                        nodeSegment = trackModel.SegmentsAt(nodeLocation).Where(segment => segment.TrackNodeIndex == nextNodeSegment.TrackNodeIndex).First();
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
                    TrackSegmentBase previousNodeSegment = trackModel.SegmentsAt(previousNodeLocation).Where(segment => segment.TrackNodeIndex == nodeSegment.TrackNodeIndex).First();
                    reverseDirection = nodeSegment.TrackVectorSectionIndex < previousNodeSegment.TrackVectorSectionIndex ||
                        (nodeSegment.TrackVectorSectionIndex == previousNodeSegment.TrackVectorSectionIndex &&
                        nodeSegment.DistanceSquared(previousNodeSegment.Location) < nodeLocation.DistanceSquared(previousNodeSegment.Location));
                }
                pathPoints.Add(new TrainPathItem(nodeLocation, nodeSegment, node.NodeType, reverseDirection));
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
            foreach (TrainPathItem pathItem in pathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }
        }

        protected override TrackSegmentSectionBase<TrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new TrainPathSection(trackModel, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<TrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            return new TrainPathSection(trackModel, trackNodeIndex);
        }
    }
}
