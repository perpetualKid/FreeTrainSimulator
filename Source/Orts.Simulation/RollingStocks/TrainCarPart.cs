using System;
using System.Collections;
using System.Collections.Generic;

namespace Orts.Simulation.RollingStocks
{
    // data and methods used to align trucks and models to track
    public class TrainCarPart
    {
        public float OffsetM { get; }   // distance from center of model, positive forward
        public int Matrix { get; }     // matrix in shape that needs to be moved
        public float Cos { get; internal set; } = 1;       // truck angle cosine
        public float Sin { get; internal set; }       // truck angle sin
        // line fitting variables
        public double SumWgt { get; internal set; }
        public double SumOffset { get; internal set; }
        public double SumOffsetSq { get; internal set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public double[] SumX { get; } = new double[4];
        public double[] SumXOffset { get; } = new double[4];
        public float[] A { get; } = new float[4];
        public float[] B { get; } = new float[4];
#pragma warning restore CA1819 // Properties should not return arrays
        public bool Bogie { get; }

        public static TrainCarPart None { get; } = new TrainCarPart();

        private TrainCarPart()
        { }

        public TrainCarPart(float offset, int i, bool bogie)
        {
            OffsetM = offset;
            Matrix = i;
            Bogie = bogie;
        }

        public void InitLineFit()
        {
            SumWgt = SumOffset = SumOffsetSq = 0;
            for (int i = 0; i < 4; i++)
                SumX[i] = SumXOffset[i] = 0;
        }

        public void AddWheelSetLocation(float w, float o, float x, float y, float z, float t)
        {
            SumWgt += w;
            SumOffset += w * o;
            SumOffsetSq += w * o * o;
            SumX[0] += w * x;
            SumXOffset[0] += w * x * o;
            SumX[1] += w * y;
            SumXOffset[1] += w * y * o;
            SumX[2] += w * z;
            SumXOffset[2] += w * z * o;
            SumX[3] += w * t;
            SumXOffset[3] += w * t * o;
        }

        public void AddPartLocation(float w, TrainCarPart part)
        {
            SumWgt += w;
            SumOffset += w * part?.OffsetM ?? throw new ArgumentNullException(nameof(part));
            SumOffsetSq += w * part.OffsetM * part.OffsetM;
            for (int i = 0; i < 4; i++)
            {
                float x = part.A[i] + part.OffsetM * part.B[i];
                SumX[i] += w * x;
                SumXOffset[i] += w * x * part.OffsetM;
            }
        }

        public void FindCenterLine()
        {
            double d = SumWgt * SumOffsetSq - SumOffset * SumOffset;
            if (d > 1e-20)
            {
                for (int i = 0; i < 4; i++)
                {
                    A[i] = (float)((SumOffsetSq * SumX[i] - SumOffset * SumXOffset[i]) / d);
                    B[i] = (float)((SumWgt * SumXOffset[i] - SumOffset * SumX[i]) / d);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    A[i] = (float)(SumX[i] / SumWgt);
                    B[i] = 0;
                }
            }
        }
    }

    public class TrainCarParts : IList<TrainCarPart>
    {
        private readonly List<TrainCarPart> list = new List<TrainCarPart>();

        public TrainCarPart this[int index] { get => list[index]; set => Insert(index, value); }

        public int Count => list.Count;

        public bool IsReadOnly => false;

        public void Add(TrainCarPart item)
        {
            list.Add(item);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(TrainCarPart item) => list.Contains(item);

        public void CopyTo(TrainCarPart[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TrainCarPart> GetEnumerator() => list.GetEnumerator();

        public int IndexOf(TrainCarPart item) => list.IndexOf(item);

        public void Insert(int index, TrainCarPart item)
        {
            if (index >= list.Count)
            {
                while (index > list.Count)
                    Add(TrainCarPart.None);
                Add(item);
            }
            else
                list[index] = item;
        }

        public bool Remove(TrainCarPart item) => list.Remove(item);

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
    }
}
