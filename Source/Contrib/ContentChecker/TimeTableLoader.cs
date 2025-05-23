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
using System.IO;

using Orts.Formats.OpenRails.Files;
using Orts.Formats.OpenRails.Parsers;

namespace Orts.ContentChecker
{
    /// <summary>
    /// Loader class for .s files
    /// </summary>
    internal sealed class TimetableLoader : Loader
    {
        /// <summary>
        /// Try to load the file.
        /// Possibly this might raise an exception. That exception is not caught here
        /// </summary>
        /// <param name="file">The file that needs to be loaded</param>
        public override void TryLoading(string file)
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            string extension = Path.GetExtension(file).ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
            if (extension.Contains("table", StringComparison.OrdinalIgnoreCase))
            {
                _ = new TimetableGroupFile(file);
            }
            else
            {
                _ = new TimetableReader(file);

            }
        }
    }
}
