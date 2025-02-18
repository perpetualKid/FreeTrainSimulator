﻿using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public abstract record TrainPathSegmentBase : TrackSegmentBase
    {
        protected TrainPathSegmentBase(TrackSegmentBase source) : base(source)
        {
        }

        protected TrainPathSegmentBase(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
        }

        protected TrainPathSegmentBase(in PointD start, in PointD end) : base(start, end)
        {
        }
    }
}
