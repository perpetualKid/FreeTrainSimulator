﻿using System;

using Orts.Common.Position;

namespace Orts.Models.Track
{
    #region TrackItemBase
    public abstract class TrackItemBase : PointPrimitive, IIndexedElement
    {
        public int TrackItemIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackItemIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected TrackItemBase(TrackItemBase source) : base(source?.Location ?? throw new ArgumentNullException(nameof(source)))
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
