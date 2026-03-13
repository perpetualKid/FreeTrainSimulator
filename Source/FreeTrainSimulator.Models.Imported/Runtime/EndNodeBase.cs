using System;
using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Runtime
{
    public abstract record EndNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected EndNodeBase(EndNode trackEndNode, TrackDatabase trackDatabase) :
            base(trackEndNode?.Location ?? throw new ArgumentNullException(nameof(trackEndNode)))
        {
            ArgumentNullException.ThrowIfNull(trackDatabase);
            TrackNodeIndex = trackEndNode.NodeIndex;

            TrackNodeConnector connector = trackDatabase.TrackNodeConnectors[TrackNodeIndex][0];
            VectorNode connectedVectorNode = trackDatabase.TrackNodes[connector.Link] as VectorNode;
            if (null == connectedVectorNode)
                return;
            
            if (trackDatabase.TrackNodeConnectors[connector.Link][0].Link == TrackNodeIndex)
            {
                //find angle at beginning of vector node
                VectorSectionNode vectorSection = connectedVectorNode.VectorSections[0];
                Direction = vectorSection.Direction.Y;
            }
            else if (trackDatabase.TrackNodeConnectors[connector.Link][1].Link == TrackNodeIndex)
            {
                //find angle at end of vector node
                VectorSectionNode vectorSection = connectedVectorNode.VectorSections[^1];
                Direction = vectorSection.Direction.Y;
                // try to get even better in case the last section is curved
                if (!RuntimeData.Instance.TrackSections.TrackSections.TryGetValue(vectorSection.NodeIndex, out TrackSection trackSection))
                    throw new System.IO.InvalidDataException($"TrackVectorSection {vectorSection.NodeIndex} not found in TSection.dat");
                if (trackSection.Curved)
                {
                    Direction += MathHelper.ToRadians(trackSection.Angle);
                }
            }
            else
            {
                Trace.TraceWarning($"Inconsistent Linking between track nodes {TrackNodeIndex} and {connector.Link}");
            }
            Direction -= MathHelper.PiOver2;
        }

        public bool EndNodeAt(in PointD location)
        {
            return location.DistanceSquared(Location) <= ProximityTolerance;
        }

    }
}
