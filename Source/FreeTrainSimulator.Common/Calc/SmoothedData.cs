using System;
using System.Collections.Generic;

namespace FreeTrainSimulator.Common.Calc
{
    public class SmoothedData
    {
        private const double DefaultSmoothPeriod = 3;
        private readonly double rate;

        public SmoothedData() : this(DefaultSmoothPeriod)
        {
        }

        public SmoothedData(double smoothPeriod)
        {
            SmoothPeriod = smoothPeriod;
            // Convert the input assuming 60 FPS (arbitary)
            rate = -60.0 * Math.Log(1 - 1 / (60 * smoothPeriod));
        }

        public virtual void Update(double elapsed, double value)
        {
            Value = value;

            if (elapsed < double.Epsilon)
            {
                if (double.IsNaN(SmoothedValue) || double.IsInfinity(SmoothedValue))
                    SmoothedValue = Value;
                return;
            }

            SmoothedValue = SmoothValue(SmoothedValue, elapsed, Value);
        }

        protected double SmoothValue(double smoothedValue, double elapsed, double value)
        {
            // This formula and the calculation of `rate` are FPS-independent;
            // see https://www.gamedeveloper.com/programming/improved-lerp-smoothing- for more details
            double ratio = Math.Exp(-rate * elapsed);
            return double.IsNaN(smoothedValue) || double.IsInfinity(smoothedValue) || ratio < 0.5
                ? value
                : smoothedValue * ratio + value * (1 - ratio);
        }

        public void Preset(double smoothedValue)
        {
            SmoothedValue = smoothedValue;
        }

        public double Value { get; private set; } = double.NaN;

        public double SmoothedValue { get; private set; } = double.NaN;

        public double SmoothPeriod { get; }
    }

    public class SmoothedDataWithPercentiles : SmoothedData
    {
        private const int historyStepCount = 40; // 40 units (i.e. 10 seconds)
        private const double historyStepSize = 0.25; // each unit = 1/4 second

        private readonly Queue<double> longHistory = new Queue<double>();
        private readonly Queue<int> historyCount = new Queue<int>(historyStepCount);

        private readonly List<double> samples = new List<double>();

        private int count;

        private double position;

        public double P50 { get; private set; }
        public double P95 { get; private set; }
        public double P99 { get; private set; }

        public double SmoothedP50 { get; private set; } = double.NaN;
        public double SmoothedP95 { get; private set; } = double.NaN;
        public double SmoothedP99 { get; private set; } = double.NaN;

        public SmoothedDataWithPercentiles()
            : base()
        {
            for (int i = 0; i < historyStepCount; i++)
                historyCount.Enqueue(0);
        }

        public override void Update(double elapsed, double value)
        {
            base.Update(elapsed, value);

            longHistory.Enqueue(value);
            position += elapsed;
            count++;

            if (position >= historyStepSize)
            {
                samples.Clear();
                samples.AddRange(longHistory);
                samples.Sort();

                P50 = samples[(int)(samples.Count * 0.50)];
                P95 = samples[(int)(samples.Count * 0.95)];
                P99 = samples[(int)(samples.Count * 0.99)];

                SmoothedP50 = SmoothValue(SmoothedP50, position, P50);
                SmoothedP95 = SmoothValue(SmoothedP95, position, P95);
                SmoothedP99 = SmoothValue(SmoothedP99, position, P99);

                historyCount.Enqueue(count);
                count = historyCount.Dequeue();
                while (count-- > 0)
                    longHistory.Dequeue();
                count = 0;
                position = 0;
            }
        }
    }
}
