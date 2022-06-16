using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class SidingPath : TrackSegmentPathBase<SidingSegment>, IDrawable<VectorPrimitive>
    {
        internal string SidingName { get; }

        private class SidingSection : TrackSegmentSectionBase<SidingSegment>, IDrawable<VectorPrimitive>
        {
            public SidingSection(int trackNodeIndex, in PointD startLocation, in PointD endLocation, IList<TrackSegmentSection> trackNodeSegments) :
                base(trackNodeIndex, startLocation, endLocation, trackNodeSegments)
            {
            }

            public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                foreach (SidingSegment segment in SectionSegments)
                {
                    segment.Draw(contentArea, colorVariation, scaleFactor);
                }
            }

            protected override SidingSegment CreateItem(in PointD start, in PointD end)
            {
                return new SidingSegment(start, end);
            }

            protected override SidingSegment CreateItem(TrackSegmentBase source)
            {
                return new SidingSegment(source);
            }

            protected override SidingSegment CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
            {
                return new SidingSegment(source, start, end);
            }
        }

        public SidingPath(SidingTrackItem start, SidingTrackItem end, IList<TrackSegmentSection> trackNodeSegments) : 
            base(start.Location, start.TrackVectorNode.Index, end.Location, end.TrackVectorNode.Index, trackNodeSegments)
        {
            SidingName = string.IsNullOrEmpty(start.SidingName) ? end.SidingName : start.SidingName;

            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                PathSections.Add(new SidingSection(start.TrackVectorNode.Index, start.Location, end.Location, trackNodeSegments));
            }
            else
            {
                // find the junction on either end
                TrackPin[] trackPins = start.TrackVectorNode.TrackPins.Intersect(end.TrackVectorNode.TrackPins, new TrackPinComparer()).ToArray();
                if (trackPins.Length == 1)
                {
                    PointD junctionLocation = PointD.FromWorldLocation((RuntimeData.Instance.TrackDB.TrackNodes[trackPins[0].Link] as TrackJunctionNode).UiD.Location);
                    PathSections.Add(new SidingSection(start.TrackVectorNode.Index, start.Location, junctionLocation, trackNodeSegments));
                    PathSections.Add(new SidingSection(end.TrackVectorNode.Index, junctionLocation, end.Location, trackNodeSegments));
                }
                else
                {
                    Trace.TraceWarning($"Siding ends are not connected by at most one Junction Node for Track Items ID {start.TrackItemId} and ID {end.TrackItemId} on Track Vector Node {start.TrackVectorNode.Index} and {end.TrackVectorNode.Index}.");
                }
            }    
        }

        public static IEnumerable<SidingPath> CreateSidings(IEnumerable<SidingTrackItem> sidingItems, IList<TrackSegmentSection> trackNodeSegments)
        {
            Dictionary<int, SidingTrackItem> sidingItemMappings = sidingItems.ToDictionary(p => p.Id);
            while (sidingItemMappings.Count > 0)
            {
                int sourceId = sidingItemMappings.Keys.First();
                SidingTrackItem start = sidingItemMappings[sourceId];
                _ = sidingItemMappings.Remove(sourceId);
                if (sidingItemMappings.TryGetValue(start.LinkedId, out SidingTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Siding Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    _ = sidingItemMappings.Remove(end.Id);
                    yield return new SidingPath(start, end, trackNodeSegments);
                }
                else
                {
                    Trace.TraceWarning($"Linked Siding Item {start.LinkedId} for Siding Item {start.Id} not found.");
                }
            }
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (TrackSegmentSectionBase<SidingSegment> segmentSection in PathSections)
            {
                foreach (SidingSegment segment in segmentSection.SectionSegments)
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
