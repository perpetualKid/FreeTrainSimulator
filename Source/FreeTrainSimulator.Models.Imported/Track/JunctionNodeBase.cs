using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public abstract record JunctionNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

        public int MainRoute { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected JunctionNodeBase(Orts.Formats.Msts.Models.TrackJunctionNode junctionNode, int mainRouteIndex, IList<Orts.Formats.Msts.Models.TrackVectorNode> vectorNodes) :
            base(junctionNode?.UiD.Location ?? throw new ArgumentNullException(nameof(junctionNode)))
        {
            ArgumentNullException.ThrowIfNull(vectorNodes);

            TrackNodeIndex = junctionNode.Index;
            Direction = MathHelper.WrapAngle(GetInboundSectionDirection(vectorNodes[0], junctionNode.TrackPins[0].Direction == TrackDirection.Reverse));
            MainRoute = junctionNode.TrackPins[junctionNode.InPins + mainRouteIndex].Link;
        }

        // find the direction angle of the facing (in) track 
        protected static float GetInboundSectionDirection(Orts.Formats.Msts.Models.TrackVectorNode vectorNode, bool reverse)
        {
            if (null == vectorNode)
                return 0;

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
                return !RuntimeData.Instance.TrackModel.TrackSections.TryGetValue(vectorNode.TrackVectorSections[^1].SectionIndex, out TrackSection trackSection)
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.TrackVectorSections[^1].SectionIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.TrackVectorSections[^1].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.TrackVectorSections[^1].Direction.Y;
            }
        }

        // find the direction angle of the trailing (out) track 
        protected static float GetOutboundSectionDirection(Orts.Formats.Msts.Models.TrackVectorNode vectorNode, bool reverse, int index)
        {
            if (null == vectorNode)
                return 0;

            if (vectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {vectorNode.Index} has no TrackVectorSections attached.");
            if (vectorNode.TrackVectorSections.Length < 1 + index)
                return float.NaN;
            // find the direction angle of the trailing (out) track 
            if (reverse)
            {
                // if the attached track is reverse, we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                return !RuntimeData.Instance.TrackModel.TrackSections.TryGetValue(vectorNode.TrackVectorSections[0].SectionIndex, out TrackSection trackSection)
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

        internal IEnumerable<TrackSegmentBase> ConnectedSegments(TrackModel trackModel)
        {
            Orts.Formats.Msts.Models.TrackNode junctionNode = trackModel.RuntimeData.TrackDB.TrackNodes[TrackNodeIndex];

            foreach (Orts.Formats.Msts.Models.TrackPin pin in junctionNode.TrackPins)
            {
                TrackSegmentSection segment = trackModel.SegmentSections[pin.Link];
                yield return segment.SectionSegments[pin.Direction == TrackDirection.Reverse ? 0 : ^1];
            }
        }
    }
}
