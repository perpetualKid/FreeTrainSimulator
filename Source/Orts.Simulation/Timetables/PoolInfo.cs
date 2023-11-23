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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Orts.Formats.OR.Parsers;

namespace Orts.Simulation.Timetables
{
    /// <summary>
    /// Class to collect pool details
    /// </summary>
    public class PoolInfo
    {
        private const string significantFileName = "pool";

        //================================================================================================//
        /// <summary>
        /// Read pool files
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public static Dictionary<string, TimetablePool> ProcessPools(string fileName, CancellationToken cancellationToken)
        {
            Dictionary<string, TimetablePool> pools = new Dictionary<string, TimetablePool>();

            // get filenames to process
            Trace.Write("\n");
            foreach (string filePath in EnumeratePoolFiles(fileName, significantFileName))
            {
                // get contents as strings
                Trace.Write("Pool File : " + filePath + "\n");
                var poolInfo = new TimetableReader(filePath);

                // read lines from input until 'Name' definition is found
                int lineindex = 1;
                while (lineindex < poolInfo.Strings.Count && !cancellationToken.IsCancellationRequested)
                {
                    switch (poolInfo.Strings[lineindex][0].ToLower().Trim())
                    {
                        // skip comment
                        case "#comment" :
                            lineindex++;
                            break;
                         
                        // process name
                        // do not increase lineindex as that is done in called method
                        case "#name" :
                            TimetablePool newPool = new TimetablePool(poolInfo, ref lineindex, Simulator.Instance);
                            // store if valid pool
                            if (!string.IsNullOrEmpty(newPool.PoolName))
                            {
                                if (!pools.TryAdd(newPool.PoolName, newPool))
                                {
                                    Trace.TraceWarning("Duplicate pool defined : " + newPool.PoolName);
                                }
                            }
                            break;

                        default :
                            if (!string.IsNullOrEmpty(poolInfo.Strings[lineindex][0]))
                            {
                                Trace.TraceInformation("Invalid definition in file " + filePath + " at line " + lineindex + " : " +
                                    poolInfo.Strings[lineindex][0].ToLower().Trim() + "\n");
                            }
                            lineindex++;
                            break;
                    }
                }
            }

            return (pools);
        }

        //================================================================================================//
        /// <summary>
        /// Get filenames of pools to process
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private protected static IEnumerable<string> EnumeratePoolFiles(string filePath, string filenameIdentifier)
        {
            // check type of timetable file - list or single
            string fileDirectory = Path.GetDirectoryName(filePath);

            foreach (string poolFile in Directory.EnumerateFiles(fileDirectory, $"*.{filenameIdentifier}_or"))
            {
                yield return poolFile;
            }
            foreach (string poolFile in Directory.EnumerateFiles(fileDirectory, $"*.{filenameIdentifier}-or"))
            {
                yield return poolFile;
            }
        }

    }
}

