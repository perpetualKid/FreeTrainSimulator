﻿// COPYRIGHT 2011 by the Open Rails project.
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
using System.IO;

namespace Orts.Common.Calc
{
    public enum IntegratorMethod
    {
        EulerBackward = 0,
        EulerBackMod = 1,
        EulerForward = 2,
        RungeKutta2 = 3,
        RungeKutta4 = 4,
        NewtonRhapson = 5,
        AdamsMoulton = 6
    }

    /// <summary>
    /// Integrator class covers discrete integrator methods
    /// Some forward method needs to be implemented
    /// </summary>
    public class Integrator
    {
        private readonly double[] previousValues = new double[100];
        private readonly double[] previousStep = new double[100];

        private double derivation;
        private double prevDerivation;

        private double max;
        private double min;
        private double oldTime;

        public IntegratorMethod Method { get; set; }

        /// <summary>
        /// Initial condition acts as a Value at the beginning of the integration
        /// </summary>
        public double InitialCondition { set; get; }
        /// <summary>
        /// Integrated value
        /// </summary>
        public double Value { get; private set; }
        /// <summary>
        /// Upper limit of the Value. Cannot be smaller than Min. Max is considered only if IsLimited is true
        /// </summary>
        public double Max
        {
            set
            {
                if (max <= min)
                    throw new InvalidOperationException("Maximum must be greater than minimum");
                max = value;

            }
            get => max;
        }
        /// <summary>
        /// Lower limit of the Value. Cannot be greater than Max. Min is considered only if IsLimited is true
        /// </summary>
        public double Min
        {
            set
            {
                if (max <= min)
                    throw new InvalidOperationException("Minimum must be smaller than maximum");
                min = value;
            }
            get => min;
        }
        /// <summary>
        /// Determines limitting according to Max and Min values
        /// </summary>
        public bool IsLimited { set; get; }

        /// <summary>
        /// Minimal step of integration
        /// </summary>
        public double MinStep { set; get; }
        public bool IsStepDividing { set; get; }

        private int waitBeforeSpeedingUp;
        public int NumOfSubstepsPS { get; private set; } = 1;

        /// <summary>
        /// Max count of substeps when timespan dividing
        /// </summary>
        public int MaxSubsteps { set; get; }

        public double Error { set; get; }

        public Integrator() : this(0.0)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="initCondition">Initial condition of integration</param>
        public Integrator(double initCondition) : this(initCondition, IntegratorMethod.EulerBackward)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="initCondition">Initial condition of integration</param>
        /// <param name="method">Method of integration</param>
        public Integrator(double initCondition, IntegratorMethod method)
        {
            Method = method;
            MinStep = 0.001;
            max = 1000.0;
            min = -1000.0;
            IsLimited = false;
            InitialCondition = initCondition;
            Value = InitialCondition;
            MaxSubsteps = 300;
            for (int i = 0; i < previousValues.Length; i++)
                previousValues[i] = initCondition;
            oldTime = 0.0;
            Error = 0.001;
        }

        public Integrator(Integrator source)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            Method = source.Method;
            MinStep = source.MinStep;
            max = source.max;
            min = source.min;
            IsLimited = source.IsLimited;
            Value = source.Value;
            InitialCondition = source.InitialCondition;
            MaxSubsteps = source.MaxSubsteps;
            for (int i = 0; i < previousValues.Length; i++)
                previousValues[i] = InitialCondition;
            oldTime = 0.0;
            Error = source.Error;
        }

        /// <summary>
        /// Resets the Value to its InitialCondition
        /// </summary>
        public void Reset()
        {
            Value = InitialCondition;
        }

        /// <summary>
        /// Integrates given value with given time span
        /// </summary>
        /// <param name="timeSpan">Integration step or timespan in seconds</param>
        /// <param name="value">Value to integrate</param>
        /// <returns>Value of integration in the next step (t + timeSpan)</returns>
        public double Integrate(double timeSpan, double value)
        {
            double end = timeSpan;
            double k1, k2, k3;

            //Skip when timeSpan is less then zero
            if (timeSpan <= 0.0f)
            {
                return Value;
            }

            int count;
            //if (timeSpan > MinStep)
            if (Math.Abs(prevDerivation) > Error)
            {
                //count = 2 * Convert.ToInt32(Math.Round((timeSpan) / MinStep, 0));
                count = ++NumOfSubstepsPS;
                if (count > MaxSubsteps)
                    count = MaxSubsteps;
                waitBeforeSpeedingUp = 100;
                //if (numOfSubstepsPS > (MaxSubsteps / 2))
                //    Method = IntegratorMethods.EulerBackMod;
                //else
                //    Method = IntegratorMethods.RungeKutta4;
            }
            else
            {
                if (--waitBeforeSpeedingUp <= 0)    //wait for a while before speeding up the integration
                {
                    count = --NumOfSubstepsPS;
                    if (count < 1)
                        count = 1;

                    waitBeforeSpeedingUp = 10;      //not so fast ;)
                }
                else
                    count = NumOfSubstepsPS;
                //IsStepDividing = false;
            }

            timeSpan /= count;
            NumOfSubstepsPS = count;

            IsStepDividing = count > 1;

            double step;
            #region SOLVERS
            //while ((step += timeSpan) <= end)
            for (step = timeSpan; step <= end; step += timeSpan)
            {
                switch (Method)
                {
                    case IntegratorMethod.EulerBackward:
                        Value += (derivation = timeSpan * value);
                        break;
                    case IntegratorMethod.EulerBackMod:
                        Value += (derivation = timeSpan / 2.0 * (previousValues[0] + value));
                        previousValues[0] = value;
                        break;
                    case IntegratorMethod.EulerForward:
                        throw new NotImplementedException("Not implemented yet!");

                    case IntegratorMethod.RungeKutta2:
                        k1 = Value + timeSpan / 2 * value;
                        k2 = 2 * (k1 - Value) / timeSpan;
                        Value += (derivation = timeSpan * k2);
                        break;
                    case IntegratorMethod.RungeKutta4:
                        k1 = timeSpan * value;
                        k2 = k1 + timeSpan / 2.0 * value;
                        k3 = k1 + timeSpan / 2.0 * k2;
                        double k4 = timeSpan * k3;
                        Value += (derivation = (k1 + 2.0 * k2 + 2.0 * k3 + k4) / 6.0);
                        break;
                    case IntegratorMethod.NewtonRhapson:
                        throw new NotImplementedException("Not implemented yet!");

                    case IntegratorMethod.AdamsMoulton:
                        //prediction
                        double predicted = Value + timeSpan / 24.0 * (55.0 * previousValues[0] - 59.0 * previousValues[1] + 37.0 * previousValues[2] - 9.0 * previousValues[3]);
                        //correction
                        Value += timeSpan / 24.0 * (9.0 * predicted + 19.0 * previousValues[0] - 5.0 * previousValues[1] + previousValues[2]);
                        for (int i = previousStep.Length - 1; i > 0; i--)
                        {
                            previousStep[i] = previousStep[i - 1];
                            previousValues[i] = previousValues[i - 1];
                        }
                        previousValues[0] = value;
                        previousStep[0] = timeSpan;
                        break;
                    default:
                        throw new NotImplementedException("Not implemented yet!");

                }
                //To make sure the loop exits
                //if (count-- < 0)
                //    break;
            }

            #endregion

            prevDerivation = derivation;

            //Limit if enabled
            return IsLimited ? (Value <= min) ? (Value = min) : ((Value >= max) ? (Value = max) : Value) : Value;
        }

        /// <summary>
        /// Integrates given value with given time span
        /// </summary>
        /// <param name="timeSpan">Integration step or timespan in seconds</param>
        /// <param name="value">Value to integrate</param>
        /// <returns>Value of integration in the next step (t + timeSpan)</returns>
        public double Integrate(double timeSpan, Func<double, double> value)
        {
            ArgumentNullException.ThrowIfNull(value);

            double end = timeSpan;
            double k1, k2, k3;

            //Skip when timeSpan is less then zero
            if (timeSpan <= 0.0f)
            {
                return Value;
            }
            int count;
            if (Math.Abs(prevDerivation) > Error)
            {
                count = ++NumOfSubstepsPS;
                if (count > MaxSubsteps)
                    count = MaxSubsteps;
                waitBeforeSpeedingUp = 100;
            }
            else
            {
                if (--waitBeforeSpeedingUp <= 0)    //wait for a while before speeding up the integration
                {
                    count = Math.Max(--NumOfSubstepsPS, 1);
                    waitBeforeSpeedingUp = 10;      //not so fast ;)
                }
                else
                    count = NumOfSubstepsPS;
            }

            timeSpan /= count;
            NumOfSubstepsPS = count;
            IsStepDividing = count > 1;


            double step;
            #region SOLVERS
            //while ((step += timeSpan) <= end)
            for (step = timeSpan; step <= end; step += timeSpan)
            {
                switch (Method)
                {
                    case IntegratorMethod.EulerBackward:
                        Value += (derivation = timeSpan * value(Value));
                        break;
                    case IntegratorMethod.EulerBackMod:
                        Value += (derivation = timeSpan / 2.0f * (previousValues[0] + value(Value)));
                        previousValues[0] = value(Value);
                        break;
                    case IntegratorMethod.EulerForward:
                        throw new NotImplementedException("Not implemented yet!");

                    case IntegratorMethod.RungeKutta2:
                        k1 = Value + timeSpan / 2 * value(Value);
                        k2 = 2 * (k1 - Value) / timeSpan;
                        Value += (derivation = timeSpan * k2);
                        break;
                    case IntegratorMethod.RungeKutta4:
                        k1 = value(Value);
                        k2 = value(Value + k1 * timeSpan / 2.0f);
                        k3 = value(Value + k2 * timeSpan / 2.0f);
                        double k4 = value(Value + k3 * timeSpan);
                        Value += (derivation = (k1 + 2.0f * k2 + 2.0f * k3 + k4) * timeSpan / 6.0f);
                        break;
                    case IntegratorMethod.NewtonRhapson:
                        throw new NotImplementedException("Not implemented yet!");

                    case IntegratorMethod.AdamsMoulton:
                        //prediction
                        double predicted = Value + timeSpan / 24.0f * (55.0f * previousValues[0] - 59.0f * previousValues[1] + 37.0f * previousValues[2] - 9.0f * previousValues[3]);
                        //correction
                        Value += timeSpan / 24.0f * (9.0f * predicted + 19.0f * previousValues[0] - 5.0f * previousValues[1] + previousValues[2]);
                        for (int i = previousStep.Length - 1; i > 0; i--)
                        {
                            previousStep[i] = previousStep[i - 1];
                            previousValues[i] = previousValues[i - 1];
                        }
                        previousValues[0] = value(Value);
                        previousStep[0] = timeSpan;
                        break;
                    default:
                        throw new NotImplementedException("Not implemented yet!");

                }
                //To make sure the loop exits
                //if (count-- < 0)
                //    break;
            }

            #endregion

            prevDerivation = derivation;

            //Limit if enabled
            return IsLimited ? Math.Clamp(Value, min, max) : Value;
        }

        /// <summary>
        /// Integrates given value in time. TimeSpan (integration step) is computed internally.
        /// </summary>
        /// <param name="clockSeconds">Time value in seconds</param>
        /// <param name="value">Value to integrate</param>
        /// <returns>Value of integration in elapsedClockSeconds time</returns>
        public double TimeIntegrate(double clockSeconds, double value)
        {
            double timeSpan = clockSeconds - oldTime;
            oldTime = clockSeconds;
            Value += timeSpan * value;
            return IsLimited ? Math.Clamp(Value, min, max) : Value;
        }

        public void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));
            outf.Write(Value);
        }

        public void Restore(BinaryReader inf)
        {
            if (null == inf)
                throw new ArgumentNullException(nameof(inf));
            Value = inf.ReadDouble();

            for (int i = 0; i < 4; i++)
                previousValues[i] = Value;
        }

    }
}
