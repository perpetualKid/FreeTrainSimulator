// COPYRIGHT 2010, 2011 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;

namespace Orts.Common.Calc
{
    public class SmoothedData
    {
        public readonly float SmoothPeriodS = 3;
        protected float currentValue = float.NaN;
        protected float smoothedValue = float.NaN;

        public SmoothedData()
        {
        }

        public SmoothedData(float smoothPeriodS)
            : this()
        {
            SmoothPeriodS = smoothPeriodS;
        }

        public virtual void Update(float periodS, float value)
        {
            currentValue = value;

            if (periodS < float.Epsilon)
            {
                if (float.IsNaN(smoothedValue) || float.IsInfinity(smoothedValue))
                    smoothedValue = currentValue;
                return;
            }

            smoothedValue = SmoothValue(smoothedValue, periodS, currentValue);
        }

        protected float SmoothValue(float smoothedValue, float periodS, float value)
        {
            float rate = SmoothPeriodS / periodS;
            if (rate < 1 || float.IsNaN(smoothedValue) || float.IsInfinity(smoothedValue))
                return value;
            else
                return (smoothedValue * (rate - 1) + value) / rate;
        }

        public void Preset(float smoothedValue)
        {
            this.smoothedValue = smoothedValue;
        }

        public float Value { get { return currentValue; } }
        public float SmoothedValue { get { return smoothedValue; } }
    }

    public class SmoothedDataWithPercentiles : SmoothedData
    {
        private const int historyStepCount = 40; // 40 units (i.e. 10 seconds)
        private const float historyStepSize = 0.25f; // each unit = 1/4 second

        private Queue<float> longHistory = new Queue<float>();
        private Queue<int> historyCount = new Queue<int>(historyStepCount);

        private int count = 0;

        private float position;

        public float P50 { get; private set; }
        public float P95 { get; private set; }
        public float P99 { get; private set; }

        public float SmoothedP50 { get; private set; } = float.NaN;
        public float SmoothedP95 { get; private set; } = float.NaN;
        public float SmoothedP99 { get; private set; } = float.NaN;

        public SmoothedDataWithPercentiles()
            : base()
        {
            for (var i = 0; i < historyStepCount; i++)
                historyCount.Enqueue(0);
        }

        public override void Update(float periodS, float value)
        {
            base.Update(periodS, value);

            longHistory.Enqueue(value);
            position += periodS;
            count++;

            if (position >= historyStepSize)
            {
                var samples = new List<float>(longHistory);
                samples.Sort();

                P50 = samples[(int)(samples.Count * 0.50f)];
                P95 = samples[(int)(samples.Count * 0.95f)];
                P99 = samples[(int)(samples.Count * 0.99f)];

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
