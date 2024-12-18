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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Orts.Formats.Msts.Files;
using Orts.Formats.OpenRails.Parsers;

namespace Orts.ContentManager.Models
{
    public class Service
    {
        public string Name { get; }
        public string ID { get; }
        public DateTime StartTime { get; }
        public string Consist { get; }
        public bool Reversed { get; }
        public string Path { get; }

        public IEnumerable<StationStop> Stops { get; }

        public Service(ContentBase content)
        {
            Debug.Assert(content?.Type == ContentType.Service);
            if (System.IO.Path.GetExtension(content.PathName).Equals(".srv", StringComparison.OrdinalIgnoreCase))
            {
                ServiceFile file = new ServiceFile(content.PathName);
                Name = file.Name;
                Consist = file.TrainConfig;
                Path = file.PathId;

                Debug.Assert(content is ContentMSTSService);
                ContentMSTSService msts = content as ContentMSTSService;
                ActivityFile actFile = new ActivityFile(content.Parent.PathName);
                if (msts.IsPlayer)
                {
                    Formats.Msts.Models.PlayerTraffics activityTraffic = actFile.Activity.PlayerServices.PlayerTraffics;

                    ID = "0";
                    StartTime = MSTSTimeToDateTime(activityTraffic.Time);
                    Stops = from stop in activityTraffic
                            select new StationStop(stop.PlatformStartID, stop.DistanceDownPath, MSTSTimeToDateTime(stop.ArrivalTime), MSTSTimeToDateTime(stop.DepartTime));
                }
                else
                {
                    TrafficFile trfFile = new TrafficFile(msts.TrafficPathName);
                    Formats.Msts.Models.Services activityService = actFile.Activity.Traffic.Services[msts.TrafficIndex];
                    Formats.Msts.Models.ServiceTraffics trafficService = trfFile.TrafficDefinition.ServiceTraffics[msts.TrafficIndex];

                    ID = $"{activityService.UiD}";
                    StartTime = MSTSTimeToDateTime(activityService.Time);
                    Stops = trafficService.Zip(activityService, (tt, stop) => new StationStop(stop.PlatformStartID, stop.DistanceDownPath, MSTSTimeToDateTime(tt.ArrivalTime), MSTSTimeToDateTime(tt.DepartTime)));
                }
            }
            else if (System.IO.Path.GetExtension(content.PathName).Equals(".timetable_or", StringComparison.OrdinalIgnoreCase)
                || System.IO.Path.GetExtension(content.PathName).Equals(".timetable-or", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Make common timetable parser.
                TimetableReader file = new TimetableReader(content.PathName);
                Name = content.Name;

                int serviceColumn = -1;
                int consistRow = -1;
                int pathRow = -1;
                int startRow = -1;
                for (int row = 0; row < file.Strings.Count; row++)
                {
                    if (file.Strings[row][0] == "#consist" && consistRow == -1)
                    {
                        consistRow = row;
                    }
                    else if (file.Strings[row][0] == "#path" && pathRow == -1)
                    {
                        pathRow = row;
                    }
                    else if (file.Strings[row][0] == "#start" && startRow == -1)
                    {
                        startRow = row;
                    }
                }
                for (int column = 0; column < file.Strings[0].Length; column++)
                {
                    if (file.Strings[0][column] == content.Name && serviceColumn == -1)
                    {
                        serviceColumn = column;
                    }
                }
                ID = $"{serviceColumn}";
                Regex timeRE = new Regex(@"^(\d\d):(\d\d)(?:-(\d\d):(\d\d))?");
                Match startTimeMatch = timeRE.Match(file.Strings[startRow][serviceColumn]);
                if (startTimeMatch.Success)
                {
                    StartTime = new DateTime(2000, 1, 1, int.Parse(startTimeMatch.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(startTimeMatch.Groups[2].Value, CultureInfo.InvariantCulture), 0);
                }
                List<StationStop> stops = new List<StationStop>();
                for (int row = 0; row < file.Strings.Count; row++)
                {
                    if (row != startRow)
                    {
                        Match timeMatch = timeRE.Match(file.Strings[row][serviceColumn]);
                        if (timeMatch.Success)
                        {
                            DateTime arrivalTime = new DateTime(2000, 1, 1, int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture), 0);
                            DateTime departureTime = timeMatch.Groups[3].Success ? new DateTime(2000, 1, 1, int.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(timeMatch.Groups[4].Value, CultureInfo.InvariantCulture), 0) : arrivalTime;
                            // If the time is prior to this train's start time, assume it is rolling over in to "tomorrow".
                            if (arrivalTime < StartTime)
                            {
                                arrivalTime = arrivalTime.AddDays(1);
                                departureTime = departureTime.AddDays(1);
                            }
                            stops.Add(new StationStop(file.Strings[row][0].Replace(" $hold", "", StringComparison.OrdinalIgnoreCase).Replace(" $forcehold", "", StringComparison.OrdinalIgnoreCase), arrivalTime, departureTime));
                        }
                    }
                }
                Stops = stops.OrderBy(s => s.ArrivalTime);
                Consist = file.Strings[consistRow][serviceColumn].Replace(" $reverse", "", StringComparison.OrdinalIgnoreCase);
                Reversed = file.Strings[consistRow][serviceColumn].Contains(" $reverse", StringComparison.OrdinalIgnoreCase);
                Path = file.Strings[pathRow][serviceColumn];
            }
        }

        /// <summary>
        /// Convert <see cref="TrafficFile"/> arrival and departure times in to normalized times.
        /// </summary>
        private static DateTime MSTSTimeToDateTime(int mstsAITime)
        {
            return new DateTime(2000, 1, 1).AddSeconds(mstsAITime);
        }
    }

    public class StationStop
    {
        public string Station { get; }
        public int PlatformID { get; }
        public float Distance { get; }
        public DateTime ArrivalTime { get; }
        public DateTime DepartureTime { get; }

        internal StationStop(int platformID, float distance, DateTime arrivalTime, DateTime departureTime)
        {
            PlatformID = platformID;
            Distance = distance;
            ArrivalTime = arrivalTime;
            DepartureTime = departureTime;
        }

        internal StationStop(string station, DateTime arrivalTime, DateTime departureTime)
        {
            Station = station;
            ArrivalTime = arrivalTime;
            DepartureTime = departureTime;
        }
    }
}
