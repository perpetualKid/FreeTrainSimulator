using System.Collections.Generic;

using Orts.Common.Position;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrainPathPath : VectorPrimitive, IDrawable<VectorPrimitive>
    {
        private readonly List<TrainPath> trainPaths = new List<TrainPath>();
        private readonly List<TrainPathItem> pathPoints = new List<TrainPathItem>();

        public TrainPathPath(in WorldLocation start, in WorldLocation end): base(start, end)
        {
        }


        public void Add(TrainPath path)
        {
            trainPaths.Add(path);
        }
        public void Add(TrainPathItem pathItem)
        {
            pathPoints.Add(pathItem);
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach(TrainPath path in trainPaths)
            {
                path.Draw(contentArea, colorVariation, scaleFactor);
            }
            foreach (TrainPathItem pathItem in pathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }
        }
    }

    internal class TrainPath : TrackSegmentSectionBase<TrainPathSegment>
    {
        private protected readonly List<TrainPathItem> pathPoints = new List<TrainPathItem>();

        public TrainPath(PointD start, int startTrackNodeIndex, PointD end, int endTrackNodeIndex, IList<TrackSegmentSection> sourceElements) :
            base(start, startTrackNodeIndex, end, endTrackNodeIndex, sourceElements)
        {

        }

        //public TrainPath(PathFile pathFile, Dictionary<int, List<SegmentBase>> trackNodeSegments) :
        //    base(PointD.FromWorldLocation(pathFile.PathNodes[0].Location), PointD.FromWorldLocation(pathFile.PathNodes[^1].Location), trackNodeSegments)
        //{
        //    foreach (PathNode node in pathFile.PathNodes)
        //    {
        //        PointD nodeLocation = PointD.FromWorldLocation(node.Location);
        //        SegmentBase nodeSegment = null;
        //        foreach (List<SegmentBase> trackNodes in trackNodeSegments.Values)
        //        {
        //            foreach (SegmentBase trackSegment in trackNodes)
        //            {
        //                if (trackSegment.DistanceSquared(nodeLocation) < proximityTolerance)
        //                {
        //                    nodeSegment = trackSegment;
        //                    break;
        //                }
        //            }
        //            if (nodeSegment != null)
        //                break;
        //        }

        //        if (nodeSegment == null)
        //            return;

        //        if (node.NextMainNode > -1)
        //        {
        //            PathNode nextPoint = pathFile.PathNodes[node.NextMainNode];
        //            pathSegments.Add(new TrainPathSegment(PointD.FromWorldLocation(node.Location), PointD.FromWorldLocation(nextPoint.Location)));
        //        }

        //        pathPoints.Add(new TrainPathItem(nodeLocation, nodeSegment, node.NodeType));
        //    }
        //}

        private static void PreprocessPathNodes()
        {
        }

        public static TrainPathPath CreateTrainPath(PathFile pathFile, IList<TrackSegmentSection> trackNodeSegments)
        {
            TrainPathPath result = new TrainPathPath(pathFile.PathNodes[0].Location, pathFile.PathNodes[^1].Location);

            TrackSegmentBase NodeSegmentByLocation(in PointD nodeLocation)
            {
                foreach (TrackSegmentSection trackNodes in trackNodeSegments)
                {
                    foreach (TrackSegmentBase trackSegment in trackNodes.SectionSegments)
                    {
                        if (trackSegment.DistanceSquared(nodeLocation) <= ProximityTolerance)
                        {
                            return trackSegment;
                        }
                    }
                }

                return null;
            }

            foreach (PathNode node in pathFile.PathNodes)
            {

                // if either one is on a junction, first get the junction
                // get all the connected track nodes
                // and find the connecting track nodes
                if (node.NextMainNode > -1)
                {
                    if (node.Junction)
                    {
                        JunctionNodeBase junctionNode = TrackModel.Instance.JunctionBaseAt(PointD.FromWorldLocation(node.Location));
                        // find the junction node
                        // get all connected track nodes
                    }
                    PathNode nextPoint = pathFile.PathNodes[node.NextMainNode];

                    PointD nodeLocation = PointD.FromWorldLocation(node.Location);
                    PointD nextNodeLocation = PointD.FromWorldLocation(nextPoint.Location);

                    TrackSegmentBase nodeSegment = NodeSegmentByLocation(nodeLocation);
                    TrackSegmentBase nextNodeSegment = NodeSegmentByLocation(nextNodeLocation);

                    if (nodeSegment == null || nextNodeSegment == null)
                        return null;

                    result.Add(new TrainPath(PointD.FromWorldLocation(node.Location), nodeSegment.TrackNodeIndex, PointD.FromWorldLocation(nextPoint.Location), nextNodeSegment.TrackNodeIndex, trackNodeSegments));
                    result.Add(new TrainPathItem(nodeLocation, nodeSegment, node.NodeType));
                }
            }
            return result;
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

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainPathSegment segment in SectionSegments)
            {
                segment.Draw(contentArea, colorVariation, scaleFactor);
            }
            foreach (TrainPathItem pathItem in pathPoints)
            {
                pathItem.Draw(contentArea, colorVariation, scaleFactor);
            }

        }
    }
}
