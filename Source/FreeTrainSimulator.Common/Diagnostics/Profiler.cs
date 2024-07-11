using System;
using System.Diagnostics;
using System.Threading;

using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Native;

namespace FreeTrainSimulator.Common.Diagnostics
{
    public sealed class Profiler
    {
        public static EnumArray<Profiler, ProcessType> ProfilingData { get; } = new EnumArray<Profiler, ProcessType>();

        private readonly string name;
        public SmoothedData Wall { get; private set; }
        public SmoothedData CPU { get; private set; }

        private readonly Stopwatch timeTotal;
        private readonly Stopwatch timeRunning;
        private TimeSpan timeCPU;
        private TimeSpan lastCPU;
        private ProcessThread processThread;

        public Profiler(string name)
        {
            this.name = name;
            Wall = new SmoothedData();
            CPU = new SmoothedData();
            timeTotal = new Stopwatch();
            timeRunning = new Stopwatch();
            timeTotal.Start();
        }

        public void SetThread()
        {
            // This is so that you can identify threads from debuggers like Visual Studio.
            try
            {
                Thread.CurrentThread.Name = $"{name} Process";
            }
            catch (InvalidOperationException) { }

            uint threadId = NativeMethods.GetCurrentWin32ThreadId();
            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
            {
                if (thread.Id == threadId)
                {
                    processThread = thread;
                    break;
                }
            }
        }

        public void Start()
        {
            timeRunning.Start();
            if (processThread != null)
                lastCPU = processThread.TotalProcessorTime;
        }

        public void Stop()
        {
            timeRunning.Stop();
            if (processThread != null)
                timeCPU += processThread.TotalProcessorTime - lastCPU;
        }

        public void Mark()
        {
            // Collect timing data from the timers while they're running and reset them.
            bool running = this.timeRunning.IsRunning;
            double timeTotal = this.timeTotal.ElapsedMilliseconds;
            double timeRunning = this.timeRunning.ElapsedMilliseconds;
            double timeCPU = this.timeCPU.TotalMilliseconds;

            this.timeTotal.Reset();
            this.timeTotal.Start();
            this.timeRunning.Reset();
            if (running)
                this.timeRunning.Start();
            this.timeCPU = TimeSpan.Zero;

            // Calculate the Wall and CPU times from timer data.
            Wall.Update(timeTotal / 1000, 100 * timeRunning / timeTotal);
            CPU.Update(timeTotal / 1000, 100 * timeCPU / timeTotal);
        }
    }
}
