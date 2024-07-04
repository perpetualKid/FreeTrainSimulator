using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Formats.Msts;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class FreightAnimationsSetSaveState: SaveStateBase
    {
        public float FreightWeight { get; set; }
        public PickupType FreightType { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<FreightAnimationSaveState> FreightAnimations { get; set; }
        public Collection<FreightAnimationSaveState> EmptyAnimations { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }

    [MemoryPackable]
    public sealed partial class FreightAnimationSaveState: SaveStateBase
    {
        public float IntakeOffset { get; set; }
        public float IntakeWidth { get; set; }
        public PickupType PickupType { get; set; }
        public Vector3 Offset { get; set; }
        public float LoadingAreaLength { get; set; }
        public float AboveLoadingAreaLength { get; set; }
        public LoadPosition LoadPosition { get; set; }
        public bool Loaded { get; set; }
        public ContainerSaveState Container {  get; set; }
    }
}
