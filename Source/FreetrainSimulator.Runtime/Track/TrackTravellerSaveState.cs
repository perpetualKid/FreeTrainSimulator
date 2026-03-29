using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Track;

using MemoryPack;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Serializable snapshot of a <see cref="TrackTraveller"/>'s position.
    /// Produced by <see cref="TrackTraveller.Snapshot"/> and consumed by
    /// <see cref="TrackTraveller.InitializeTraveller(TrackTravellerSaveState)"/>.
    /// Properties must stay in declaration order for MemoryPack version tolerance.
    /// </summary>
    [MemoryPackable]
    public sealed partial class TrackTravellerSaveState : SaveStateBase, ISaveStateRestoreApi<TrackTravellerSaveState, TrackTraveller>
    {
        /// <summary>Zero-based node index into the rail or road <c>TrackDatabase.TrackNodes</c> array.</summary>
        public int TrackNodeIndex { get; set; }

        /// <summary>Zero-based index of the <see cref="VectorSectionNode"/> within the <see cref="VectorNode"/>.</summary>
        public int SectionIndex { get; set; }

        /// <summary>Offset in metres from the start of the section.</summary>
        public double SectionOffset { get; set; }

        /// <summary>Direction of travel at the time of the snapshot.</summary>
        public TrackDirection Direction { get; set; }

        /// <summary>Whether the traveller was on the rail or road database.</summary>
        public TrackDataBaseType TrackDataBaseType { get; set; }

        /// <summary>
        /// Creates a restored <see cref="TrackTraveller"/> from <paramref name="saveState"/>.
        /// Returns a default (off-track) traveller when the save state references an invalid node or section.
        /// </summary>
        public TrackTraveller CreateRuntimeTarget(TrackTravellerSaveState saveState)
            => TrackTraveller.InitializeTraveller(saveState) ?? default;
    }
}
