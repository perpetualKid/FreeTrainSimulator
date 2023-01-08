using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
{
    public abstract class JunctionNodeBase : PointPrimitive, ITrackNode
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

        public int MainRoute { get; }

        protected JunctionNodeBase(TrackJunctionNode junctionNode, int mainRouteIndex, IList<TrackVectorNode> vectorNodes, TrackSections trackSections) :
            base(junctionNode?.UiD.Location ?? throw new ArgumentNullException(nameof(junctionNode)))
        {
            if (null == vectorNodes)
                throw new ArgumentNullException(nameof(vectorNodes));
            if (null == trackSections)
                throw new ArgumentNullException(nameof(trackSections));

            TrackNodeIndex = junctionNode.Index;
            Direction = MathHelper.WrapAngle(GetInboundSectionDirection(vectorNodes[0], junctionNode.TrackPins[0].Direction == TrackDirection.Reverse, trackSections));
            MainRoute = junctionNode.TrackPins[junctionNode.InPins + mainRouteIndex].Link;
        }

        // find the direction angle of the facing (in) track 
        protected static float GetInboundSectionDirection(TrackVectorNode vectorNode, bool reverse, TrackSections trackSections)
        {
            if (null == vectorNode)
                return 0;
            if (null == trackSections)
                throw new ArgumentNullException(nameof(trackSections));

            if (vectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {vectorNode.Index} has no TrackVectorSections attached.");
            // find the direction angle of the facing (in) track 
            if (reverse)
            {
                // if the attached track is reverse, we can take just the angle
                return vectorNode.TrackVectorSections[0].Direction.Y + MathHelper.Pi;
            }
            else
            {
                // else we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                TrackSection trackSection = trackSections.TryGet(vectorNode.TrackVectorSections[^1].SectionIndex);
                return null == trackSection
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.TrackVectorSections[^1].SectionIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.TrackVectorSections[^1].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.TrackVectorSections[^1].Direction.Y;
            }
        }

        // find the direction angle of the trailing (out) track 
        protected static float GetOutboundSectionDirection(TrackVectorNode vectorNode, bool reverse, TrackSections trackSections, int index)
        {
            if (null == vectorNode)
                return 0;
            if (null == trackSections)
                throw new ArgumentNullException(nameof(trackSections));

            if (vectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {vectorNode.Index} has no TrackVectorSections attached.");
            if (vectorNode.TrackVectorSections.Length < 1 + index)
                return float.NaN;
            // find the direction angle of the trailing (out) track 
            if (reverse)
            {
                // if the attached track is reverse, we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                TrackSection trackSection = trackSections.TryGet(vectorNode.TrackVectorSections[0].SectionIndex);
                return null == trackSection
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.TrackVectorSections[0].SectionIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.TrackVectorSections[index].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.TrackVectorSections[index].Direction.Y;
            }
            else
            {
                // else we can take just the angle
                return vectorNode.TrackVectorSections[^(1 + index)].Direction.Y + MathHelper.Pi;
            }
        }

        public bool JunctionNodeAt(in PointD location)
        {
            return location.DistanceSquared(Location) <= ProximityTolerance;
        }
    }
}
