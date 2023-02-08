using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.Diagnostics;

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

        private protected ProcessState processState;
        private protected Profiler profiler;

        protected ProcessBase(GameHost gameHost, string name, int timerPeriod = 0)
        {
            this.gameHost = gameHost;
            processState = new ProcessState(name);
            profiler = new Profiler(name);
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
            processState.SignalTerminate();
            cancellationTokenSource.Cancel();
        }

        internal virtual void TriggerUpdate(GameTime gameTime)
        {
            this.gameTime = gameTime;
            processState.SignalStart();
        }

        internal void WaitForComplection()
        {
            processState.WaitTillFinished();
        }

        protected abstract void Update(GameTime gameTime);

        protected virtual void Initialize()
        { }

        protected void ThreadMethod()
        {
            profiler.SetThread();
            Initialize();
            while (!processState.Terminated)
            {
                if (timerBased)
                    Thread.Sleep(timerPeriod);
                else
                    // Wait for a new trigger command
                    processState.WaitTillStarted();
                try
                {
                    try
                    {
                        profiler.Start();
                        Update(gameTime);
                    }
                    finally
                    {
                        profiler.Stop();
                    }
                }
                catch (Exception error) when (!Debugger.IsAttached)
                {
                    // Unblock anyone waiting for us, report error and die.
                    processState.SignalTerminate();
                    gameHost.ProcessReportError(error);
                }
                // Signal finished so RenderProcess can start drawing
                processState.SignalFinish();
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
                    processState.SignalTerminate();
                    processState.Dispose();
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
