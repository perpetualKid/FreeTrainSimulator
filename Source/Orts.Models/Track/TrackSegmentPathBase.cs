using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Orts.Common.Position;

namespace Orts.Models.Track
{
    /// <summary>
    /// A collection of one or more <see cref="TrackSegmentSectionBase{T}"/> forming a train's path.
    /// Unlike a single TrackSegmentSection, a TrackSegmentPath can have path points such as reversals, where a train will pass sections of a track multiple times.
    /// Also at junctions, train could take alternatve paths, following along an alternate TrackSegmentSection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class TrackSegmentPathBase<T> : VectorPrimitive where T : TrackSegmentBase
    {
        private readonly PointD midPoint;

#pragma warning disable CA1002 // Do not expose generic lists
        protected List<TrackSegmentSectionBase<T>> PathSections { get; } = new List<TrackSegmentSectionBase<T>>();
#pragma warning restore CA1002 // Do not expose generic lists
        public ref readonly PointD MidPoint => ref midPoint;

        protected TrackSegmentPathBase(in PointD start, int startTrackNodeIndex, in PointD end, int endTrackNodeIndex) :
            base(start, end)
        {
            midPoint = Location + (Vector - Location) / 2.0;
        }
    }
}
