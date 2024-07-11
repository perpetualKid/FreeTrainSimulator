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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class UpdaterProcess : ProcessBase
    {
        private RenderFrame CurrentFrame;

        public UpdaterProcess(GameHost gameHost): base(gameHost, "Updater")
        {
            Profiler.ProfilingData[ProcessType.Updater] = profiler;
        }


        internal override void Stop()
        {
            foreach (GameComponent component in gameHost.Components)
                component.Enabled = false;
            base.Stop();
        }

        internal void TriggerUpdate(RenderFrame frame, GameTime gameTime)
        {
            CurrentFrame = frame;
            base.TriggerUpdate(gameTime);
        }

        protected override void Update(GameTime gameTime)
        {
            CurrentFrame.Clear();
            for (int i = 0; i < gameHost.Components.Count; i++)
            {
                if (gameHost.Components[i] is GameComponent gameComponent && gameComponent.Enabled)
                    gameComponent.Update(gameTime);
            }
            if (gameHost.State != null)
            {
                gameHost.State.Update(CurrentFrame, gameTime);
                CurrentFrame.Sort();
            }
        }
    }
}
