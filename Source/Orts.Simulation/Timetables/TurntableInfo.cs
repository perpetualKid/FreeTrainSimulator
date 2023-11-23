// COPYRIGHT 2014 by the Open Rails project.
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

// This code processes the Timetable definition and converts it into playable train information
//

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Orts.Formats.OR.Parsers;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class to collect pool details
    /// </summary>
    public class TurntableInfo : PoolInfo
    {
        private const string significantFileName = "turntable";

        //================================================================================================//
        /// <summary>
        /// Read pool files
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public static Dictionary<string, TimetableTurntablePool> ProcessTurntables(string fileName, CancellationToken cancellationToken)
        {
            Dictionary<string, TimetableTurntablePool> turntables = new Dictionary<string, TimetableTurntablePool>();

            // get filenames to process
            Trace.Write("\n");
            foreach (string filePath in EnumeratePoolFiles(fileName, significantFileName))
            {
                // get contents as strings
                Trace.Write("Turntable File : " + filePath + "\n");
                var turntableInfo = new TimetableReader(filePath);

                // read lines from input until 'Name' definition is found
                int lineindex = 1;
                while (lineindex < turntableInfo.Strings.Count && !cancellationToken.IsCancellationRequested)
                {
                    switch (turntableInfo.Strings[lineindex][0].ToLower().Trim())
                    {
                        // skip comment
                        case "#comment":
                            lineindex++;
                            break;

                        // process name
                        // do not increase lineindex as that is done in called method
                        case "#name":
                            TimetableTurntablePool newTurntable = new TimetableTurntablePool(turntableInfo, ref lineindex, Simulator.Instance);
                            // store if valid pool
                            if (!string.IsNullOrEmpty(newTurntable.PoolName))
                            {
                                if (!turntables.TryAdd(newTurntable.PoolName, newTurntable))
                                {
                                    Trace.TraceWarning("Duplicate turntable defined : " + newTurntable.PoolName);
                                }
                            }
                            break;

                        default:
                            if (!string.IsNullOrEmpty(turntableInfo.Strings[lineindex][0]))
                            {
                                Trace.TraceInformation("Invalid definition in file " + filePath + " at line " + lineindex + " : " +
                                    turntableInfo.Strings[lineindex][0].ToLower().Trim() + "\n");
                            }
                            lineindex++;
                            break;
                    }
                }
            }

            return (turntables);
        }
    }
}

