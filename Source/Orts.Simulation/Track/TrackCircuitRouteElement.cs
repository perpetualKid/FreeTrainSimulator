using System;
using System.IO;

using FreeTrainSimulator.Common;

using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    /// Track Circuit Route Element
    /// </summary>
    public class TrackCircuitRouteElement
    {
        internal class AlternativePath
        {
            //Index of Alternative Path
            public int PathIndex;
            //related TrackCircuitCrossReferences Index
            public TrackCircuitSection TrackCircuitSection;
        }

        public TrackCircuitSection TrackCircuitSection { get; private set; }
        public TrackDirection Direction { get; internal set; }
        public EnumArray<TrackDirection, Location> OutPin { get; } = new EnumArray<TrackDirection, Location>();
        
        // path based passing path definitions
        internal AlternativePath StartAlternativePath { get; set; }  // if used : index 0 = index of alternative path, index 1 = TC end index
        internal AlternativePath EndAlternativePath { get; set; }    // if used : index 0 = index of alternative path, index 1 = TC start index

        // used for location based passing path processing
        public bool FacingPoint { get; private set; }            // element is facing point
        public int UsedAlternativePath { get; internal set; }     // set to index of used alternative path

        public int MovingTableApproachPath { get; internal set; } // set if approaching moving table, is index in access path list
                                                                  // used for moving table approach in timetable mode

        //================================================================================================//
        /// <summary>
        /// Constructor from tracknode
        /// </summary>

        public TrackCircuitRouteElement(TrackNode node, int trackCircuitIndex, TrackDirection direction)
        {
            ArgumentNullException.ThrowIfNull(node);

            TrackCircuitSection = TrackCircuitSection.TrackCircuitList[node.TrackCircuitCrossReferences[trackCircuitIndex].Index];
            Direction = direction;
            OutPin[Location.NearEnd] = direction;
            OutPin[Location.FarEnd] = TrackDirection.Ahead;           // always 0 for NORMAL sections, updated for JUNCTION sections

            if (TrackCircuitSection.CircuitType == TrackCircuitType.Crossover)
            {
                TrackDirection outPinLink = direction;
                int nextIndex;
                nextIndex = direction == TrackDirection.Reverse ? node.TrackCircuitCrossReferences[trackCircuitIndex - 1].Index : node.TrackCircuitCrossReferences[trackCircuitIndex + 1].Index;
                OutPin[Location.FarEnd] = (TrackCircuitSection.Pins[outPinLink, Location.NearEnd].Link == nextIndex) ? TrackDirection.Ahead : TrackDirection.Reverse;
            }

            FacingPoint = (TrackCircuitSection.CircuitType == TrackCircuitType.Junction && TrackCircuitSection.Pins[direction, Location.FarEnd].Link != -1);

            UsedAlternativePath = -1;
            MovingTableApproachPath = -1;
        }

        //================================================================================================//
        /// <summary>
        /// Constructor from CircuitSection
        /// </summary>
        public TrackCircuitRouteElement(TrackCircuitSection section, TrackDirection direction, int lastSectionIndex)
        {
            TrackCircuitSection = section ?? throw new ArgumentNullException(nameof(section));
            Direction = direction;
            OutPin[Location.NearEnd] = direction;
            OutPin[Location.FarEnd] = TrackDirection.Ahead;           // always 0 for NORMAL sections, updated for JUNCTION sections

            if (section.CircuitType == TrackCircuitType.Crossover)
            {
                TrackDirection inPinLink = direction.Reverse();
                OutPin[Location.FarEnd] = (section.Pins[inPinLink, Location.NearEnd].Link == lastSectionIndex) ? TrackDirection.Ahead : TrackDirection.Reverse;
            }

            FacingPoint = (section.CircuitType == TrackCircuitType.Junction && section.Pins[direction, Location.FarEnd].Link != -1);

            UsedAlternativePath = -1;
            MovingTableApproachPath = -1;
        }

        //================================================================================================//
        /// <summary>
        /// Constructor for additional items for route checking (not part of train route, NORMAL items only)
        /// </summary>
        public TrackCircuitRouteElement(int trackCircuitIndex, TrackDirection direction)
        {
            TrackCircuitSection = TrackCircuitSection.TrackCircuitList[trackCircuitIndex];
            Direction = direction;
            OutPin[Location.NearEnd] = direction;
            OutPin[Location.FarEnd] = TrackDirection.Ahead;
            UsedAlternativePath = -1;
            MovingTableApproachPath = -1;
        }

        //================================================================================================//
        //
        // Constructor from other route element
        //
        public TrackCircuitRouteElement(TrackCircuitRouteElement source)
        {
            ArgumentNullException.ThrowIfNull(source);

            TrackCircuitSection = source.TrackCircuitSection;
            Direction = source.Direction;

            OutPin = new EnumArray<TrackDirection, Location>(source.OutPin);

            if (source.StartAlternativePath != null)
            {
                StartAlternativePath = new AlternativePath()
                {
                    PathIndex = source.StartAlternativePath.PathIndex,
                    TrackCircuitSection = source.StartAlternativePath.TrackCircuitSection,
                };
            }

            if (source.EndAlternativePath != null)
            {
                EndAlternativePath = new AlternativePath()
                {
                    PathIndex = source.EndAlternativePath.PathIndex,
                    TrackCircuitSection = source.EndAlternativePath.TrackCircuitSection,
                };
            }

            FacingPoint = source.FacingPoint;
            UsedAlternativePath = source.UsedAlternativePath;
            MovingTableApproachPath = source.MovingTableApproachPath;
        }

        //================================================================================================//
        //
        // Restore
        //
        public TrackCircuitRouteElement(BinaryReader inf)
        {
            ArgumentNullException.ThrowIfNull(inf);
            int index = inf.ReadInt32();

            TrackCircuitSection = index> -1 ? TrackCircuitSection.TrackCircuitList[index] : TrackCircuitSection.Invalid;
            Direction = (TrackDirection)inf.ReadInt32();
            OutPin = new EnumArray<TrackDirection, Location>(new TrackDirection[] { (TrackDirection)inf.ReadInt32(), (TrackDirection)inf.ReadInt32() });

            int altindex = inf.ReadInt32();
            if (altindex >= 0)
            {
                StartAlternativePath = new AlternativePath()
                {
                    PathIndex = altindex,
                    TrackCircuitSection = TrackCircuitSection.TrackCircuitList[inf.ReadInt32()],
                };
            }

            altindex = inf.ReadInt32();
            if (altindex >= 0)
            {
                EndAlternativePath = new AlternativePath()
                {
                    PathIndex = altindex,
                    TrackCircuitSection = TrackCircuitSection.TrackCircuitList[inf.ReadInt32()],
                };
            }

            FacingPoint = inf.ReadBoolean();
            UsedAlternativePath = inf.ReadInt32();
            MovingTableApproachPath = inf.ReadInt32();
        }

        //================================================================================================//
        //
        // Save
        //
        public void Save(BinaryWriter outf)
        {
            ArgumentNullException.ThrowIfNull(outf);

            outf.Write(TrackCircuitSection.Index);
            outf.Write((int)Direction);
            outf.Write((int)OutPin[Location.NearEnd]);
            outf.Write((int)OutPin[Location.FarEnd]);

            if (StartAlternativePath != null)
            {
                outf.Write(StartAlternativePath.PathIndex);
                outf.Write(StartAlternativePath.TrackCircuitSection.Index);
            }
            else
            {
                outf.Write(-1);
            }


            if (EndAlternativePath != null)
            {
                outf.Write(EndAlternativePath.PathIndex);
                outf.Write(EndAlternativePath.TrackCircuitSection.Index);
            }
            else
            {
                outf.Write(-1);
            }

            outf.Write(FacingPoint);
            outf.Write(UsedAlternativePath);
            outf.Write(MovingTableApproachPath);
        }

        // Invalidate preceding section index to avoid wrong indexing when building route forward (in Reserve())
        internal void Invalidate()
        {
            TrackCircuitSection = TrackCircuitSection.Invalid;
        }
    }

}
