using System;
using System.Collections.ObjectModel;

using MemoryPack;

using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class TrainCarItem
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
            WagonFilePath = trainCar.RealWagFilePath;

            //wagon path without folder name
            int index = WagonFilePath.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                WagonFilePath = WagonFilePath.Remove(0, index + 17);
            }

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
