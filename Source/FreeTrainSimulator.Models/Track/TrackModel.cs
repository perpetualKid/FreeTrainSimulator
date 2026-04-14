using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Top-level model for a route's track infrastructure, combining both rail and road databases.
    /// Serialized with the <c>.tmodel</c> file extension.
    /// </summary>
    /// <remarks>
    /// Abstracts the MSTS <c>.tdb</c> (rail) and <c>.rdb</c> (road) track database files
    /// into a single pre-processed, MemoryPack-serializable container.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".tmodel")]
    public sealed partial record TrackModel : ModelBase
    {
        /// <inheritdoc/>
        public override RouteModel Parent => _parent as RouteModel;

        /// <summary>Rail track database containing all rail track nodes and items.</summary>
        public TrackDatabase TrackDatabase { get; init; }

        /// <summary>Road track database containing all road track nodes and items.</summary>
        public TrackDatabase RoadDatabase { get; init; }
    }
}
