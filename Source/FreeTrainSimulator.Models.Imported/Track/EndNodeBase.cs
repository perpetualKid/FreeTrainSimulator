using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Track
{
    public abstract record EndNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected EndNodeBase(Orts.Formats.Msts.Models.TrackEndNode trackEndNode, Orts.Formats.Msts.Models.TrackVectorNode connectedVectorNode) :
            base(trackEndNode?.UiD.Location ?? throw new ArgumentNullException(nameof(trackEndNode)))
        {
            TrackNodeIndex = trackEndNode.Index;

            if (null == connectedVectorNode)
                return;
            if (connectedVectorNode.TrackPins[0].Link == trackEndNode.Index)
            {
                //find angle at beginning of vector node
                Orts.Formats.Msts.Models.TrackVectorSection tvs = connectedVectorNode.TrackVectorSections[0];
                Direction = tvs.Direction.Y;
            }
            else
            {
                //find angle at end of vector node
                Orts.Formats.Msts.Models.TrackVectorSection trackVectorSection = connectedVectorNode.TrackVectorSections[^1];
                Direction = trackVectorSection.Direction.Y;
                // try to get even better in case the last section is curved
                if (!RuntimeData.Instance.TrackModel.TrackSections.TryGetValue(trackVectorSection.SectionIndex, out TrackSection trackSection))
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
