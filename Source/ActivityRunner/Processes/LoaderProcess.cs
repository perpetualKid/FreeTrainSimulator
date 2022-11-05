// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Diagnostics;
using System.Threading;


namespace Orts.ActivityRunner.Processes
{
    public class LoaderProcess : IDisposable
    {
        public Profiler Profiler { get; } = new Profiler("Loader");
        private readonly ProcessState processState = new ProcessState("Loader");
        private readonly GameHost game;
        private readonly Thread thread;
        private readonly CancellationTokenSource cancellationTokenSource;
        private bool disposedValue;

        public LoaderProcess(GameHost game)
        {
            this.game = game;
            thread = new Thread(LoaderThread);
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            thread.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
            processState.SignalTerminate();
        }

        public bool Finished => processState.Finished;

        /// <summary>
        /// Returns a token (copyable object) which can be queried for the cancellation (termination) of the loader.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All loading code should periodically (e.g. between loading each file) check the token and exit as soon
        /// as it is cancelled (<see cref="CancellationToken.IsCancellationRequested"/>).
        /// </para>
        /// <para>
        /// Reading <see cref="CancellationToken.IsCancellationRequested"/> causes the WatchdogToken to
        /// be pinged, informing the WatchdogProcess that the loader is still responsive. Therefore the
        /// remarks about the WatchdogToken.Ping() method apply to the token regarding when it should
        /// and should not be used.
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        public void WaitTillFinished()
        {
            processState.WaitTillFinished();
        }

        private void LoaderThread()
        {
            Profiler.SetThread();
            game.SetThreadLanguage();

            while (true)
            {
                // Wait for a new Update() command
                processState.WaitTillStarted();
                if (processState.Terminated)
                    break;
                try
                {
                    if (!DoLoad())
                        return;
                }
                finally
                {
                    // Signal finished so RenderProcess can start drawing
                    processState.SignalFinish();
                }
            }
        }

        internal void StartLoad()
        {
            Debug.Assert(processState.Finished);
            processState.SignalStart();
        }

        private bool DoLoad()
        {
            if (Debugger.IsAttached)
            {
                Load();
            }
            else
            {
                try
                {
                    Load();
                }
                catch (Exception error)
                {
                    // Unblock anyone waiting for us, report error and die.
                    cancellationTokenSource.Cancel();
                    processState.SignalTerminate();
                    game.ProcessReportError(error);
                    return false;
                }
            }
            return true;
        }

        public void Load()
        {
            Profiler.Start();
            try
            {
                game.State.Load();
            }
            finally
            {
                Profiler.Stop();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                    processState?.Dispose();
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
