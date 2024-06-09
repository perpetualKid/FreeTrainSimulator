using System;

namespace Orts.Simulation.Track
{
    public class WaitingPointDetail
    {
        internal int[] Values { get; } = new int[6];
        public int SubListIndex { get => Values[0]; set { Values[0] = value; } }
        public int WaitingPointSection { get => Values[1]; set { Values[1] = value; } }
        public int WaitTime { get => Values[2]; set { Values[2] = value; } }
        public int DepartTime { get => Values[3]; set { Values[3] = value; } }
        public int HoldSignal { get => Values[4]; set { Values[4] = value; } }
        public int Offset { get => Values[5]; set { Values[5] = value; } }

        public WaitingPointDetail() { }

        public WaitingPointDetail(WaitingPointDetail source): this(source?.Values ?? throw new ArgumentNullException(nameof(source)))
        {
        }

        public WaitingPointDetail(int[] source)
        {
            source.CopyTo(Values, 0);
        }

    }
}
