using Orts.Common.Position;

namespace Orts.Models.Track
{
    public abstract class TrainPathItemBase: PointPrimitive
    {
        public TrainPathPoint.InvalidReasons ValidationResult { get; set; }

        protected TrainPathItemBase(in PointD location) : base(location)
        {
        }

        internal void UpdateLocation(in PointD location)
        {
            SetLocation(location);
        }

        protected void UpdateLocation(TrackSegmentBase trackSegment, in PointD location)
        {
            SetLocation(trackSegment?.SnapToSegment(location) ?? location);
            ValidationResult = null == trackSegment ? TrainPathPoint.InvalidReasons.NotOnTrack : TrainPathPoint.InvalidReasons.None;
        }
    }
}
