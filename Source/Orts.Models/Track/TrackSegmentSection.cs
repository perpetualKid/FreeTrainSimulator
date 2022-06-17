using System;
using System.Collections.Generic;

using Orts.Common.Position;

namespace Orts.Models.Track
{
    public class TrackSegmentSection : TrackSegmentSectionBase<TrackSegmentBase>
    {
        public TrackSegmentSection(int trackNodeIndex, IEnumerable<TrackSegmentBase> trackSegments): 
            base(trackNodeIndex, trackSegments)
        {
        }

        protected override TrackSegmentBase CreateItem(in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }

        protected override TrackSegmentBase CreateItem(TrackSegmentBase source)
        {
            throw new NotImplementedException();
        }

        protected override TrackSegmentBase CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }
    }
}
