using System;
using System.Collections.ObjectModel;
using System.IO;

using MemoryPack;

using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainCarItem
    {
        public string TrainCarId { get; set; }
        public bool Flipped { get; set; }
        public float Length { get; set; }
        public string WagonFilePath { get; set; }

        public Collection<TrainCarFreightAnimationItem> FreightAnimations { get; private set; }

        [MemoryPackConstructor]
        public TrainCarItem() { }

        public TrainCarItem(TrainCar trainCar)
        {
            TrainCarId = trainCar.CarID;
            Flipped = trainCar.Flipped;
            Length = trainCar.CarLengthM;
            //wagon path without folder name
            WagonFilePath = Path.GetRelativePath(Simulator.Instance.RouteFolder.ContentFolder.TrainSetsFolder, trainCar.RealWagFilePath);

            if (trainCar.FreightAnimations != null)
            {
                foreach (FreightAnimation animation in trainCar.FreightAnimations.Animations)
                {
                    if (animation is FreightAnimationDiscrete discreteAnimation)
                    {
                        FreightAnimations ??= new Collection<TrainCarFreightAnimationItem>();
                        FreightAnimations.Add(new TrainCarFreightAnimationItem(discreteAnimation));
                    }
                }
            }
        }
    }
}
