using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemoryPack;

using Orts.Common.Position;

using Orts.Common;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Common.Calc;
using Orts.Formats.Msts;
using System.Diagnostics;
using System.Drawing;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public partial class TrainFlipMessage : TrainStateBaseMessage
    {
        public bool ReverseMultiUnit { get; set; }

        [MemoryPackConstructor]
        public TrainFlipMessage() { }

        public TrainFlipMessage(Train train, bool reverseMultiUnit) : base(train)
        {
            ReverseMultiUnit = reverseMultiUnit;
        }

        public override void HandleMessage()
        {
            foreach (Train train in Simulator.Instance.Trains)
            {
                if (train.Number == TrainNumber)
                {
                    train.ToDoUpdate(TrackNodeIndex, RearLocation.TileX, RearLocation.TileZ, RearLocation.Location.X, RearLocation.Location.Z, DistanceTravelled, Speed, MultiUnitDirection, TrainDirection, Length, true, ReverseMultiUnit);
                    return;
                }
            }
        }
    }
}
