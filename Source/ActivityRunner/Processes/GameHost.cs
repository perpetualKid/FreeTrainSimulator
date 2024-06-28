// COPYRIGHT 2013, 2014 by the Open Rails project.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Logging;
using Orts.Graphics.Xna;
using Orts.Settings;

namespace Orts.ActivityRunner.Processes
{
    /// <summary>
    /// Provides the foundation for running the game.
    /// </summary>
    public sealed class GameHost : Game
    {
        internal SystemProcess SystemProcess { get; }

        /// <summary>
        /// Gets the <see cref="UserSettings"/> for the game.
        /// </summary>
        public UserSettings Settings { get; }

        /// <summary>
        /// Exposes access to the <see cref="RenderProcess"/> for the game.
        /// </summary>
        internal RenderProcess RenderProcess { get; }

        /// <summary>
        /// Exposes access to the <see cref="UpdaterProcess"/> for the game.
        /// </summary>
        internal UpdaterProcess UpdaterProcess { get; }

        /// <summary>
        /// Exposes access to the <see cref="LoaderProcess"/> for the game.
        /// </summary>
        internal LoaderProcess LoaderProcess { get; }

        /// <summary>
        /// Exposes access to the <see cref="SoundProcess"/> for the game.
        /// </summary>
        internal SoundProcess SoundProcess { get; }

        /// <summary>
        /// Exposes access to the <see cref="WebServer"/> for the game.
        /// </summary>
        internal WebServerProcess WebServerProcess { get; }

        public EnumArray<INameValueInformationProvider, DiagnosticInfo> SystemInfo { get; } = new EnumArray<INameValueInformationProvider, DiagnosticInfo>();

        /// <summary>
        /// Gets the current <see cref="GameState"/>, if there is one, or <c>null</c>.
        /// </summary>
        internal GameState State => gameStates.Count > 0 ? gameStates.Peek() : null;

        private readonly Stack<GameState> gameStates;

        public GameComponentCollection GameComponents { get; } = new GameComponentCollection();

        /// <summary>
        /// Initializes a new instance of the <see cref="GameHost"/> based on the specified <see cref="UserSettings"/>.
        /// </summary>
        /// <param name="settings">The <see cref="UserSettings"/> for the game to use.</param>
        public GameHost(UserSettings settings)
        {
            Settings = settings;
            Exiting += Game_Exiting;
            RenderProcess = new RenderProcess(this);
            UpdaterProcess = new UpdaterProcess(this);
            LoaderProcess = new LoaderProcess(this);
            SoundProcess = new SoundProcess(this);
            WebServerProcess = new WebServerProcess(this);
            gameStates = new Stack<GameState>();
            SystemProcess = new SystemProcess(this);
        }

        protected override void Initialize()
        {
            base.Initialize();
            RenderProcess.Initialize();
        }

        protected override void LoadContent()
        {
            base.LoadContent();
        }

        protected override void BeginRun()
        {
            // At this point, GraphicsDevice is initialized and set up.
            SoundProcess.Start();
            LoaderProcess.Start();
            UpdaterProcess.Start();
            RenderProcess.Start();
            WebServerProcess.Start();
            SystemProcess.Start();
            base.BeginRun();
        }

        protected override void Update(GameTime gameTime)
        {
            // The first Update() is called before the window is displayed, with a gameTime == 0. The second is called
            // after the window is displayed.
            //if (!addedComponents.IsEmpty)
            //    while (addedComponents.TryDequeue(out GameComponent component))
            //        component.Initialize();
            if (State == null)
                Exit();
            else
            {
                RenderProcess.Update(gameTime);
                SystemInfo[DiagnosticInfo.System].DetailInfo["Resolution"] = Window.ClientBounds.ToString();// need to update from main/render thread otherwise results are invalid
            }
//            base.Update(gameTime);
        }

        protected override bool BeginDraw()
        {
            RenderProcess.BeginDraw();
            return true;
        }

        protected override void Draw(GameTime gameTime)
        {
            RenderProcess.Draw(gameTime);
            base.Draw(gameTime);
        }

        protected override void EndDraw()
        {
            RenderProcess.EndDraw();
            base.EndDraw();
        }

        protected override void EndRun()
        {
            base.EndRun();
            RenderProcess.Stop();
            UpdaterProcess.Stop();
            LoaderProcess.Stop();
            SoundProcess.Stop();
            WebServerProcess.Stop();
            SystemProcess.Stop();
        }

        private void Game_Exiting(object sender, EventArgs e)
        {
            while (State != null)
                PopState();
        }

        internal void PushState(GameState state)
        {
            state.Game = this;
            gameStates.Push(state);
            Trace.TraceInformation($"Game.PushState({state.GetType().Name})  {string.Join(" | ", gameStates.Select(s => s.GetType().Name).ToArray())}");
        }

        internal void PopState()
        {
            State.Dispose();
            gameStates.Pop();
            Trace.TraceInformation($"Game.PopState()  {string.Join(" | ", gameStates.Select(s => s.GetType().Name).ToArray())}");
        }

        internal void ReplaceState(GameState state)
        {
            if (State != null)
            {
                State.Dispose();
                gameStates.Pop();
            }
            state.Game = this;
            gameStates.Push(state);
            Trace.TraceInformation($"Game.ReplaceState({state.GetType().Name})  {string.Join(" | ", gameStates.Select(s => s.GetType().Name).ToArray())}");
        }

        /// <summary>
        /// Reports an <see cref="Exception"/> to the log file and/or user, exiting the game in the process.
        /// </summary>
        /// <param name="error">The <see cref="Exception"/> to report.</param>
        public void ProcessReportError(Exception error)
        {
            // Log the error first in case we're burning.
            Trace.WriteLine(new FatalException(error));
            // Show the user that it's all gone horribly wrong.
            if (Settings.ShowErrorDialogs)
            {
                string errorSummary = error?.GetType().FullName + ": " + error.Message;
                string logFile = Path.Combine(Settings.LoggingPath, Settings.LoggingFilename);
                DialogResult openTracker = MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                        $"    {errorSummary}\n\n" +
                        $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ProductName} by reporting this error in our bug tracker at https://github.com/perpetualKid/ORTS-MG/issues and attaching the log file {logFile}.\n\n" +
                        ">>> Click OK to report this error on the GitHub bug tracker <<<",
                        $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (openTracker == DialogResult.OK)
                    Orts.Common.Info.SystemInfo.OpenBrowser(LoggingUtil.BugTrackerUrl);
            }
            // Stop the world!
            Exit();
        }
    }
}
