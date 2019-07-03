// COPYRIGHT 2010, 2011, 2012 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.IO;

namespace Orts.Parsers.Msts
{
    /// <summary>
    /// Interpolated table lookup
    /// Supports linear or cubic spline interpolation
    /// </summary>
    public class Interpolator
    {
        private float[] xArray;  // must be in increasing order
        private float[] yArray;
        private float[] y2Array;
        private int size;       // number of values populated
        private int prevIndex;  // used to speed up repeated evaluations with similar x values

        public Interpolator(int n)
        {
            xArray = new float[n];
            yArray = new float[n];
        }
        public Interpolator(float[] x, float[] y)
        {
            xArray = x;
            yArray = y;
            size = xArray.Length;
        }

        public Interpolator(Interpolator other)
        {
            xArray = other.xArray;
            yArray = other.yArray;
            y2Array= other.y2Array;
            size = other.size;
        }

        public Interpolator(STFReader stf)
        {
            List<float> list = new List<float>();
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
                list.Add(stf.ReadFloat(STFReader.UNITS.Any, null));
            if (list.Count % 2 == 1)
                STFException.TraceWarning(stf, "Ignoring extra odd value in Interpolator list.");
            int n = list.Count/2;
            if (n < 2)
                STFException.TraceWarning(stf, "Interpolator must have at least two value pairs.");
            xArray = new float[n];
            yArray = new float[n];
            size = n;
            for (int i = 0; i < n; i++)
            {
                xArray[i] = list[2*i];
                yArray[i] = list[2*i+1];
                if (i > 0 && xArray[i - 1] >= xArray[i])
                    STFException.TraceWarning(stf, "Interpolator x values must be increasing.");
            }
        }

        public float this[float x]
        {
            get
            {
                if (x < xArray[prevIndex] || x > xArray[prevIndex + 1])
                {
                    if (x < xArray[1])
                        prevIndex = 0;
                    else if (x > xArray[size - 2])
                        prevIndex = size - 2;
                    else
                    {
                        int i = 0;
                        int j = size - 1;
                        while (j - i > 1)
                        {
                            int k = (i + j) / 2;
                            if (xArray[k] > x)
                                j = k;
                            else
                                i = k;
                        }
                        prevIndex = i;
                    }
                }
                float d = xArray[prevIndex + 1] - xArray[prevIndex];
                float a = (xArray[prevIndex + 1] - x) / d;
                float b = (x - xArray[prevIndex]) / d;
                float y = a * yArray[prevIndex] + b * yArray[prevIndex + 1];
                if (y2Array != null && a >= 0 && b >= 0)
                    y += ((a * a * a - a) * y2Array[prevIndex] + (b * b * b - b) * y2Array[prevIndex + 1]) * d * d / 6;
                return y;
            }
            set
            {
                xArray[size] = x;
                yArray[size] = value;
                size++;
            }
        }

        public float MinX() { return xArray[0]; }

        public float MaxX() { return xArray[size-1]; }

        public float MaxY() { return MaxY(out float x); }

        public float MaxY(out float x)
        {
            int maxi= 0;
            for (int i = 1; i < size; i++)
                if (yArray[maxi] < yArray[i])
                    maxi = i;
            x = xArray[maxi];
            return yArray[maxi];
        }

        public bool HasNegativeValue()
        {
            for (int i = 1; i < size; i++)
            {
                if (yArray[i] < 0)
                    return true;
            }
            return false;
        }

        public void ScaleX(float factor)
        {
            for (int i = 0; i < size; i++)
                xArray[i] *= factor;
        }

        public void ScaleY(float factor)
        {
            for (int i = 0; i < size; i++)
                yArray[i] *= factor;
            if (y2Array != null)
            {
                for (int i = 0; i < size; i++)
                    y2Array[i]*= factor;
            }
        }

        public void ComputeSpline()
        {
            ComputeSpline(null,null);
        }

        public void ComputeSpline(float? yp1, float? yp2)
        {
            y2Array = new float[size];
            float[] u= new float[size];
            if (yp1 == null)
            {
                y2Array[0]= 0;
                u[0]= 0;
            }
            else
            {
                y2Array[0]= -.5f;
                float d= xArray[1]-xArray[0];
                u[0]= 3/d * ((yArray[1]-yArray[0])/d-yp1.Value);
            }
            for (int i=1; i<size-1; i++)
            {
                float sig= (xArray[i]-xArray[i-1]) / (xArray[i+1]-xArray[i-1]);
                float p= sig*y2Array[i-1] + 2;
                y2Array[i]= (sig-1)/p;
                u[i]= (6*((yArray[i+1]-yArray[i])/(xArray[i+1]-xArray[i]) -
                    (yArray[i]-yArray[i-1])/(xArray[i]-xArray[i-1])) / (xArray[i+1]-xArray[i-1]) -
                    sig*u[i-1]) / p;
            }
            if (yp2 == null)
            {
                y2Array[size-1]= 0;
            }
            else
            {
                float d= xArray[size-1]-xArray[size-2];
                y2Array[size-1]= (3/d *(yp2.Value-(yArray[size-1]-yArray[size-2])/d)- .5f*u[size-2])/(.5f*y2Array[size-2]+1);
            }
            for (int i=size-2; i>=0; i--)
                y2Array[i]= y2Array[i]*y2Array[i+1] + u[i];
        }
        
        // restore game state
        public Interpolator(BinaryReader inf)
        {
            size = inf.ReadInt32();
            xArray = new float[size];
            yArray = new float[size];
            for (int i = 0; i < size; i++)
            {
                xArray[i] = inf.ReadSingle();
                yArray[i] = inf.ReadSingle();
            }
            if (inf.ReadBoolean())
            {
                y2Array = new float[size];
                for (int i = 0; i < size; i++)
                    y2Array[i] = inf.ReadSingle();
            }
        }

        // save game state
        public void Save(BinaryWriter outf)
        {
            outf.Write(size);
            for (int i = 0; i < size; i++)
            {
                outf.Write(xArray[i]);
                outf.Write(yArray[i]);
            }
            outf.Write(y2Array != null);
            if (y2Array != null)
                for (int i = 0; i < size; i++)
                    outf.Write(y2Array[i]);
        }

        public void Test(string label, int n)
        {
            float dx = (MaxX() - MinX()) / (n-1);
            for (int i = 0; i < n; i++)
            {
                float x = MinX() + i * dx;
                float y = this[x];
                Console.WriteLine("{0} {1} {2}", label, x, y);
            }
        }

        public int GetSize()
        {
            if (xArray.Length == yArray.Length)
                return size;
            else
                return -1;
        }
    }

    /// <summary>
    /// two dimensional Interpolated table lookup - for use in Diesel
    /// </summary>
    public class InterpolatorDiesel2D
    {
        private float[] xArray;  // must be in increasing order
        private Interpolator[] yArray;
        private int size;       // number of values populated
        private int prevIndex;  // used to speed up repeated evaluations with similar x values
        private bool hasNegativeValues; // set when negative Y values present (e.g. in old triphase locos)

        public InterpolatorDiesel2D(int n)
        {
            xArray = new float[n];
            yArray = new Interpolator[n];
        }

        public InterpolatorDiesel2D(InterpolatorDiesel2D other)
        {
            xArray = other.xArray;
            size = other.size;
            yArray = new Interpolator[size];
            for (int i = 0; i < size; i++)
                yArray[i] = new Interpolator(other.yArray[i]);
        }
        public InterpolatorDiesel2D(STFReader stf, bool tab)
        {
            // <CSComment> TODO: probably there is some other stf.SkipRestOfBlock() that should be removed </CSComment>
            List<float> xlist = new List<float>();
            List<Interpolator> ilist = new List<Interpolator>();

            bool errorFound = false;
            if (tab)
            {
                stf.MustMatch("(");
                int numOfRows = stf.ReadInt(0);
                if (numOfRows < 2)
                {
                    STFException.TraceWarning(stf, "Interpolator must have at least two rows.");
                    errorFound = true;
                }
                int numOfColumns = stf.ReadInt(0);
                string header = stf.ReadString().ToLower();
                if (header == "throttle")
                {
                    stf.MustMatch("(");
                    int numOfThrottleValues = 0;
                    while (!stf.EndOfBlock())
                    {
                        xlist.Add(stf.ReadFloat(STFReader.UNITS.None, 0f));
                        ilist.Add(new Interpolator(numOfRows));
                        numOfThrottleValues++;
                    }
                    if (numOfThrottleValues != (numOfColumns - 1))
                    {
                        STFException.TraceWarning(stf, "Interpolator throttle vs. num of columns mismatch.");
                        errorFound = true;
                    }

                    if (numOfColumns < 3)
                    {
                        STFException.TraceWarning(stf, "Interpolator must have at least three columns.");
                        errorFound = true;
                    }

                    int numofData = 0;
                    string tableLabel = stf.ReadString().ToLower();
                    if (tableLabel == "table")
                    {
                        stf.MustMatch("(");
                        for (int i = 0; i < numOfRows; i++)
                        {
                            float x = stf.ReadFloat(STFReader.UNITS.SpeedDefaultMPH, 0);
                            numofData++;
                            for (int j = 0; j < numOfColumns - 1; j++)
                            {
                                if (j >= ilist.Count)
                                {
                                    STFException.TraceWarning(stf, "Interpolator throttle vs. num of columns mismatch. (missing some throttle values)");
                                    errorFound = true;
                                }
                                ilist[j][x] = stf.ReadFloat(STFReader.UNITS.Force, 0);
                                numofData++;
                            }
                        }
                        stf.SkipRestOfBlock();
                    }
                    else
                    {
                        STFException.TraceWarning(stf, "Interpolator didn't find a table to load.");
                        errorFound = true;
                    }
                    //check the table for inconsistencies

                    foreach (Interpolator checkMe in ilist)
                    {
                        if (checkMe.GetSize() != numOfRows)
                        {
                            STFException.TraceWarning(stf, "Interpolator has found a mismatch between num of rows declared and num of rows given.");
                            errorFound = true;
                        }
                        float dx = (checkMe.MaxX() - checkMe.MinX()) * 0.1f;
                        if (dx <= 0f)
                        {
                            STFException.TraceWarning(stf, "Interpolator has found X data error - x values must be increasing. (Possible row number mismatch)");
                            errorFound = true;
                        }
                        else
                        {
                            for (float x = checkMe.MinX(); x <= checkMe.MaxX(); x += dx)
                            {
                                if ((checkMe[x] == float.NaN))
                                {
                                    STFException.TraceWarning(stf, "Interpolator has found X data error - x values must be increasing. (Possible row number mismatch)");
                                    errorFound = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (numofData != (numOfRows * numOfColumns))
                    {
                        STFException.TraceWarning(stf, "Interpolator has found a mismatch: num of data doesn't fit the header information.");
                        errorFound = true;
                    }
                }
                else
                {
                    STFException.TraceWarning(stf, "Interpolator must have a 'throttle' header row.");
                    errorFound = true;
                }
                stf.SkipRestOfBlock();
            }
            else
            {
                stf.MustMatch("(");
                while (!stf.EndOfBlock())
                {
                    xlist.Add(stf.ReadFloat(STFReader.UNITS.Any, null));
                    ilist.Add(new Interpolator(stf));
                }
            }


            int n = xlist.Count;
            if (n < 2)
            {
                STFException.TraceWarning(stf, "Interpolator must have at least two x values.");
                errorFound = true;
            }
            xArray = new float[n];
            yArray = new Interpolator[n];
            size = n;
            for (int i = 0; i < n; i++)
            {
                xArray[i] = xlist[i];
                yArray[i] = ilist[i];
                if (i > 0 && xArray[i - 1] >= xArray[i])
                    STFException.TraceWarning(stf, "Interpolator x values must be increasing.");
            }
            //stf.SkipRestOfBlock();
            if (errorFound)
            {
                STFException.TraceWarning(stf, "Errors found in the Interpolator definition!!! The Interpolator will not work correctly!");
            }
        }

        public float Get(float x, float y)
        {
            if (x < xArray[prevIndex] || x > xArray[prevIndex + 1])
            {
                if (x < xArray[1])
                    prevIndex = 0;
                else if (x > xArray[size - 2])
                    prevIndex = size - 2;
                else
                {
                    int i = 0;
                    int j = size - 1;
                    while (j - i > 1)
                    {
                        int k = (i + j) / 2;
                        if (xArray[k] > x)
                            j = k;
                        else
                            i = k;
                    }
                    prevIndex = i;
                }
            }
            float d = xArray[prevIndex + 1] - xArray[prevIndex];
            float a = (xArray[prevIndex + 1] - x) / d;
            float b = (x - xArray[prevIndex]) / d;
            float z = 0;
            if (a != 0)
                z += a * yArray[prevIndex][y];
            if (b != 0)
                z += b * yArray[prevIndex + 1][y];
            return z;
        }

        public void HasNegativeValue()
        {
            for (int i = 0; i < size; i++)
            {
                var size = yArray[i].GetSize();
                for (int j = 0; j < size; j++)
                {
                    if (yArray[i].HasNegativeValue())
                    {
                        hasNegativeValues = true;
                        return;
                    }
                }
            }
        }

        public Interpolator this[float x]
        {
            set
            {
                xArray[size] = x;
                yArray[size] = value;
                size++;
            }
        }

        public float MinX() { return xArray[0]; }

        public float MaxX() { return xArray[size - 1]; }

        public void ScaleX(float factor)
        {
            for (int i = 0; i < size; i++)
                xArray[i] *= factor;
        }

        public bool AcceptsNegativeValues() { return hasNegativeValues; }
    }

     /// <summary>
     /// two dimensional Interpolated table lookup - Generic
     /// </summary>
    public class Interpolator2D
    {
        private float[] xArray;  // must be in increasing order
        private Interpolator[] yArray;
        int size;       // number of values populated
        int prevIndex;  // used to speed up repeated evaluations with similar x values

        public Interpolator2D(int n)
        {
            xArray = new float[n];
            yArray = new Interpolator[n];
        }

        public Interpolator2D(float[] x, Interpolator[] y)
        {
            xArray = x;
            yArray = y;
            size = xArray.Length;
        }

        public Interpolator2D(Interpolator2D other)
        {
            xArray = other.xArray;
            size = other.size;
            yArray = new Interpolator[size];
            for (int i = 0; i < size; i++)
                yArray[i] = new Interpolator(other.yArray[i]);
        }

        public Interpolator2D(STFReader stf)
        {
            List<float> xlist = new List<float>();
            List<Interpolator> ilist = new List<Interpolator>();
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
            {
                xlist.Add(stf.ReadFloat(STFReader.UNITS.Any, null));
                ilist.Add(new Interpolator(stf));
            }
            stf.SkipRestOfBlock();
            int n = xlist.Count;
            if (n < 2)
                STFException.TraceWarning(stf, "Interpolator must have at least two x values.");
            xArray = new float[n];
            yArray = new Interpolator[n];
            size = n;
            for (int i = 0; i < n; i++)
            {
                xArray[i] = xlist[i];
                yArray[i] = ilist[i];
                if (i > 0 && xArray[i - 1] >= xArray[i])
                    STFException.TraceWarning(stf, " Interpolator x values must be increasing.");
            }
        }
        public float Get(float x, float y)
        {
            if (x < xArray[prevIndex] || x > xArray[prevIndex + 1])
            {
                if (x < xArray[1])
                    prevIndex = 0;
                else if (x > xArray[size - 2])
                    prevIndex = size - 2;
                else
                {
                    int i = 0;
                    int j = size - 1;
                    while (j - i > 1)
                    {
                        int k = (i + j) / 2;
                        if (xArray[k] > x)
                            j = k;
                        else
                            i = k;
                    }
                    prevIndex = i;
                }
            }
            float d = xArray[prevIndex + 1] - xArray[prevIndex];
            float a = (xArray[prevIndex + 1] - x) / d;
            float b = (x - xArray[prevIndex]) / d;
            float z = 0;
            if (a != 0)
                z += a * yArray[prevIndex][y];
            if (b != 0)
                z += b * yArray[prevIndex + 1][y];
            return z;
        }
        public Interpolator this[float x]
        {
            set
            {
                xArray[size] = x;
                yArray[size] = value;
                size++;
            }
        }
        public float MinX() { return xArray[0]; }

        public float MaxX() { return xArray[size - 1]; }

        public void ScaleX(float factor)
        {
            for (int i = 0; i < size; i++)
                xArray[i] *= factor;
        }
    }

}
