using System;

using MemoryPack;

using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class ExhaustMessageItem
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
}
