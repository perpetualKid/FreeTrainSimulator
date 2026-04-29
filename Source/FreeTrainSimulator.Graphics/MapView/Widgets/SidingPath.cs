using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Runtime.Track;

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
            public SidingSection(TrackWorld trackWorld, int trackNodeIndex) : base(trackWorld, trackNodeIndex)
            {
            }

            public SidingSection(TrackWorld trackWorld, int trackNodeIndex, in PointD startLocation, in PointD endLocation) :
                base(trackWorld, trackNodeIndex, startLocation, endLocation)
            {
            }

            public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
            {
                foreach (SidingSegment segment in SectionSegments)
                {
                    segment.Draw(renderer, colorVariation, scaleFactor);
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

        public SidingPath(TrackWorld trackWorld, SidingTrackItem start, SidingTrackItem end) :
            base(trackWorld, start.Location, start.VectorNode.NodeIndex, end.Location, end.VectorNode.NodeIndex, trackWorld.TrackDatabase)
        {
            SidingName = string.IsNullOrEmpty(start.SidingName) ? end.SidingName : start.SidingName;
            if (PathSections.Length == 0)
                Trace.TraceWarning($"Siding items {start.TrackItemId} and {end.TrackItemId} could not be linked on the underlying track database for track nodes {start.VectorNode.NodeIndex} and {end.VectorNode.NodeIndex}. This may indicate an error or inconsistency in the route data.");
        }

        public static List<SidingPath> CreateSidings(TrackWorld trackWorld, IEnumerable<SidingTrackItem> sidingItems)
        {
            List<SidingPath> result = new List<SidingPath>();
            if (sidingItems is not IList<SidingTrackItem>)
                sidingItems = sidingItems.ToList();
            Dictionary<int, SidingTrackItem> sidingItemMappings = sidingItems.ToDictionary(p => p.TrackItemId);

            foreach (SidingTrackItem start in sidingItems)
            {
                if (!sidingItemMappings.TryGetValue(start.LinkedId, out SidingTrackItem end))
                {
                    Trace.TraceError($"Siding Item pair not found for Source Id {start.TrackItemId} to target {start.LinkedId}");
                }
                result.Add(new SidingPath(trackWorld, start, end));
            }
            return result;
        }

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (SidingSection segmentSection in PathSections.Cast<SidingSection>())
            {
                segmentSection.Draw(renderer, colorVariation, scaleFactor);
            }
        }

        public override double DistanceSquared(in PointD point)
        {
            foreach (SidingSection section in PathSections.Cast<SidingSection>())
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

        protected override TrackSegmentSectionBase<SidingSegment> InitializeSection(TrackWorld trackWorld, int trackNodeIndex, in PointD start, in PointD end)
        {
            return new SidingSection(trackWorld, trackNodeIndex, start, end);
        }

        protected override TrackSegmentSectionBase<SidingSegment> InitializeSection(TrackWorld trackWorld, int trackNodeIndex)
        {
            return new SidingSection(trackWorld, trackNodeIndex);
        }
    }

}
