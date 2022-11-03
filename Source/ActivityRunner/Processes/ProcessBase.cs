using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes
{
    internal abstract class ProcessBase : IDisposable
    {
        private protected readonly Thread thread;
        private protected readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool disposedValue;
        private protected readonly GameHost gameHost;
        private readonly bool timerBased;
        private readonly int timerPeriod;
        private GameTime gameTime;

        public ProcessState ProcessState { get; }
        public Profiler Profiler { get; }

        protected ProcessBase(GameHost gameHost, string name, int timerPeriod = 0)
        {
            this.gameHost = gameHost;
            ProcessState = new ProcessState(name);
            Profiler = new Profiler(name);
            thread = new Thread(ThreadMethod);
            if (timerPeriod > 0)
            {
                timerBased = true;
                this.timerPeriod = timerPeriod;
            }
        }

        internal virtual void Start()
        {
            thread.Start();
        }

        internal virtual void Stop()
        {
            ProcessState.SignalTerminate();
            cancellationTokenSource.Cancel();
        }

        internal virtual void TriggerUpdate(GameTime gameTime)
        {
            this.gameTime = gameTime;
            ProcessState.SignalStart();
        }

        protected abstract void Update(GameTime gameTime);

        protected virtual void Initialize()
        { }

        protected void ThreadMethod()
        {
            Profiler.SetThread();
            Initialize();
            while (!ProcessState.Terminated)
            {
                if (timerBased)
                    Thread.Sleep(timerPeriod);
                else
                // Wait for a new trigger command
                ProcessState.WaitTillStarted();
                try
                {
                    try
                    {
                        Profiler.Start();
                        Update(gameTime);
                    }
                    finally
                    {
                        Profiler.Stop();
                    }
                }
                catch (Exception error) when (!Debugger.IsAttached)
                {
                    // Unblock anyone waiting for us, report error and die.
                    ProcessState.SignalTerminate();
                    gameHost.ProcessReportError(error);
                }
                // Signal finished so RenderProcess can start drawing
                ProcessState.SignalFinish();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    ProcessState.SignalTerminate();
                    ProcessState.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
