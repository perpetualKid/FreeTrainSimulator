using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Header model containing summary information for a train path.
    /// Abstracts data originally stored in MSTS path files (<c>.pat</c>).
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("TrainPaths", ".path")]
    public partial record PathModelHeader : ModelBase
    {
        /// <inheritdoc/>
        public override RouteModelHeader Parent => _parent as RouteModelHeader;
        /// <summary>Start location of the path</summary>
        public string Start { get; init; }
        /// <summary>Destination location of the path</summary>
        public string End { get; init; }
        /// <summary>Is the path a player path or not</summary>
        public bool PlayerPath { get; init; }
        /// <summary>
        /// Result of validating the path against the current track via the path route resolver. Defaults to
        /// <see cref="PathValidationState.NotValidated"/> for paths that have not been validated yet (including
        /// path files written before this field existed). Appended last to preserve MemoryPack version-tolerant
        /// sequential layout.
        /// </summary>
        public PathValidationState ValidationState { get; init; }
    }
}
