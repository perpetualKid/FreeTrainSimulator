using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Runtime.Track
{
    #region TrackItemBase
    public abstract record TrackItemBase : PointPrimitive, IIndexedElement
    {
        public int TrackItemIndex { get; }

        int IIndexedElement.Index => TrackItemIndex;

        protected TrackItemBase(TrackItemBase source) : base(source ?? throw new ArgumentNullException(nameof(source)))
        {
            TrackItemIndex = source.TrackItemIndex;
        }

        protected TrackItemBase(in PointD location) : base(location)
        {
        }

        protected TrackItemBase(in WorldLocation location) : base(location)
        {
        }
    }
    #endregion
}
