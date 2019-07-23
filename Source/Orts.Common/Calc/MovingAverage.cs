using System;
using System.Collections.Generic;
using System.Linq;

namespace Orts.Common.Calc
{
    public class MovingAverage
    {
        private readonly Queue<float> buffer;
        private readonly int size;

        public MovingAverage():
            this(10)
        {
        }

        public MovingAverage(int size)
        {
            this.size = Math.Max(1, size);
            buffer = new Queue<float>(this.size);
            Initialize();
        }

        public int Size
        {
            get { return buffer.Count;}
        }

        public void Initialize(float value)
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

        public float Update(float value)
        {
            if (!float.IsNaN(value))
            {
                buffer.Dequeue();
                buffer.Enqueue(value);
            }
            return buffer.Average();
        }
    }
}
