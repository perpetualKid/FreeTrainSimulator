using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EditorTrainPath : TrackSegmentPathBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
    {
        private enum PathType
        {
            Invalid,
            MainPath,
            PassingPath,
        }

        private readonly List<EditorPathItem> pathPoints = new List<EditorPathItem>();

        public int SelectedNodeIndex { get; set; } = -1;

        public EditorPathItem SelectedNode => (SelectedNodeIndex >= 0 && SelectedNodeIndex < pathPoints.Count) ? pathPoints[SelectedNodeIndex] : null;

        public TrainPath TrainPathModel { get; private set; }

        private class TrainPathSection : TrackSegmentSectionBase<EditorTrainPathSegment>, IDrawable<VectorPrimitive>
        {
            public PathType PathType { get; private set; }

            public TrainPathSection(in PointD startLocation, in PointD endLocation, PathType pathType) :
                base(startLocation, endLocation)
            {
                PathType = pathType;
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, PathType pathType) :
                base(trackModel, trackNodeIndex)
            {
                PathType = pathType;
            }

            public TrainPathSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation, PathType pathType) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
            {
                PathType = pathType;
            }

            public TrainPathSection(TrackModel trackModel, int startTrackNodeIndex, in PointD startLocation, int endTrackNodeIndex, in PointD endLocation, PathType pathType) :
                base(trackModel, startTrackNodeIndex, startLocation, endTrackNodeIndex, endLocation)
            {
                PathType = pathType;
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                colorVariation = PathType switch
                {
                    PathType.PassingPath => ColorVariation.Highlight,
                    _ => ColorVariation.None,
                };
                foreach (EditorTrainPathSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, PathType == PathType.Invalid ? -scaleFactor : scaleFactor);
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

            static bool CheckPathItem(TrainPathItem pathItem, int index)
            {
                if (pathItem.Junction && pathItem.JunctionNode == null)
                {
                    Trace.TraceWarning($"Path point #{index} is marked as junction but not actually located on junction.");
                    return false;
                }
                else if (pathItem.Invalid)
                {
                    Trace.TraceWarning($"Path item #{index} is not on track.");
                    return false;
                }
                return true;
            }

            void AddSection(PathType pathType, TrainPathItem start, TrainPathItem end, int index)
            {
                if (!CheckPathItem(start, index) || !CheckPathItem(end, index))
                {
                    nodeSegment = null;
                    PathSections.Add(new TrainPathSection(start.Location, end.Location, PathType.Invalid));
                }
                else
                {
                    List<TrackSegmentBase> trackSegments = start.ConnectedSegments.IntersectBy(end.ConnectedSegments.Select(s => s.TrackNodeIndex), s => s.TrackNodeIndex).ToList();

                    TrainPathSection section = null;
                    switch (trackSegments.Count)
                    {
                        case 0:
                            nodeSegment = null;
                            //            Trace.TraceWarning($"Two junctions are not connected on single tracknode  for #{i}");
                            //            Trace.TraceWarning($"A junction could not be connected with another single tracknode  for #{i}");
                            Trace.TraceWarning($"A junction could not be connected with another single tracknode  for #{index}");
                            start.Invalid = true;
                            section = new TrainPathSection(start.Location, end.Location, PathType.Invalid);
                            break;
                        case 1:
                            nodeSegment = trackSegments[0];
                            section = new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location, pathType);
                            break;
                        default:
                            nodeSegment = trackSegments.Where(s => s.TrackNodeIndex == (start.JunctionNode ?? end.JunctionNode).MainRoute).First();
                            section = new TrainPathSection(trackModel, nodeSegment.TrackNodeIndex, start.Location, end.Location, pathType);
                            break;
                    }
                    if (start.PathNode.NodeType != PathNodeType.End)
                        PathSections.Add(section);

                    if (nodeSegment != null)
                    {
                        TrackSegmentBase otherNodeSegment = end.ConnectedSegments.Where(s => s.TrackNodeIndex == nodeSegment.TrackNodeIndex).FirstOrDefault();
                        reverseDirection = otherNodeSegment.TrackVectorSectionIndex < nodeSegment.TrackVectorSectionIndex ||
                            (otherNodeSegment.TrackVectorSectionIndex == nodeSegment.TrackVectorSectionIndex &&
                            start.Location.DistanceSquared(nodeSegment.Vector) < start.Location.DistanceSquared(nodeSegment.Location));

                        if (start.PathNode.NodeType == PathNodeType.End)
                            reverseDirection = !reverseDirection;
                    }
                }
                if (start.NextMainItem == null || start.NextMainItem == end)
                {
                    if (nodeSegment == null)
                        pathPoints.Add(new EditorPathItem(start.Location, end.Location, start.PathNode.NodeType));
                    else
                        pathPoints.Add(new EditorPathItem(start.Location, nodeSegment, start.PathNode.NodeType, reverseDirection));
                }
            }

            for (int i = 0; i < TrainPathModel.PathItems.Count; i++)
            {
                TrainPathItem pathItem = TrainPathModel.PathItems[i];

                if (pathItem.NextMainItem != null)
                {
                    AddSection(PathType.MainPath, pathItem, pathItem.NextMainItem, i);
                }
                if (pathItem.NextSidingItem != null)
                {
                    AddSection(PathType.PassingPath, pathItem, pathItem.NextSidingItem, i);
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
            throw new System.NotImplementedException();
            //return new TrainPathSection(trackModel, trackNodeIndex, start, end, PathType.Invalid);
        }

        protected override TrackSegmentSectionBase<EditorTrainPathSegment> AddSection(TrackModel trackModel, int trackNodeIndex)
        {
            throw new System.NotImplementedException();
            //return new TrainPathSection(trackModel, trackNodeIndex, PathType.Invalid);
        }
    }
}
