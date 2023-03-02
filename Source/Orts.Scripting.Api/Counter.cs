using System;

using Microsoft.Xna.Framework;

namespace Orts.Scripting.Api
{
    public interface IGametimeSource
    { 
        double GameTime { get; }
    }

    /// <summary>
    /// Base class for Timer and OdoMeter. Not to be used directly.
    /// </summary>
    public abstract class Counter
    {
        private double endValue;
        protected Func<double> CurrentValue { get; set; }

        public double AlarmValue { get; private set; }
        public double RemainingValue { get { return endValue - CurrentValue(); } }
        public bool Started { get; private set; }
        public void Setup(float alarmValue) { AlarmValue = alarmValue; }
        public void Start() { endValue = CurrentValue() + AlarmValue; Started = true; }
        public void Stop() { Started = false; }
        public bool Triggered { get { return Started && CurrentValue() >= endValue; } }
    }

    public class Timer : Counter
    {
        public Timer(ScriptBase script)
        {
            CurrentValue = script.GameTime;
        }

        public Timer(IGametimeSource source)
        {
            CurrentValue = () => source.GameTime;
        }
    }

    public class Odometer : Counter
    {
        public Odometer(TrainScriptBase script)
        {
            CurrentValue = script.DistanceM;
        }

        public Odometer(dynamic car)//TODO 20210923 refactor use of dynamic
        {
            CurrentValue = () => (float)car.DistanceM;
        }
    }

    public class Blinker
    {
        private double StartValue;
        protected Func<double> CurrentValue { get; set; }

        public float FrequencyHz { get; private set; }
        public bool Started { get; private set; }
        public void Setup(float frequencyHz) { FrequencyHz = frequencyHz; }
        public void Start() { StartValue = CurrentValue(); Started = true; }
        public void Stop() { Started = false; }
        public bool On { get { return Started && ((CurrentValue() - StartValue) % (1f / FrequencyHz)) * FrequencyHz * 2f < 1f; } }

        public Blinker(ScriptBase script)
        {
            CurrentValue = script.GameTime;
        }

        public Blinker(dynamic car)//TODO 20210923 refactor use of dynamic
        {
            CurrentValue = () => (float)car.Simulator.GameTime;
        }
    }
}
