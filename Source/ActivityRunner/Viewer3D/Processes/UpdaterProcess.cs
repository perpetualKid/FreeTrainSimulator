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

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;

namespace Orts.ActivityRunner.Viewer3D.Processes
{
    public class UpdaterProcess : IDisposable
    {
        public Profiler Profiler { get; } = new Profiler("Updater");

        private readonly ProcessState State = new ProcessState("Updater");
        private readonly GameHost game;
        private readonly Thread thread;

        private RenderFrame CurrentFrame;
        private GameTime gameTime;
        private bool disposedValue;

        public UpdaterProcess(GameHost game)
        {
            this.game = game;
            thread = new Thread(UpdaterThread);
        }

        public void Start()
        {
            thread.Start();
        }

        public void Stop()
        {
            foreach (GameComponent component in game.GameComponents)
                component.Enabled = false;
            State.SignalTerminate();
        }

        public void WaitTillFinished()
        {
            State.WaitTillFinished();
        }

        private void UpdaterThread()
        {
            Profiler.SetThread();
            game.SetThreadLanguage();

            while (!State.Terminated)
            {
                // Wait for a new Update() command
                State.WaitTillStarted();
                if (!DoUpdate())
                    return;
                // Signal finished so RenderProcess can start drawing
                State.SignalFinish();
            }
        }

        internal void StartUpdate(RenderFrame frame, GameTime gameTime)
        {
            CurrentFrame = frame;
            this.gameTime = gameTime;
            State.SignalStart();
        }

        private bool DoUpdate()
        {
            if (Debugger.IsAttached)
            {
                Update();
            }
            else
            {
                try
                {
                    Update();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception error)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Unblock anyone waiting for us, report error and die.
                    State.SignalTerminate();
                    game.ProcessReportError(error);
                    return false;
                }
            }
            return true;
        }

        public void Update()
        {
            Profiler.Start();
            try
            {
                CurrentFrame.Clear();
                for (int i = 0; i < game.GameComponents.Count; i++)
                {
                    if ((game.GameComponents[i] is GameComponent gameComponent) && gameComponent.Enabled)
                        gameComponent.Update(gameTime);
                }
                if (game.State != null)
                {
                    game.State.Update(CurrentFrame, gameTime);
                    game.RenderProcess.ComputeFPS(gameTime.ElapsedGameTime.TotalSeconds);
                    CurrentFrame.Sort();
                }
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
                    State?.Dispose();
                    // TODO: dispose managed state (managed objects)
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
