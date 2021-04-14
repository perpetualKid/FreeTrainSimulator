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
    public class UpdaterProcess
    {
        public Profiler Profiler { get; } = new Profiler("Updater");

        private readonly ProcessState State = new ProcessState("Updater");
        private readonly Game game;
        private readonly Thread thread;
        private readonly WatchdogToken watchdogToken;

        public GameComponentCollection GameComponents { get; } = new GameComponentCollection();
        private RenderFrame CurrentFrame;
        private GameTime gameTime;

        public UpdaterProcess(Game game)
        {
            this.game = game;
            thread = new Thread(UpdaterThread);
            watchdogToken = new WatchdogToken(thread);
        }

        public void Start()
        {
            game.WatchdogProcess.Register(watchdogToken);
            thread.Start();
        }

        public void Stop()
        {
            foreach (GameComponent component in GameComponents)
                component.Enabled = false;
            game.WatchdogProcess.Unregister(watchdogToken);
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

            while (true)
            {
                // Wait for a new Update() command
                State.WaitTillStarted();
                if (State.Terminated)
                    break;
                try
                {
                    if (!DoUpdate())
                        return;
                }
                finally
                {
                    // Signal finished so RenderProcess can start drawing
                    State.SignalFinish();
                }
            }
        }

        //[CallOnThread("Render")]
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
                catch (Exception error)
                {
                    // Unblock anyone waiting for us, report error and die.
                    State.SignalTerminate();
                    game.ProcessReportError(error);
                    return false;
                }
            }
            return true;
        }

        //[CallOnThread("Updater")]
        public void Update()
        {
            Profiler.Start();
            try
            {
                watchdogToken.Ping();
                CurrentFrame.Clear();
                foreach (GameComponent component in GameComponents)
                    if (component.Enabled)
                        component.Update(gameTime);
                if (game.State != null)
                {
                    game.State.Update(CurrentFrame, gameTime.TotalGameTime.TotalSeconds, gameTime);
                    CurrentFrame.Sort();
                }
            }
            finally
            {
                Profiler.Stop();
            }
        }
    }
}
