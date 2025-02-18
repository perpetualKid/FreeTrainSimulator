﻿using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public record TrackSegmentSection : TrackSegmentSectionBase<TrackSegmentBase>
    {
        public TrackSegmentSection(int trackNodeIndex, IEnumerable<TrackSegmentBase> trackSegments) :
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
