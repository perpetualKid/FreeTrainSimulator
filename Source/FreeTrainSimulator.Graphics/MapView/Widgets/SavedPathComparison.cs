using System;
using System.Collections.Immutable;
using System.Threading;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    /// <summary>Read-only, independently colored saved-path geometry. Owns no editor state or GPU resources.</summary>
    public sealed class SavedPathComparison
    {
        /// <summary>Diagnostic for a saved path that cannot be drawn without choosing or repairing its route.</summary>
        public const string UnresolvedMessage = "Saved path comparison requires an unambiguous, resolved main and passing route. Open the saved path and repair or select its route, then save again.";
        /// <summary>Diagnostic for resolved topology whose selected track geometry is unavailable.</summary>
        public const string MissingGeometryMessage = "Saved path comparison could not match its selected track geometry. Validate the route and saved path before comparing again.";

        private readonly ImmutableArray<ComparisonSection> sections;

        /// <summary>Saved route-owned path identity.</summary>
        public string Id { get; }
        /// <summary>Overlay color, also used by the path-list legend.</summary>
        public Color Color { get; }

        private SavedPathComparison(string id, Color color, ImmutableArray<ComparisonSection> sections)
        {
            Id = id;
            Color = color;
            this.sections = sections;
        }

        /// <summary>Resolves once and clips only the exact vector sequence witnessed by the saved model.</summary>
        public static SavedPathComparison Create(PathModel savedPath, TrackWorld trackWorld, Color color, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(savedPath);
            ArgumentNullException.ThrowIfNull(trackWorld);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(savedPath.Id) || savedPath.PathNodes.IsDefaultOrEmpty)
                throw new InvalidOperationException(UnresolvedMessage);
            
            foreach (PathNode node in savedPath.PathNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node == null || node.NextMainNode < -1 || node.NextMainNode >= savedPath.PathNodes.Length || node.NextSidingNode < -1 || node.NextSidingNode >= savedPath.PathNodes.Length)
                    throw new InvalidOperationException(UnresolvedMessage);
            }
            
            if (trackWorld.TrackDatabase == null || trackWorld.TrackDatabase.TrackNodes.IsDefaultOrEmpty)
                throw new InvalidOperationException(MissingGeometryMessage);

            PathRouteResolution resolution = PathRouteResolver.Resolve(savedPath, trackWorld, cancellationToken);
            
            if (!resolution.IsValid || resolution.MainRoute == null || resolution.MainRoute.Spans.IsEmpty)
                throw new InvalidOperationException(UnresolvedMessage);

            ImmutableArray<ComparisonSection>.Builder sections = ImmutableArray.CreateBuilder<ComparisonSection>();
            AddRoute(resolution.MainRoute);
            
            foreach (ResolvedPathRoute route in resolution.PassingRoutes)
                AddRoute(route);
            
            return sections.Count == 0
                ? throw new InvalidOperationException(MissingGeometryMessage)
                : new SavedPathComparison(savedPath.Id, color, sections.ToImmutable());

            void AddRoute(ResolvedPathRoute route)
            {
                foreach (ResolvedPathSpan span in route.Spans)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (span.Status != PathRouteSpanStatus.Resolved || span.Candidates.IsEmpty)
                        throw new InvalidOperationException(UnresolvedMessage);
                    
                    // A resolved span's first candidate is its selected physical traversal, not a new route search.
                    ImmutableArray<int> nodes = span.Candidates[0].RouteNodeIndexes;
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int index = nodes[i];
                        
                        if (trackWorld.TrackNodeByIndex(index) is not VectorNode vector)
                            continue;
                        
                        TrackSegmentSection source = index >= 0 && index < trackWorld.SegmentSections.Length
                            ? trackWorld.SegmentSections[index] : null;
                        
                        if (source == null || source.SectionSegments.Count == 0)
                            throw new InvalidOperationException(MissingGeometryMessage);

                        PathRouteAnchor from = resolution.AuthoredNodeAnchors[span.FromNodeIndex];
                        PathRouteAnchor to = resolution.AuthoredNodeAnchors[span.ToNodeIndex];
                        bool startAtVectorStart = false;
                        bool endAtVectorStart = false;
                        int startSection = i == 0 ? from.TrackVectorSectionIndex : BoundarySection(index, nodes[i - 1], vector, out startAtVectorStart);
                        int endSection = i == nodes.Length - 1 ? to.TrackVectorSectionIndex : BoundarySection(index, nodes[i + 1], vector, out endAtVectorStart);
                        
                        if (startSection < 0 || endSection < 0 || startSection >= source.SectionSegments.Count || endSection >= source.SectionSegments.Count)
                            throw new InvalidOperationException(MissingGeometryMessage);
                        
                        PointD start = i == 0 ? PointD.FromWorldLocation(from.Location) : BoundaryPoint(source, startAtVectorStart);
                        PointD end = i == nodes.Length - 1 ? PointD.FromWorldLocation(to.Location) : BoundaryPoint(source, endAtVectorStart);
                        sections.Add(new ComparisonSection(source, startSection, endSection, start, end, cancellationToken));
                    }
                }
            }

            int BoundarySection(int vectorIndex, int adjacentIndex, VectorNode vector, out bool atVectorStart)
            {
                atVectorStart = false;
                if (vectorIndex >= trackWorld.TrackDatabase.TrackNodeConnectors.Length)
                    throw new InvalidOperationException(MissingGeometryMessage);
                TrackNodeConnectorIndex connectors = trackWorld.TrackDatabase.TrackNodeConnectors[vectorIndex];
                
                if (connectors == null || connectors.TrackNodeConnectors.IsDefaultOrEmpty || connectors.InboundCount != 1
                    || connectors.TrackNodeConnectors.Length != 2)
                    throw new InvalidOperationException(MissingGeometryMessage);
                
                int boundary = -1;
                
                for (int pin = 0; pin < connectors.TrackNodeConnectors.Length; pin++)
                {
                    if (connectors.TrackNodeConnectors[pin]?.Link != adjacentIndex)
                        continue;
                    
                    // Exact traversal pins are internal to Runtime. A repeated junction link cannot be
                    // disambiguated from public candidate node indexes; never collapse its loop to a point.
                    if (boundary >= 0)
                        throw new InvalidOperationException(MissingGeometryMessage);
                    
                    atVectorStart = pin == 0;
                    boundary = pin == 0 ? 0 : vector.VectorSections.Length - 1;
                }
                return boundary;
            }
        }

        private static PointD BoundaryPoint(TrackSegmentSection source, bool atVectorStart) => atVectorStart ? source.SectionSegments[0].Location : source.SectionSegments[^1].Vector;

        /// <summary>Draws the retained geometry without handles, hit testing, palette mutation, or allocations.</summary>
        public void Draw(IMapRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);
            float width = renderer.WorldToScreenSize(Math.Max(0.5, 2 / renderer.Scale));
            foreach (ComparisonSection section in sections)
            {
                for (int i = 0; i < section.SectionSegments.Length; i++)
                {
                    ComparisonSegment segment = section.SectionSegments[i];
                    if (segment.Curved)
                        renderer.DrawArc(width, Color, renderer.WorldToScreenCoordinates(in segment.Location), renderer.WorldToScreenSize(segment.Radius), segment.Direction, segment.Angle);
                    else
                        renderer.DrawLine(width, Color, renderer.WorldToScreenCoordinates(in segment.Location), renderer.WorldToScreenSize(segment.Length), segment.Direction);
                }
            }
        }

        private sealed class ComparisonSection
        {
            internal ImmutableArray<ComparisonSegment> SectionSegments { get; }

            internal ComparisonSection(TrackSegmentSection source, int startSection, int endSection, PointD start, PointD end, CancellationToken cancellationToken)
            {
                if (!source.SectionSegments[startSection].TrackSegmentAt(start) || !source.SectionSegments[endSection].TrackSegmentAt(end))
                    throw new InvalidOperationException(MissingGeometryMessage);
                ImmutableArray<ComparisonSegment>.Builder segments = ImmutableArray.CreateBuilder<ComparisonSegment>();
                int step = startSection <= endSection ? 1 : -1;
                
                for (int sectionIndex = startSection; ; sectionIndex += step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TrackSegmentBase segment = source.SectionSegments[sectionIndex];
                    if (segment.TrackVectorSectionIndex != sectionIndex)
                        throw new InvalidOperationException(MissingGeometryMessage);
                    if (startSection == endSection)
                        segments.Add(new ComparisonSegment(segment, start, end));
                    else if (sectionIndex == startSection)
                        segments.Add(new ComparisonSegment(segment, start, step > 0 ? segment.Vector : segment.Location));
                    else if (sectionIndex == endSection)
                        segments.Add(new ComparisonSegment(segment, step > 0 ? segment.Location : segment.Vector, end));
                    else
                        segments.Add(new ComparisonSegment(segment));
                    if (sectionIndex == endSection)
                        break;
                }
                SectionSegments = segments.ToImmutable();
            }
        }

        private sealed record ComparisonSegment : TrainPathSegmentBase
        {
            internal ComparisonSegment(TrackSegmentBase source) : base(source)
            {
            }

            internal ComparisonSegment(TrackSegmentBase source, in PointD start, in PointD end) : base(source, start, end)
            {
            }
        }
    }
}
