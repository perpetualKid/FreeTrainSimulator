// COPYRIGHT 2013 by the Open Rails project.
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
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.State;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;
using Orts.Formats.Msts;

namespace Orts.ActivityRunner.Processes
{
    /// <summary>
    /// Represents a single state for the game to be in (e.g. loading, running, in menu).
    /// </summary>
    internal abstract class GameState : IDisposable, ISaveStateApi<GameSaveState>
    {
        private bool disposedValue;

        private protected static ActivityType activityType;
        private protected static string[] data;

        internal GameHost Game { get; set; }

        /// <summary>
        /// Called just before a frame is drawn.
        /// </summary>
        /// <param name="frame">The <see cref="RenderFrame"/> containing everything to be drawn.</param>
        internal virtual void BeginRender(RenderFrame frame)
        {
        }

        /// <summary>
        /// Called just after a frame is drawn.
        /// </summary>
        /// <param name="frame">The <see cref="RenderFrame"/> containing everything that was drawn.</param>
        internal virtual void EndRender(RenderFrame frame)
        {
        }

        /// <summary>
        /// Called to update the game and populate a new <see cref="RenderFrame"/>.
        /// </summary>
        /// <param name="frame">The new <see cref="RenderFrame"/> that needs populating.</param>
        /// <param name="totalRealSeconds">The total number of real-world seconds which have elapsed since the game was started.</param>
        internal virtual void Update(RenderFrame frame, GameTime gameTime)
        {
            // By default, every update tries to trigger a load.
            if (Game.LoaderProcess.Finished)
                Game.LoaderProcess.TriggerUpdate(gameTime);
        }

        /// <summary>
        /// Called to load new content as and when necessary.
        /// </summary>
        internal virtual ValueTask Load()
        {
            return ValueTask.CompletedTask;
        }

        internal virtual ValueTask Save()
        {
            return ValueTask.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="GameState"/> class.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public virtual ValueTask<GameSaveState> Snapshot()
        {
            throw new NotImplementedException();
        }

        public virtual ValueTask Restore(GameSaveState saveState)
        {
            throw new NotImplementedException();
        }
    }
}
