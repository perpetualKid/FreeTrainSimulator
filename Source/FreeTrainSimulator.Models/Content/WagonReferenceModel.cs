using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a reference to a single wagon or locomotive within a train consist.
    /// Each entry identifies the rolling stock type, orientation, and external file reference.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Wagons", ".wagon")]
    public sealed partial record WagonReferenceModel: ModelBase
    {
        /// <inheritdoc/>
        public override FolderModel Parent => _parent as FolderModel;

        /// <summary>Classification of this car (engine, wagon, tender, etc.).</summary>
        public TrainCarType TrainCarType { get; init; } 
        /// <summary>Human-readable description of the wagon or locomotive.</summary>
        public string Description { get; init; }
        /// <summary>Unique identifier of this car within the owning consist.</summary>
        public int Uid { get; init; }
        /// <summary>Indicates whether this car is reversed relative to the consist direction.</summary>
        public  bool Reverse { get; init; }
        /// <summary>External file reference identifying the rolling stock definition.</summary>
        public string Reference { get; init; }
    }
}
