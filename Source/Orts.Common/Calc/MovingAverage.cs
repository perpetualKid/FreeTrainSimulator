using System;
using System.Collections.Generic;
using System.Linq;

namespace Orts.Common.Calc
{
    public class MovingAverage
    {
        private readonly Queue<double> buffer;
        private readonly int size;

        public MovingAverage():
            this(10)
        {
        }

        public MovingAverage(int size)
        {
            this.size = Math.Max(1, size);
            buffer = new Queue<double>(this.size);
            Initialize();
        }

        public int Size
        {
            get { return buffer.Count;}
        }

        public void Initialize(double value)
        {
            buffer.Clear();
            for (int i = 0; i < size; i++)
            {
                buffer.Enqueue(value);
            }
        }
        public void Initialize()
        {
            Initialize(0f);
        }

        public double Update(double value)
        {
            if (!double.IsNaN(value))
            {
                buffer.Dequeue();
                buffer.Enqueue(value);
            }
            return buffer.Average();
        }
    }
}
