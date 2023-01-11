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

            bool reverseDirection = false;
            TrackSegmentBase nodeSegment = null;

            for (int i = 0; i < TrainPathModel.PathItems.Count; i++)
            {
                TrainPathItem pathItem = TrainPathModel.PathItems[i];
                if (pathItem.Invalid || (pathItem.PathNode.NodeType != PathNodeType.End && pathItem.NextMainItem.Invalid))
                {
                    PathSections.Add(new TrainPathSection(trackModel, pathItem.Location, pathItem.NextMainItem.Location));
                    pathPoints.Add(new EditorPathItem(pathItem.Location, pathItem.NextMainItem.Location, pathItem.PathNode.NodeType));
                    if (pathItem.Invalid && pathItem.Junction)
                    {
                        Trace.TraceWarning($"Path point #{i} is marked as junction but not actually locate on junction");
                    }
                    else if (pathItem.Invalid)
                    {
                        Trace.TraceWarning($"One of the endpoints for path item #{i} is not on track and invalid");
                    }
                }
                else
                {
                    List<TrackSegmentBase> trackSegments = pathItem.ConnectedSegments.IntersectBy(pathItem.NextMainItem.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).ToList();

                    TrainPathSection section = null;
                    switch (trackSegments.Count)
                    {
                        case 0:
                            nodeSegment = null;
                            //            Trace.TraceWarning($"Two junctions are not connected on single tracknode  for #{i}");
                            //            Trace.TraceWarning($"A junction could not be connected with another single tracknode  for #{i}");
                            Trace.TraceWarning($"A junction could not be connected with another single tracknode  for #{i}");
                            pathItem.Invalid = true;
                            section = new TrainPathSection(trackModel, pathItem.Location, pathItem.NextMainItem.Location);
                            break;
                        case 1:
                            nodeSegment = trackSegments[0];
                            section = new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, pathItem.Location, pathItem.NextMainItem.Location);
                            break;
                        default:
                            nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (pathItem.JunctionNode ?? pathItem.NextMainItem.JunctionNode).MainRoute).First();
                            section = new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, pathItem.Location, pathItem.NextMainItem.Location);
                            break;
                    }
                    if (pathItem.PathNode.NodeType != PathNodeType.End)
                        PathSections.Add(section);

                    if (nodeSegment != null)
                    {
                        TrackSegmentBase otherNodeSegment = pathItem.NextMainItem.ConnectedSegments.Where(s => s.TrackNodeIndex == nodeSegment.TrackNodeIndex).FirstOrDefault();
                        reverseDirection = otherNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                            (otherNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                            pathItem.Location.DistanceSquared(nodeSegment.Vector) < pathItem.Location.DistanceSquared(nodeSegment.Location));

                        if (pathItem.PathNode.NodeType == PathNodeType.End)
                            reverseDirection = !reverseDirection;

                    }

                    if (nodeSegment == null)
                        pathPoints.Add(new EditorPathItem(pathItem.Location, pathItem.NextMainItem.Location, pathItem.PathNode.NodeType));
                    else
                        pathPoints.Add(new EditorPathItem(pathItem.Location, nodeSegment, pathItem.PathNode.NodeType, reverseDirection));
                }
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
