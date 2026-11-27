// COPYRIGHT 2018 by the Open Rails project.
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

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Viewer3D.Sound
{
    /// <summary>
    /// Checks WAV files using the existing runtime audio loader.
    /// </summary>
    public static class WaveFileChecker
    {
        /// <summary>
        /// Loads and disposes a sound piece, preserving runtime WAV diagnostics.
        /// The caller must initialize OpenAL on the calling thread before checking a file.
        /// </summary>
        /// <param name="file">The WAV file to check.</param>
        public static void CheckFile(string file)
        {
            ArgumentException.ThrowIfNullOrEmpty(file);

            using (new SoundPiece(file, false, false))
            { }
        }
    }
}
