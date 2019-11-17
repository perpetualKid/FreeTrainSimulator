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

namespace Orts.Formats.OR.Files
{
    /// <summary>
    /// class TimetableGroupFileLite
    /// Creates pre-info for Multi TT file
    /// returns Description and list of pre-info per file
    /// </summary>

    public class TimetableGroupFile
    {
        public List<TimetableFile> TimeTables { get; } = new List<TimetableFile>();
        public string Description { get; private set; }

        public TimetableGroupFile(string fileName)
        {
            Description = string.Empty;
            try
            {
                using (StreamReader scrStream = new StreamReader(fileName, true))
                {
                    TimeTableGroupFileRead(fileName, scrStream);
                    if (string.IsNullOrEmpty(Description))
                        Description = fileName;
                }
            }
            catch (Exception)
            {
                Description = $"<load error: {Path.GetFileNameWithoutExtension(fileName)}>";
            }
        }

        public TimetableGroupFile(TimetableFile singleTimetableFile)
        {
            Description = singleTimetableFile.Description;
            TimeTables.Add(singleTimetableFile);
        }

        private void TimeTableGroupFileRead(string fileName, StreamReader scrStream)
        {
            // read first line - first character is separator, rest is train info
            string readLine = scrStream.ReadLine();

            while (!string.IsNullOrEmpty(readLine))
            {
                if (readLine[0] == '#')
                {
                    if (string.IsNullOrEmpty(Description))
                        Description = readLine.Substring(1);
                }
                else
                {
                    TimeTables.Add(new TimetableFile(Path.Combine(Path.GetDirectoryName(fileName), readLine)));
                }
                readLine = scrStream.ReadLine();
            }
        }

        /// <summary>
        /// extracts filenames from multiTTfile, extents names to full path
        /// </summary>
        /// <returns></returns>
        public static List<string> GetTimeTableList(string fileName)
        {
            List<string> result = new List<string>();
            using (StreamReader scrStream = new StreamReader(fileName, true))
            {
                // read first line - first character is separator, rest is train info
                string readLine = scrStream.ReadLine();

                while (!string.IsNullOrEmpty(readLine))
                {
                    if (readLine[0] != '#')
                    {
                        result.Add(Path.Combine(Path.GetDirectoryName(fileName), readLine));
                    }
                    readLine = scrStream.ReadLine();
                }
            }
            return result;
        }
    }
}
