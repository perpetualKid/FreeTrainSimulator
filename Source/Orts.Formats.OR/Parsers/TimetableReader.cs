// COPYRIGHT 2014, 2015 by the Open Rails project.
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
using System.IO;

namespace Orts.Formats.OR.Parsers
{
    /// <summary>
    /// Reads a timetable file in to a 2D collection of unprocessed strings.
    /// </summary>
    public class TimetableReader
    {
        public List<string[]> Strings { get; } = new List<string[]>();
        public string FilePath { get; private set; }

        public TimetableReader(string filePath)
        {
            FilePath = filePath;
            using (var filestream = new StreamReader(filePath, true))
            {
                // read all lines in file
                string readLine = filestream.ReadLine();

                // extract separator from first line
                char separator = readLine.Length > 0 ? readLine[0] : '\0';

                // check : only ";" or "," or "\tab" are allowed as separators
                var validSeparator = separator == ';' || separator == ',' || separator == '\t';
                if (!validSeparator)
                {
                    throw new InvalidDataException($"Expected separator ';' or ','; got '{separator}' in timetable {filePath}");
                }

                // extract and store all strings
                do
                {
                    Strings.Add(readLine.Split(separator));
                    readLine = filestream.ReadLine();
                } while (readLine != null);
            }
        }
    }
}
