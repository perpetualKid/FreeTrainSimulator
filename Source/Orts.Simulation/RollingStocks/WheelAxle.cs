using System.Collections.Generic;

namespace Orts.Simulation.RollingStocks
{
    public class WheelAxle : IComparer<WheelAxle>
    {
        public float OffsetM { get; internal set; }   // distance from center of model, positive forward
        public int BogieIndex { get; internal set; }
        public int BogieMatrix { get; internal set; }
        public TrainCarPart Part { get; internal set; }

        public WheelAxle(float offset, int bogie, int parentMatrix)
        {
            OffsetM = offset;
            BogieIndex = bogie;
            BogieMatrix = parentMatrix;
        }

        public int Compare(WheelAxle x, WheelAxle y)
        {
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            return x.OffsetM.CompareTo(y.OffsetM);
        }
    }
}
