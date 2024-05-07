using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using Orts.Common;
using Orts.Formats.Msts.Models;
using Orts.Models.State;
using Orts.Simulation.Signalling;

using SharpDX.Direct2D1;

namespace Orts.Simulation.Track
{
    //================================================================================================//
    /// <summary>
    /// Track Circuit Route Element
    /// </summary>
    public class TrackCircuitRouteElement : ISaveStateApi<TrackCircuitRouteElementSaveState>
    {
        internal class AlternativePath : ISaveStateApi<TrackCircuitRouteElementAlternativePathSaveState>
        {
            //Index of Alternative Path
            public int PathIndex { get; private set; }
            //related TrackCircuitCrossReferences Index
            public TrackCircuitSection TrackCircuitSection { get; private set; }

            public AlternativePath() { }

            public AlternativePath(int pathIndex, TrackCircuitSection trackCircuitSection)
            {
                ArgumentNullException.ThrowIfNull(trackCircuitSection, nameof(trackCircuitSection));

                PathIndex = pathIndex;
                TrackCircuitSection = trackCircuitSection;
            }

            public ValueTask Restore(TrackCircuitRouteElementAlternativePathSaveState saveState)
            {
                ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));
                PathIndex = saveState.PathIndex;
                TrackCircuitSection = TrackCircuitSection.TrackCircuitList[saveState.TrackCircuitSectionIndex];
                return ValueTask.CompletedTask;
            }

            public ValueTask<TrackCircuitRouteElementAlternativePathSaveState> Snapshot()
            {
                return ValueTask.FromResult(new TrackCircuitRouteElementAlternativePathSaveState()
                {
                    PathIndex = PathIndex,
                    TrackCircuitSectionIndex = TrackCircuitSection.Index
                });
            }
        }

        public TrackCircuitSection TrackCircuitSection { get; private set; }
        public TrackDirection Direction { get; internal set; }
        public EnumArray<TrackDirection, SignalLocation> OutPin { get; } = new EnumArray<TrackDirection, SignalLocation>();

        // path based passing path definitions
        internal AlternativePath StartAlternativePath { get; set; }  // if used : index 0 = index of alternative path, index 1 = TC end index
        internal AlternativePath EndAlternativePath { get; set; }    // if used : index 0 = index of alternative path, index 1 = TC start index

        // used for location based passing path processing
        public bool FacingPoint { get; private set; }            // element is facing point
        public int UsedAlternativePath { get; internal set; }     // set to index of used alternative path

        public int MovingTableApproachPath { get; internal set; } // set if approaching moving table, is index in access path list
                                                                  // used for moving table approach in timetable mode

        public TrackCircuitRouteElement() { }
        //================================================================================================//
        /// <summary>
        /// Constructor from tracknode
        /// </summary>

        public TrackCircuitRouteElement(TrackNode node, int trackCircuitIndex, TrackDirection direction)
        {
            ArgumentNullException.ThrowIfNull(node);

            TrackCircuitSection = TrackCircuitSection.TrackCircuitList[node.TrackCircuitCrossReferences[trackCircuitIndex].Index];
            Direction = direction;
            OutPin[SignalLocation.NearEnd] = direction;
            OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;           // always 0 for NORMAL sections, updated for JUNCTION sections

            if (TrackCircuitSection.CircuitType == TrackCircuitType.Crossover)
            {
                TrackDirection outPinLink = direction;
                int nextIndex;
                nextIndex = direction == TrackDirection.Reverse ? node.TrackCircuitCrossReferences[trackCircuitIndex - 1].Index : node.TrackCircuitCrossReferences[trackCircuitIndex + 1].Index;
                OutPin[SignalLocation.FarEnd] = (TrackCircuitSection.Pins[outPinLink, SignalLocation.NearEnd].Link == nextIndex) ? TrackDirection.Ahead : TrackDirection.Reverse;
            }

            FacingPoint = (TrackCircuitSection.CircuitType == TrackCircuitType.Junction && TrackCircuitSection.Pins[direction, SignalLocation.FarEnd].Link != -1);

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
            OutPin[SignalLocation.NearEnd] = direction;
            OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;           // always 0 for NORMAL sections, updated for JUNCTION sections

            if (section.CircuitType == TrackCircuitType.Crossover)
            {
                TrackDirection inPinLink = direction.Reverse();
                OutPin[SignalLocation.FarEnd] = (section.Pins[inPinLink, SignalLocation.NearEnd].Link == lastSectionIndex) ? TrackDirection.Ahead : TrackDirection.Reverse;
            }

            FacingPoint = (section.CircuitType == TrackCircuitType.Junction && section.Pins[direction, SignalLocation.FarEnd].Link != -1);

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
            OutPin[SignalLocation.NearEnd] = direction;
            OutPin[SignalLocation.FarEnd] = TrackDirection.Ahead;
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

            OutPin = new EnumArray<TrackDirection, SignalLocation>(source.OutPin);

            if (source.StartAlternativePath != null)
            {
                StartAlternativePath = new AlternativePath(source.StartAlternativePath.PathIndex, source.StartAlternativePath.TrackCircuitSection);
            }

            if (source.EndAlternativePath != null)
            {
                EndAlternativePath = new AlternativePath(source.EndAlternativePath.PathIndex, source.EndAlternativePath.TrackCircuitSection);
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

            TrackCircuitSection = index > -1 ? TrackCircuitSection.TrackCircuitList[index] : TrackCircuitSection.Invalid;
            Direction = (TrackDirection)inf.ReadInt32();
            OutPin = new EnumArray<TrackDirection, SignalLocation>(new TrackDirection[] { (TrackDirection)inf.ReadInt32(), (TrackDirection)inf.ReadInt32() });

            int altindex = inf.ReadInt32();
            if (altindex >= 0)
            {
                StartAlternativePath = new AlternativePath(altindex, TrackCircuitSection.TrackCircuitList[inf.ReadInt32()]);
            }

            altindex = inf.ReadInt32();
            if (altindex >= 0)
            {
                EndAlternativePath = new AlternativePath(altindex, TrackCircuitSection.TrackCircuitList[inf.ReadInt32()]);
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
            outf.Write((int)OutPin[SignalLocation.NearEnd]);
            outf.Write((int)OutPin[SignalLocation.FarEnd]);

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

        public async ValueTask<TrackCircuitRouteElementSaveState> Snapshot()
        {
            return new TrackCircuitRouteElementSaveState()
            {
                TrackCircuitSectionIndex = TrackCircuitSection.Index,
                Direction = Direction,
                OutPin = OutPin.ToArray(),
                AlternativePathStart = StartAlternativePath != null ? await StartAlternativePath.Snapshot().ConfigureAwait(false) : null,
                AlternativePathEnd = EndAlternativePath != null ? await EndAlternativePath.Snapshot().ConfigureAwait(false) : null,
                FacingPoint = FacingPoint,
                AlternativePathIndex = UsedAlternativePath,
                MovingTableApproachPath = MovingTableApproachPath,
            };
        }

        public async ValueTask Restore(TrackCircuitRouteElementSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            TrackCircuitSection = saveState.TrackCircuitSectionIndex > -1 ? TrackCircuitSection.TrackCircuitList[saveState.TrackCircuitSectionIndex] : TrackCircuitSection.Invalid;
            Direction = saveState.Direction;
            OutPin.FromArray(saveState.OutPin);
            if (saveState.AlternativePathStart != null)
            {
                StartAlternativePath = new AlternativePath();
                await StartAlternativePath.Restore(saveState.AlternativePathStart).ConfigureAwait(false);
            }
            if (saveState.AlternativePathEnd != null)
            {
                EndAlternativePath = new AlternativePath();
                await EndAlternativePath.Restore(saveState.AlternativePathEnd).ConfigureAwait(false);
            }
            FacingPoint = saveState.FacingPoint;
            UsedAlternativePath = saveState.AlternativePathIndex;
            MovingTableApproachPath = saveState.MovingTableApproachPath;
        }
    }

}
