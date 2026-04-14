using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a train consist (set of wagons and locomotives).
    /// Abstracts data originally stored in MSTS consist files (<c>.con</c>).
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("TrainSets", ".trainset")]
    public sealed partial record WagonSetModel : ModelBase
    {
        /// <inheritdoc/>
        public override FolderModel Parent => _parent as FolderModel;

        /// <summary>Maximum speed of the consist in meters per second, derived from tractive effort and tonnage.</summary>
        public float MaximumSpeed { get; init; }
        /// <summary>Multiplier that determines speed reduction on grades and curves for AI trains.</summary>
        public float AccelerationFactor { get; init; }
        /// <summary>Durability factor affecting wear and failure probability.</summary>
        public float Durability { get; init; }

        /// <summary>Ordered collection of wagon and locomotive references that compose this consist.</summary>
        public ImmutableArray<WagonReferenceModel> TrainCars { get; init; } = ImmutableArray<WagonReferenceModel>.Empty;
        /// <summary>The primary locomotive in the consist, determined by direction (<see cref="Reverse"/>).</summary>
        public WagonReferenceModel Locomotive => Reverse ? TrainCars.Where(c => c.TrainCarType == Common.TrainCarType.Engine).LastOrDefault() : TrainCars.Where(c => c.TrainCarType == Common.TrainCarType.Engine).FirstOrDefault();
        /// <summary>Indicates whether the consist is reversed from its defined order.</summary>
        [MemoryPackIgnore]
        public bool Reverse { get; init; }

        public override void Initialize(ModelBase parent)
        {
            foreach (WagonReferenceModel wagonReference in TrainCars)
            {
                wagonReference.Initialize(this);
            }
            base.Initialize(parent);
        }
    }
}
