using System;
using System.IO;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainCarFreightAnimationItem
    {
        public string FileName { get; set; }
        public string DirectoryName { get; set; }
        public LoadPosition LoadPosition { get; set; }

        [MemoryPackConstructor]
        public TrainCarFreightAnimationItem() { }

        public TrainCarFreightAnimationItem(FreightAnimationDiscrete freightAnimation)
        {
            ArgumentNullException.ThrowIfNull(freightAnimation, nameof(freightAnimation));

            FileName = Path.GetFileNameWithoutExtension(freightAnimation.Container.LoadFilePath);
            DirectoryName = Path.GetRelativePath(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, Path.GetDirectoryName(freightAnimation.Container.LoadFilePath));
            LoadPosition = freightAnimation.LoadPosition;
        }
    }
}
