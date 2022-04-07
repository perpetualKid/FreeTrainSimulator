﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class SidingPath: VectorWidget
    {
        private readonly string sidingName;
        private protected static Dictionary<int, List<SegmentBase>> sourceElements; //temporary variable to avoid passing the same element around
        private protected readonly List<SidingSegment> pathSegments = new List<SidingSegment>();
        private protected readonly PointD midPoint;

        internal SidingPath(SidingTrackItem start, SidingTrackItem end)
        {
            base.location = start.Location;
            tile = start.Tile;
            vectorEnd = end.Location;
            otherTile = end.Tile;
            sidingName = string.IsNullOrEmpty(start.SidingName) ? end.SidingName: start.SidingName;
            midPoint = base.Location + (vectorEnd - base.location) / 2.0;

            ref readonly PointD startLocation = ref start.Location;
            ref readonly PointD endLocation = ref end.Location;

            SegmentBase startSegment;
            SegmentBase endSegment;
            List<SegmentBase> segments;

            // simple case, both are on the same tracknode
            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                if (!sourceElements.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
            }
            //advanced case, most likely it's just on the junction node due to overlap
            else
            {
                //check if the this was close enough on the other tracknode, maybe just a rounding error
                if (!sourceElements.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");
                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);

                if (startSegment == null || endSegment == null)
                {
                    if (!sourceElements.TryGetValue(end.TrackVectorNode.Index, out segments))
                        throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                    (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
                }
            }
            if (startSegment == null || endSegment == null)
            {
                Trace.TraceWarning($"Can't connect siding ends for Siding '{start.SidingName}'.");
                pathSegments.Add(new SidingSegment(startLocation, endLocation));
                return;
            }

            //startSegment = CheckForSegmentOverrun(startSegment, startLocation, segments);
            //endSegment = CheckForSegmentOverrun(endSegment, endLocation, segments);
            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                pathSegments.Add(new SidingSegment(startSegment, startLocation, reverse ?  startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    pathSegments.Add(new SidingSegment(segments[i]));
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                pathSegments.Add(new SidingSegment(endSegment, reverse ? endSegment.Vector : endSegment.Location, endLocation));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                pathSegments.Add(new SidingSegment(endSegment, endLocation, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    pathSegments.Add(new SidingSegment(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                pathSegments.Add(new SidingSegment(startSegment, reverse ? startSegment.Vector : startSegment.Location, startLocation));
            }
            //on a single track vector section
            else
            {
                pathSegments.Add(new SidingSegment(startSegment, startLocation, endLocation));
            }
        }

        public static List<SidingPath> CreateSidings(IEnumerable<SidingTrackItem> sidingItems, Dictionary<int, List<SegmentBase>> trackNodeSegments)
        {
            SidingPath.sourceElements = trackNodeSegments;
            List<SidingPath> sidings = new List<SidingPath>();

            Dictionary<int, SidingTrackItem> sidingItemMappings = sidingItems.ToDictionary(p => p.Id);
            while (sidingItemMappings.Count > 0)
            {
                int sourceId = sidingItemMappings.Keys.First();
                SidingTrackItem start = sidingItemMappings[sourceId];
                sidingItemMappings.Remove(sourceId);
                if (sidingItemMappings.TryGetValue(start.LinkedId, out SidingTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Siding Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    sidingItemMappings.Remove(end.Id);
                    sidings.Add(new SidingPath(start, end));
                }
                else
                {
                    Trace.TraceWarning($"Linked Siding Item {start.LinkedId} for Siding Item {start.Id} not found.");
                }
            }
            SidingPath.sourceElements = null;
            return sidings;

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (SidingSegment segment in pathSegments)
            {
                segment.Draw(contentArea, colorVariation, scaleFactor);
            }

            Color fontColor = GetColor<SidingPath>(colorVariation);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in midPoint), fontColor, sidingName, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
        }

        private static (SegmentBase startSegment, SegmentBase endSegment) EvaluteSegments(in PointD startLocation, in PointD endLocation, List<SegmentBase> segments)
        {
            SegmentBase startSegment = null;
            SegmentBase endSegment = null;
            foreach (SegmentBase segment in segments)
            {
                //find the start vector section
                if (segment.DistanceSquared(startLocation) < 1)
                {
                    startSegment = segment;
                    if (null != endSegment)
                        break;
                }
                //find the end vector section
                if (segment.DistanceSquared(endLocation) < 1)
                {
                    endSegment = segment;
                    if (null != startSegment)
                        break;
                }
            }
            return (startSegment, endSegment);
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

    }

    internal class SidingSegment : SegmentBase
    {
        public SidingSegment(SegmentBase source) : base(source)
        {
            Size = 3;
        }

        public SidingSegment(SegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
            Size = 3;
        }

        public SidingSegment(in PointD start, in PointD end) : base(start, end)
        {
            Size = 3;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<SidingSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

        public override NameValueCollection DebugInfo => null;
    }
}
