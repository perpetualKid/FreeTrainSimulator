using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.Track;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record SidingPath : TrackSegmentPathBase<SidingSegment>, IDrawable<VectorPrimitive>, INameValueInformationProvider
    {
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Item Type"] = "Siding" };
        private protected static int debugInfoHash;

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public virtual InformationDictionary DetailInfo
        {
            get
            {
                int hash = SidingName.GetHashCode(StringComparison.OrdinalIgnoreCase);
                if (hash != debugInfoHash)
                {
                    debugInformation["Name"] = SidingName;
                    debugInfoHash = hash;
                }
                return debugInformation;
            }
        }

        internal string SidingName { get; }

        private record SidingSection : TrackSegmentSectionBase<SidingSegment>, IDrawable<VectorPrimitive>
        {
            public SidingSection(TrackModel trackModel, int trackNodeIndex) : base(trackModel, trackNodeIndex)
            {
            }

            public SidingSection(TrackModel trackModel, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackModel, trackNodeIndex, startLocation, endLocation)
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

        public SidingPath(TrackModel trackModel, SidingTrackItem start, SidingTrackItem end) :
            base(trackModel, start.Location, start.TrackVectorNode.Index, end.Location, end.TrackVectorNode.Index)
        {
            SidingName = string.IsNullOrEmpty(start.SidingName) ? end.SidingName : start.SidingName;
            if (PathSections.Count == 0)
                Trace.TraceWarning($"Siding items {start.TrackItemId} and {end.TrackItemId} could not be linked on the underlying track database for track nodes {start.TrackVectorNode.Index} and {end.TrackVectorNode.Index}. This may indicate an error or inconsistency in the route data.");
        }

        public static List<SidingPath> CreateSidings(TrackModel trackModel, IEnumerable<SidingTrackItem> sidingItems)
        {
            List<SidingPath> result = new List<SidingPath>();
            Dictionary<int, SidingTrackItem> sidingItemMappings = sidingItems.ToDictionary(p => p.TrackItemId);
            while (sidingItemMappings.Count > 0)
            {
                int sourceId = sidingItemMappings.Keys.First();
                SidingTrackItem start = sidingItemMappings[sourceId];
                _ = sidingItemMappings.Remove(sourceId);
                if (sidingItemMappings.TryGetValue(start.LinkedId, out SidingTrackItem end))
                {
                    if (end.LinkedId != start.TrackItemId)
                        Trace.TraceWarning($"Siding Item Pair has inconsistent linking from Source Id {start.TrackItemId} to target {start.LinkedId} vs Target id {end.TrackItemId} to source {end.LinkedId}.");
                    _ = sidingItemMappings.Remove(end.TrackItemId);
                    result.Add(new SidingPath(trackModel, start, end));
                }
                else
                {
                    Trace.TraceWarning($"Linked Siding Item {start.LinkedId} for Siding Item {start.TrackItemId} not found.");
                }
            }
            return result;
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (SidingSection segmentSection in PathSections)
            {
                segmentSection.Draw(contentArea, colorVariation, scaleFactor);
            }
        }

        public override double DistanceSquared(in PointD point)
        {
            foreach (SidingSection section in PathSections)
            {
                foreach (SidingSegment segment in section.SectionSegments)
                {
                    double distanceSquared;
                    if (!double.IsNaN(distanceSquared = segment.DistanceSquared(point)))
                        return distanceSquared;
                }
            }
            return double.NaN;
        }

        protected override TrackSegmentSectionBase<SidingSegment> InitializeSection(in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }

        protected override TrackSegmentSectionBase<SidingSegment> InitializeSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new SidingSection(trackModel, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<SidingSegment> InitializeSection(TrackModel trackModel, int trackNodeIndex)
        {
            return new SidingSection(trackModel, trackNodeIndex);
        }
    }

}
