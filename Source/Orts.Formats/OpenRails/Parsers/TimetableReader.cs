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

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Orts.Formats.OpenRails.Parsers
{
    /// <summary>
    /// Reads a timetable file into a 2D collection of strings which will need further processing.
    /// </summary>
    public class TimetableReader
    {
        private const string validSeparators = ";,\t";

        public Collection<string[]> Strings { get; } = new Collection<string[]>();
        public string FilePath { get; private set; }

        public TimetableReader(string filePath)
        {
            FilePath = filePath;
            using (StreamReader filestream = new StreamReader(filePath, true))
            {
                // read all lines in file
                string readLine = filestream.ReadLine();

                // Extract the separator character from start of the first line.
                char separator = readLine.Length > 0 ? readLine[0] : '\0';

                // check : only ";" or "," or "\tab" are allowed as separators
                if (!validSeparators.Contains($"{separator}", System.StringComparison.OrdinalIgnoreCase)) // Fatal error
                {
                    throw new InvalidDataException($"Expected separators are {validSeparators} and tab but found '{separator}' as first character of timetable {filePath}");
                }

                // Process the first line and then the remaining lines extracting each cell and storing it as a string in a list of arrays of strings.
                do
                {
                    Strings.Add(readLine.Split(separator).Select(s => s.Trim()).ToArray()); // Remove leading and trailing whitespace which is difficult to see in a spreadsheet and leads to parse failures which are hard to find.
                    readLine = filestream.ReadLine();
                } while (readLine != null);
            }
        }
    }
}
