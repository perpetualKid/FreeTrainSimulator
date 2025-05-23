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

using Orts.Formats.Msts.Files;

namespace Orts.ContentChecker
{
    /// <summary>
    /// Loader class for -f.raw files
    /// </summary>
    internal sealed class TerrainFlagsLoader : Loader
    {
        /// <summary> The sample count needed for loading a terrain .raw file </summary>
        private readonly int sampleCount;

        /// <summary>
        /// default constructor when not enough information is available
        /// </summary>
        public TerrainFlagsLoader() : base()
        {
            IsDependent = true;
        }

        /// <summary>
        /// Constructor giving the information this loaded depends on
        /// </summary>
        /// <param name="sampleCount">The Signal Configuration object</param>
        public TerrainFlagsLoader(int sampleCount) : this()
        {
            this.sampleCount = sampleCount;
        }

        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="file">The file that needs to be loaded</param>
        public override void TryLoading(string file)
        {
            if (sampleCount == 0)
            {
                FilesLoaded = 0;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Console.WriteLine("_f.raw files can not be loaded independently. Try the option /d");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            else
            {
                _ = TerrainFlagsFile.LoadTerrainFlagsFile(file, sampleCount);
            }
        }
    }
}
