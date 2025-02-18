using System;

using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public abstract record EndNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected EndNodeBase(TrackEndNode trackEndNode, TrackVectorNode connectedVectorNode, TrackSections trackSections) :
            base(trackEndNode?.UiD.Location ?? throw new ArgumentNullException(nameof(trackEndNode)))
        {
            ArgumentNullException.ThrowIfNull(trackSections);

            TrackNodeIndex = trackEndNode.Index;

            if (null == connectedVectorNode)
                return;
            if (connectedVectorNode.TrackPins[0].Link == trackEndNode.Index)
            {
                //find angle at beginning of vector node
                TrackVectorSection tvs = connectedVectorNode.TrackVectorSections[0];
                Direction = tvs.Direction.Y;
            }
            else
            {
                //find angle at end of vector node
                TrackVectorSection trackVectorSection = connectedVectorNode.TrackVectorSections[^1];
                Direction = trackVectorSection.Direction.Y;
                // try to get even better in case the last section is curved
                TrackSection trackSection = trackSections.TryGet(trackVectorSection.SectionIndex);
                if (null == trackSection)
                    throw new System.IO.InvalidDataException($"TrackVectorSection {trackVectorSection.SectionIndex} not found in TSection.dat");
                if (trackSection.Curved)
                {
                    Direction += MathHelper.ToRadians(trackSection.Angle);
                }
            }
            Direction -= MathHelper.PiOver2;
        }

        public bool EndNodeAt(in PointD location)
        {
            return location.DistanceSquared(Location) <= ProximityTolerance;
        }

    }
}
