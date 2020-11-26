using System;
using System.Diagnostics;
using System.IO;

using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Physics;

namespace Orts.Simulation.Track
{
    /// <summary>
    /// TrackCircuit position class
    /// </summary>
    public class TrackCircuitPosition
    {
        public int TrackCircuitSectionIndex { get; internal set; }
        public TrackDirection Direction { get; internal set; }
        public float Offset { get; internal set; }
        public int RouteListIndex { get; internal set; }
        public int TrackNode { get; private set; }
        public float DistanceTravelled { get; internal set; }

        /// <summary>
        /// constructor - creates empty item
        /// </summary>
        public TrackCircuitPosition()
        {
            TrackCircuitSectionIndex = -1;
            Direction = TrackDirection.Ahead;
            Offset = 0.0f;
            RouteListIndex = -1;
            TrackNode = -1;
            DistanceTravelled = 0.0f;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public TrackCircuitPosition(TrackCircuitPosition source, bool reverse = false)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            TrackCircuitSectionIndex = source.TrackCircuitSectionIndex;
            Direction = reverse ? source.Direction.Next() : source.Direction;
            Offset = source.Offset;
            RouteListIndex = source.RouteListIndex;
            TrackNode = source.TrackNode;
            DistanceTravelled = source.DistanceTravelled;
        }

        // Restore
        public void RestorePresentPosition(BinaryReader inf, Train train)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            TrackNode tn = train.FrontTDBTraveller.TN;
            float offset = train.FrontTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)train.FrontTDBTraveller.Direction;

            TrackCircuitPosition tempPosition = new TrackCircuitPosition();
            tempPosition.SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            TrackCircuitSectionIndex = inf.ReadInt32();
            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();

            float offsetDif = Math.Abs(Offset - tempPosition.Offset);
            if (TrackCircuitSectionIndex != tempPosition.TrackCircuitSectionIndex ||
                    (TrackCircuitSectionIndex == tempPosition.TrackCircuitSectionIndex && offsetDif > 5.0f))
            {
                Trace.TraceWarning("Train {0} restored at different present position : was {1} - {3}, is {2} - {4}",
                        train.Number, TrackCircuitSectionIndex, tempPosition.TrackCircuitSectionIndex,
                        Offset, tempPosition.Offset);
            }
        }


        public void RestorePresentRear(BinaryReader inf, Train train)
        {
            if (null == train)
                throw new ArgumentNullException(nameof(train));
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            TrackNode tn = train.RearTDBTraveller.TN;
            float offset = train.RearTDBTraveller.TrackNodeOffset;
            TrackDirection direction = (TrackDirection)train.RearTDBTraveller.Direction;

            TrackCircuitPosition tempPosition = new TrackCircuitPosition();
            tempPosition.SetPosition(tn.TrackCircuitCrossReferences, offset, direction);

            TrackCircuitSectionIndex = inf.ReadInt32();
            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();

            float offsetDif = Math.Abs(Offset - tempPosition.Offset);
            if (TrackCircuitSectionIndex != tempPosition.TrackCircuitSectionIndex ||
                    (TrackCircuitSectionIndex == tempPosition.TrackCircuitSectionIndex && offsetDif > 5.0f))
            {
                Trace.TraceWarning("Train {0} restored at different present rear : was {1}-{2}, is {3}-{4}",
                        train.Number, TrackCircuitSectionIndex, tempPosition.TrackCircuitSectionIndex,
                        Offset, tempPosition.Offset);
            }
        }


        public void RestorePreviousPosition(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            TrackCircuitSectionIndex = inf.ReadInt32();
            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();
        }

        // Restore dummies for trains not yet started
        public void RestorePresentPositionDummy(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            TrackCircuitSectionIndex = inf.ReadInt32();
            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();
        }


        public void RestorePresentRearDummy(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();
        }


        public void RestorePreviousPositionDummy(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));

            TrackCircuitSectionIndex = inf.ReadInt32();
            Direction = (TrackDirection)inf.ReadInt32();
            Offset = inf.ReadSingle();
            RouteListIndex = inf.ReadInt32();
            TrackNode = inf.ReadInt32();
            DistanceTravelled = inf.ReadSingle();
        }

        // Save
        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));
            outf.Write(TrackCircuitSectionIndex);
            outf.Write((int)Direction);
            outf.Write(Offset);
            outf.Write(RouteListIndex);
            outf.Write(TrackNode);
            outf.Write(DistanceTravelled);
        }

        // Update this instance based on another instance
        // this avoid allocation such as using Copy constructor
        internal void UpdateFrom(TrackCircuitPosition source)
        {
            TrackCircuitSectionIndex = source.TrackCircuitSectionIndex;
            Direction = source.Direction;
            Offset = source.Offset;
            RouteListIndex = source.RouteListIndex;
            TrackNode = source.TrackNode;
            DistanceTravelled = source.DistanceTravelled;
        }

        /// <summary>
        /// Reverse (or continue in same direction)
        /// <\summary>

        internal void Reverse(TrackDirection oldDirection, TrackCircuitPartialPathRoute route, float offset)
        {
            if (null == route)
                throw new ArgumentNullException(nameof(route));

            RouteListIndex = route.GetRouteIndex(TrackCircuitSectionIndex, 0);
            Direction = RouteListIndex >= 0 ? route[RouteListIndex].Direction : Direction = Direction.Next();

            TrackCircuitSection section = TrackCircuitSection.TrackCircuitList[TrackCircuitSectionIndex];
            if (oldDirection != Direction)
                Offset = section.Length - Offset; // actual reversal so adjust offset

            DistanceTravelled = offset;
        }

        /// <summary>
        /// Set the position based on the trackcircuit section.
        /// </summary>
        /// <param name="trackCircuitCrossReferecenList">List of cross-references from tracknode to trackcircuitsection</param>
        /// <param name="offset">Offset along the tracknode</param>
        /// <param name="direction">direction along the tracknode (1 is forward)</param>
        internal void SetPosition(TrackCircuitCrossReferences trackCircuitCrossReferecenList, float offset, TrackDirection direction)
        {
            if (null == trackCircuitCrossReferecenList)
                throw new ArgumentNullException(nameof(trackCircuitCrossReferecenList));

            int crossRefIndex = trackCircuitCrossReferecenList.GetCrossReferenceIndex(offset, (int)direction);

            if (crossRefIndex < 0)
                return;

            TrackCircuitSectionCrossReference crossReference = trackCircuitCrossReferecenList[crossRefIndex];
            TrackCircuitSectionIndex = crossReference.Index;
            Direction = direction;
            Offset = offset - crossReference.OffsetLength[(int)direction];
        }
    }

}
