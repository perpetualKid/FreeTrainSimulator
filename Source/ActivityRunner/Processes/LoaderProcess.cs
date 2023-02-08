﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using System.Threading;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.Diagnostics;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class LoaderProcess : ProcessBase
    {
        public LoaderProcess(GameHost gameHost) : base(gameHost, "Loader")
        {
            Profiler.ProfilingData[ProcessType.Loader] = profiler;
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
        /// </remarks>
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        protected override void Update(GameTime gameTime)
        {
            gameHost.State.Load();
        }
    }
}
