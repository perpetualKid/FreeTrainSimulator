using System;
using System.Collections.ObjectModel;
using System.Linq;

using MemoryPack;

using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{

    [MemoryPackable]
    public sealed partial class ExhaustMessage : MultiPlayerMessageContent
    {
        public Collection<ExhaustMessageItem> Items { get; private set; }

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
            foreach (ExhaustMessageItem item in Items ?? Enumerable.Empty<ExhaustMessageItem>())
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
