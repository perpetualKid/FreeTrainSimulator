using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PlatformPath : TrackSegmentPathBase<PlatformSegment>, IDrawable<VectorPrimitive>
    {
        internal string PlatformName { get; }
        internal string StationName { get; }

        private class PlatformSection : TrackSegmentSectionBase<PlatformSegment>, IDrawable<VectorPrimitive>
        {
            public PlatformSection(int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackNodeIndex, startLocation, endLocation)
            {
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                foreach (PlatformSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, scaleFactor);
                }
            }

            protected override PlatformSegment CreateItem(in PointD start, in PointD end)
            {
                return new PlatformSegment(start, end);
            }

            protected override PlatformSegment CreateItem(TrackSegmentBase source)
            {
                return new PlatformSegment(source);
            }

            protected override PlatformSegment CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
            {
                return new PlatformSegment(source, start, end);
            }
        }

        public PlatformPath(PlatformTrackItem start, PlatformTrackItem end) : 
            base(start.Location, start.TrackVectorNode.Index, end.Location, end.TrackVectorNode.Index)
        {
            PlatformName = string.IsNullOrEmpty(start.PlatformName) ? end.PlatformName : start.PlatformName;
            StationName = string.IsNullOrEmpty(start.StationName) ? end.StationName: start.StationName;
            //Strip the station name out of platform name (only if they are not equal)
            if (PlatformName?.Length > StationName?.Length && PlatformName.StartsWith(StationName, System.StringComparison.OrdinalIgnoreCase))
                PlatformName = PlatformName[StationName.Length..];

            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                PathSections.Add(new PlatformSection(start.TrackVectorNode.Index, start.Location, end.Location));
            }
            else
            {
                // find the junction on either end
                TrackPin[] trackPins = start.TrackVectorNode.TrackPins.Intersect(end.TrackVectorNode.TrackPins, new TrackPinComparer()).ToArray();
                if (trackPins.Length == 1)
                {
                    PointD junctionLocation = PointD.FromWorldLocation((RuntimeData.Instance.TrackDB.TrackNodes[trackPins[0].Link] as TrackJunctionNode).UiD.Location);
                    PathSections.Add(new PlatformSection(start.TrackVectorNode.Index, start.Location, junctionLocation));
                    PathSections.Add(new PlatformSection(end.TrackVectorNode.Index, junctionLocation, end.Location));
                }
                else
                {
                    Trace.TraceWarning($"Platform ends are not connected by at most one Junction Node for Track Items ID {start.TrackItemId} and ID {end.TrackItemId} on Track Vector Node {start.TrackVectorNode.Index} and {end.TrackVectorNode.Index}.");
                }
            }
        }

        public static List<PlatformPath> CreatePlatforms(IEnumerable<PlatformTrackItem> platformItems)
        {
            List<PlatformPath> result = new List<PlatformPath>();
            Dictionary<int, PlatformTrackItem> platformItemMappings = platformItems.ToDictionary(p => p.Id);
            while (platformItemMappings.Count > 0)
            {
                int sourceId = platformItemMappings.Keys.First();
                PlatformTrackItem start = platformItemMappings[sourceId];
                _ = platformItemMappings.Remove(sourceId);
                if (platformItemMappings.TryGetValue(start.LinkedId, out PlatformTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Platform Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    _ = platformItemMappings.Remove(end.Id);
                    result.Add(new PlatformPath(start, end));
                }
                else
                {
                    Trace.TraceWarning($"Linked Platform Item {start.LinkedId} for Platform Item {start.Id} not found.");
                }
            }
            return result;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrackSegmentSectionBase<PlatformSegment> segmentSection in PathSections)
            {
                foreach (PlatformSegment segment in segmentSection.SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, scaleFactor);
                }
            }
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }
    }
}
