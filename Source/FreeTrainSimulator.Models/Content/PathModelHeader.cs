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
    }
}
