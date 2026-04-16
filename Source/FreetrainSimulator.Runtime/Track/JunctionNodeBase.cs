using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    public abstract record JunctionNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

        public int MainRoute { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected JunctionNodeBase(JunctionNode junctionNode, int mainRouteNode) :
            base(junctionNode?.Location ?? throw new ArgumentNullException(nameof(junctionNode)))
        {
            TrackNodeIndex = junctionNode.NodeIndex;

            Direction = MathHelper.WrapAngle(junctionNode.Direction.Y + MathHelper.Pi);
            MainRoute = mainRouteNode;
        }

        // find the direction angle of the facing (in) track 
        protected static float GetInboundSectionDirection(VectorNode vectorNode, bool reverse)
        {
            if (null == vectorNode)
                return 0;

            // find the direction angle of the facing (in) track 
            if (reverse)
            {
                // if the attached track is reverse, we can take just the angle
                return vectorNode.VectorSections[0].Direction.Y + MathHelper.Pi;
            }
            else
            {
                // else we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                return !RuntimeDataResolver.Instance.TrackSections.TrackSections.TryGetValue(vectorNode.VectorSections[^1].NodeIndex, out TrackSection trackSection)
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.VectorSections[^1].NodeIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.VectorSections[^1].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.VectorSections[^1].Direction.Y;
            }
        }

        // find the direction angle of the trailing (out) track 
        protected static float GetOutboundSectionDirection(VectorNode vectorNode, bool reverse, int index)
        {
            if (null == vectorNode)
                return 0;

            if (vectorNode.VectorSections.Length < 1 + index)
                return float.NaN;
            // find the direction angle of the trailing (out) track 
            if (reverse)
            {
                // if the attached track is reverse, we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                return !RuntimeDataResolver.Instance.TrackSections.TrackSections.TryGetValue(vectorNode.VectorSections[0].NodeIndex, out TrackSection trackSection)
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.VectorSections[0].NodeIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.VectorSections[index].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.VectorSections[index].Direction.Y;
            }
            else
            {
                // else we can take just the angle
                return vectorNode.VectorSections[^(1 + index)].Direction.Y + MathHelper.Pi;
            }
        }

        public bool JunctionNodeAt(in PointD location)
        {
            return location.DistanceSquared(Location) <= WorldLocation.ProximityTolerance;
        }

        internal IEnumerable<TrackSegmentBase> ConnectedSegments()
        {
            TrackWorld trackWorld = TrackWorld.Instance;
            ImmutableArray<TrackNodeConnector> connectors = trackWorld.TrackDatabase.TrackNodeConnectors[TrackNodeIndex].TrackNodeConnectors;

            foreach (TrackNodeConnector connector in connectors)
            {
                TrackSegmentSection segment = trackWorld.SegmentSections[connector.Link];
                yield return segment.SectionSegments[connector.Direction == TrackDirection.Reverse ? 0 : ^1];
            }
        }
    }
}
