using System;
using System.Collections.Generic;

using Orts.Common.Position;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrainPath : SegmentPathBase<TrainPathSegment>
    {
        private protected readonly List<TrainPathItem> pathPoints = new List<TrainPathItem>();

        public TrainPath(PathFile pathFile) : base(PointD.FromWorldLocation(pathFile.PathNodes[0].Location), PointD.FromWorldLocation(pathFile.PathNodes[^1].Location))
        {
            foreach(PathNode node in pathFile.PathNodes)
            {
                pathPoints.Add(new TrainPathItem(PointD.FromWorldLocation(node.Location), node.NodeType));
            }
        }

        protected override TrainPathSegment CreateItem(in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }

        protected override TrainPathSegment CreateItem(SegmentBase source)
        {
            throw new NotImplementedException();
        }

        protected override TrainPathSegment CreateItem(SegmentBase source, in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrainPathSegment segment in pathSegments)
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
