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

// OR Timetable file is csv file, with following main layout :
// Top Row : train information
// special items in toprow : 
//    #comment : general comment column (ignored except for first cell with row and column set to #comment)
//    <empty>  : continuation of train from previous column
//
// First column : station names
// special items in first column :
//    #comment   : general comment column (ignored except for first cell with row and column set to #comment)
//    #consist   : train consist
//    #path      : train path
//    #direction : Up or Down
//

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

using Orts.Formats.OpenRails.Models;

namespace Orts.Formats.OpenRails.Files
{
    /// <summary>
    /// class TimetableFileLite
    /// provides pre-information for menu
    /// extracts only description and list of trains
    /// </summary>

    public class TimetableFile
    {
        public Collection<TrainInformation> Trains { get; } = new Collection<TrainInformation>();
        public string Description { get; private set; }
        public string Briefing { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath"></param>

        public TimetableFile(string fileName)
        {
            string separator = string.Empty;
            try
            {
                using (StreamReader scrStream = new StreamReader(fileName, true))
                {
                    PreliminaryRead(fileName, scrStream, separator);
                }
            }
            catch (Exception ex) when (ex is Exception)
            {
                Trace.TraceInformation("Load error for timetable {0} : {1}", Path.GetFileNameWithoutExtension(fileName), ex.ToString());
                Description = $"<load error: {Path.GetFileNameWithoutExtension(fileName)}>";
            }
        }

        public override string ToString()
        {
            return Description;
        }

        private void PreliminaryRead(string fileName, StreamReader scrStream, string separator)
        {
            string readLine;
            string restLine;
            int firstCommentColumn = -1;

            // read first line - first character is separator, rest is train info
            readLine = scrStream.ReadLine();
            if (string.IsNullOrEmpty(separator))
                separator = readLine.Substring(0, 1);

            restLine = readLine.Substring(1);
            string[] separators = new string[] { separator };
            string[] parts = restLine.Split(separators, StringSplitOptions.None);

            int columnIndex = 1;
            foreach (string header in parts)
            {
                if (header.Equals("#comment", StringComparison.OrdinalIgnoreCase))
                {
                    if (firstCommentColumn < 0)
                        firstCommentColumn = columnIndex;
                }
                else if (!string.IsNullOrEmpty(header) && !header.Contains("$static", StringComparison.OrdinalIgnoreCase))
                {
                    Trains.Add(new TrainInformation(columnIndex, header));
                }
                columnIndex++;
            }

            // process comment row - cell at first comment row and column is description
            // process path and consist row

            Description = fileName;

            bool descFound = false;
            bool pathFound = false;
            bool consistFound = false;
            bool startFound = false;
            bool briefingFound = false;

            readLine = scrStream.ReadLine();

            while (readLine != null && (!descFound || !pathFound || !consistFound || !startFound || !briefingFound))
            {
                parts = readLine.Split(separators, StringSplitOptions.None);

                if (!descFound && firstCommentColumn > 0)
                {
                    if (parts[0].Equals("#comment", StringComparison.OrdinalIgnoreCase))
                    {
                        Description = parts[firstCommentColumn];
                        descFound = true;
                    }
                }
                if (!pathFound)
                {
                    if (parts[0].Trim().Substring(0, 5).Equals("#path", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (TrainInformation train in Trains)
                            train.Path = parts[train.Column];
                        pathFound = true;
                    }
                }
                if (!consistFound)
                {
                    if (parts[0].Equals("#consist", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (TrainInformation train in Trains)
                        {
                            train.Consist = parts[train.Column];
                            train.LeadingConsist = ExtractConsist(train.Consist, out bool reverse);
                            train.ReverseConsist = reverse;
                        }
                        consistFound = true;
                    }
                }
                if (!startFound)
                {
                    if (parts[0].Equals("#start", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (TrainInformation train in Trains)
                            train.StartTime = parts[train.Column];
                        startFound = true;
                    }
                }
                if (!briefingFound)
                {
                    if (parts[0].Equals("#briefing", StringComparison.OrdinalIgnoreCase))
                    {
                        briefingFound = true;
                        // Newlines "\n" cannot be emdedded in CSV files, so HTML breaks "<br>" are used instead.
                        Briefing = parts[1].Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
                        foreach (TrainInformation train in Trains)
                            train.Briefing = parts[train.Column].Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
                    }
                }
                readLine = scrStream.ReadLine();
            }
        }

        private static string ExtractConsist(string consistDef, out bool reverse)
        {
            reverse = false;
            string reqString = consistDef;
            string consistProc = consistDef.Trim();

            if (!string.IsNullOrEmpty(consistProc) && consistProc[0] == '<')
            {
                int endIndex = consistProc.IndexOf('>', StringComparison.OrdinalIgnoreCase);
                if (endIndex < 0)
                {
                    reqString = consistProc.Substring(1);
                    consistProc = string.Empty;
                }
                else
                {
                    reqString = consistProc.Substring(1, endIndex - 1);
                    consistProc = consistProc.Substring(endIndex + 1).Trim();
                }
            }
            else
            {
                int plusIndex = consistProc.IndexOf('+', StringComparison.OrdinalIgnoreCase);
                if (plusIndex > 0)
                {
                    reqString = consistProc.Substring(0, plusIndex - 1);

                    int sepIndex = consistDef.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                    if (sepIndex > 0)
                    {
                        consistProc = consistDef.Substring(sepIndex).Trim();
                    }
                    else
                    {
                        consistProc = string.Empty;
                    }
                }
                else
                {
                    reqString = consistDef;

                    int sepIndex = consistDef.IndexOf('$', StringComparison.OrdinalIgnoreCase);
                    if (sepIndex > 0)
                    {
                        consistProc = consistDef.Substring(sepIndex).Trim();
                    }
                    else
                    {
                        consistProc = string.Empty;
                    }
                }
            }

            if (!string.IsNullOrEmpty(consistProc) && consistProc[0] == '$')
            {
                reverse = consistProc.Substring(1, 7).Equals("reverse", StringComparison.OrdinalIgnoreCase);
            }
            return (reverse && reqString.EndsWith("$reverse", StringComparison.OrdinalIgnoreCase) ? reqString[..^8] : reqString).Trim();
        }
    }
}
