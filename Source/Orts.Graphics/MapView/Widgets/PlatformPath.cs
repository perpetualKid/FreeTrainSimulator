using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Models.Simplified.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PlatformPath : SegmentPathBase<PlatformSegment>, IDrawable<VectorPrimitive>
    {
        internal string PlatformName { get; }
        internal string StationName { get; }

        public PlatformPath(PlatformTrackItem start, PlatformTrackItem end, Dictionary<int, List<SegmentBase>> trackNodeSegments) : base(start.Location, start.TrackVectorNode.Index, end.Location, end.TrackVectorNode.Index, trackNodeSegments)
        {
            PlatformName = string.IsNullOrEmpty(start.PlatformName) ? end.PlatformName : start.PlatformName;
            StationName = string.IsNullOrEmpty(start.StationName) ? end.StationName: start.StationName;
            //Strip the station name out of platform name (only if they are not equal)
            if (PlatformName?.Length > StationName?.Length && PlatformName.StartsWith(StationName, System.StringComparison.OrdinalIgnoreCase))
                PlatformName = PlatformName[StationName.Length..];
        }

        public static IEnumerable<PlatformPath> CreatePlatforms(IEnumerable<PlatformTrackItem> platformItems, Dictionary<int, List<SegmentBase>> trackNodeSegments)
        {
            Dictionary<int, PlatformTrackItem> platformItemMappings = platformItems.ToDictionary(p => p.Id);
            while (platformItemMappings.Count > 0)
            {
                int sourceId = platformItemMappings.Keys.First();
                PlatformTrackItem start = platformItemMappings[sourceId];
                platformItemMappings.Remove(sourceId);
                if (platformItemMappings.TryGetValue(start.LinkedId, out PlatformTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Platform Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    platformItemMappings.Remove(end.Id);
                    yield return new PlatformPath(start, end, trackNodeSegments);
                }
                else
                {
                    Trace.TraceWarning($"Linked Platform Item {start.LinkedId} for Platform Item {start.Id} not found.");
                }
            }
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (PlatformSegment segment in pathSegments)
            {
                segment.Draw(contentArea, colorVariation, scaleFactor);
            }
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        protected override PlatformSegment CreateItem(in PointD start, in PointD end)
        {
            return new PlatformSegment(start, end);
        }

        protected override PlatformSegment CreateItem(SegmentBase source)
        {
            return new PlatformSegment(source);
        }

        protected override PlatformSegment CreateItem(SegmentBase source, in PointD start, in PointD end)
        {
            return new PlatformSegment(source, start, end);
        }

    }
}
