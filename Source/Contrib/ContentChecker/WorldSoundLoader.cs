﻿// COPYRIGHT 2018 by the Open Rails project.
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

using System;
using System.Collections.Generic;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.ContentChecker
{
    /// <summary>
    /// Loader class for .s files
    /// </summary>
    internal sealed class WorldSoundLoader : Loader
    {
        /// <summary> The track database needed for loading a world-sound file </summary>
        private TrackDB trackDB;

        /// <summary>
        /// default constructor when not enough information is available
        /// </summary>
        public WorldSoundLoader() : base()
        {
            IsDependent = true;
        }

        /// <summary>
        /// Constructor giving the information this loaded depends on
        /// </summary>
        /// <param name="tdb">The Signal Configuration object</param>
        public WorldSoundLoader(TrackDB tdb) : this()
        {
            trackDB = tdb;
        }

        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="file">The file that needs to be loaded</param>
        public override void TryLoading(string file)
        {
            _ = new WorldSoundFile(file, trackDB?.TrackItems.Count ?? 100000);
        }
    }
}
