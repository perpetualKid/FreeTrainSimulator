using Orts.Common.Position;

namespace Orts.Models.Track
{
    public abstract class TrainPathSegmentBase: TrackSegmentBase
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
