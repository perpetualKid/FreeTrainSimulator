using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using MemoryPack;

using Orts.Simulation.Physics;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class RemoveTrainMessage : MultiPlayerMessageContent
    {
        public Collection<int> Trains { get; }

        [MemoryPackConstructor]
        public RemoveTrainMessage() { }

        public RemoveTrainMessage(IEnumerable<Train> trains)
        {
            ArgumentNullException.ThrowIfNull(trains, nameof(trains));

            Trains ??= new Collection<int>();
            foreach (Train train in trains)
            {
                Trains.Add(train.Number);
            }
        }

        public override void HandleMessage()
        {
            foreach (int trainNumber in Trains ?? Enumerable.Empty<int>())
            {
                foreach (Train train in Simulator.Instance.Trains)
                {
                    if (trainNumber == train.Number)
                    {
                        if (multiPlayerManager.IsDispatcher)
                            multiPlayerManager.AddOrRemoveLocomotives(string.Empty, train, false);
                        multiPlayerManager.AddOrRemoveTrain(train, false);//added to the removed list, treated later to be thread safe
                    }
                }
            }

        }
    }
}
