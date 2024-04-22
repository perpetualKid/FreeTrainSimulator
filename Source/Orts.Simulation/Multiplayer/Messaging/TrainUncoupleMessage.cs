using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class TrainUncoupleMessage : MultiPlayerMessageContent
    {
        public override void HandleMessage()
        {
            throw new NotImplementedException();
        }
    }
}
