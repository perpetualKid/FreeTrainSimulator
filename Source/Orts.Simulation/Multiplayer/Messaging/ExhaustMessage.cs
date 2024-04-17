using System;
using System.Collections.ObjectModel;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class ExhaustMessageItem
    {
        public int CarIndex { get; set; }
        public double Particles { get; set; }
        public double Magnitude { get; set; }
        public double ColorR { get; set; }
        public double ColorG { get; set; }
        public double ColorB { get; set; }

        [MemoryPackConstructor]
        public ExhaustMessageItem()
        { }

        public ExhaustMessageItem(int carIndex, MSTSDieselLocomotive dieselLocomotive)
        {
            ArgumentNullException.ThrowIfNull(dieselLocomotive, nameof(dieselLocomotive));

            CarIndex = carIndex;
            Particles = dieselLocomotive.ExhaustParticles.SmoothedValue;
            Magnitude = dieselLocomotive.ExhaustMagnitude.SmoothedValue;
            ColorR = dieselLocomotive.ExhaustColorR.SmoothedValue;
            ColorG = dieselLocomotive.ExhaustColorG.SmoothedValue;
            ColorB = dieselLocomotive.ExhaustColorB.SmoothedValue;
        }
    }

    [MemoryPackable]
    public partial class ExhaustMessage : MultiPlayerMessageContent
    {
        public Collection<ExhaustMessageItem> Items { get; }

        public int TrainNumber { get; set; }

        [MemoryPackConstructor]
        public ExhaustMessage() { }

        public ExhaustMessage(Train train)
        {
            ArgumentNullException.ThrowIfNull(train, nameof(train));

            TrainNumber = train.Number;
            for (int i = 0; i < train.Cars.Count; i++)
            {
                if (train.Cars[i] is MSTSDieselLocomotive dieselLocomotive)
                {
                    Items ??= new Collection<ExhaustMessageItem>();
                    Items.Add(new ExhaustMessageItem(i, dieselLocomotive));
                }
            }
        }

        public override void HandleMessage()
        {
            foreach (ExhaustMessageItem item in Items)
            {
                Train train = MultiPlayerManager.FindPlayerTrain(User);
                if (train != null && train.Cars.Count > item.CarIndex && train.Cars[item.CarIndex] is MSTSDieselLocomotive dieselLocomotive)
                {
                    dieselLocomotive.RemoteUpdate(item.Particles, item.Magnitude, item.ColorR, item.ColorG, item.ColorB);
                }
            }
        }
    }
}
