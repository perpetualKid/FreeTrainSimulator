using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class ContainerSaveState : SaveStateBase
    {
        public string Name { get; set; }
        public string ShapeFileFolder { get; set; }
        public string ShapeFileName { get; set; }
        public string LoadFilePath { get; set; }
        public Vector3 ShapeOffset { get; set; }
        public ContainerType ContainerType { get; set; }
        public bool Flipped { get; set; }
        public float Mass { get; set; }
        public float EmptyMass { get; set; }
        public float MaxMass { get; set; }
        public Matrix RelativeStationPosition { get; set; }
    }
}
