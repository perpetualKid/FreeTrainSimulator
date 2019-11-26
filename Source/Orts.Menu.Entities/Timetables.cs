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

using Orts.Formats.OR.Files;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Orts.Menu.Entities
{
    public class TimetableInfo : ContentBase
    {
        public List<TimetableFile> TimeTables { get; private set; } = new List<TimetableFile>();
        public string Description { get; private set; }
        public string FileName { get; private set; }

        // items set for use as parameters, taken from main menu
        public int Day { get; set; }
        public int Season { get; set; }
        public int Weather { get; set; }
        public string WeatherFile { get; set; }

        // note : file is read preliminary only, extracting description and train information
        // all other information is read only when activity is started

        internal TimetableInfo(string filePath)
        {

            try
            {
                string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                if (extension.Contains("list"))
                {
                    TimetableGroupFile groupFile = new TimetableGroupFile(filePath);
                    TimeTables = groupFile.TimeTables;
                    FileName = filePath;
                    Description = groupFile.Description;
                }
                else
                {
                    TimetableFile timeTableFile = new TimetableFile(filePath);
                    TimeTables.Add(timeTableFile);
                    FileName = filePath;
                    Description = timeTableFile.Description;
                }
            }
            catch
            {
                Description = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
            }
        }
        internal static async Task<TimetableInfo> FromFileAsync(string fileName, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                return new TimetableInfo(fileName);
            }, token).ConfigureAwait(false);
        }

        public override string ToString()
        {
            return Description;
        }

        public static async Task<IEnumerable<TimetableInfo>> GetTimetableInfo(Folder folder, Route route, CancellationToken token)
        {
            string[] extensions = { "*.timetable_or", "*.timetable-or", "*.timetablelist_or", "*.timetablelist-or" };
            string orActivitiesDirectory = route.RouteFolder.OrActivitiesFolder;

            if (Directory.Exists(orActivitiesDirectory))
            {
                try
                {
                    var tasks = extensions.SelectMany(extension => (Directory.GetFiles(orActivitiesDirectory, extension))).Select(timeTableFile => FromFileAsync(timeTableFile, token));
                    return (await Task.WhenAll(tasks).ConfigureAwait(false)).Where(t => t != null);
                }
                catch (OperationCanceledException) { }
            }
            return new TimetableInfo[0];
        }
    }

    public class WeatherFileInfo
    {
        public FileInfo FileDetails;

        private WeatherFileInfo(string fileName)
        {
            FileDetails = new FileInfo(fileName);
        }

        internal static async Task<WeatherFileInfo> FromFileNameAsync(string fileName, CancellationToken token)
        {
            return await Task.Run(() => new WeatherFileInfo(fileName));
        }

        public override string ToString()
        {
            return (FileDetails.Name);
        }

        public string GetFullName()
        {
            return (FileDetails.FullName);
        }

        // get weatherfiles
        public static async Task<IEnumerable<WeatherFileInfo>> GetTimetableWeatherFiles(Folder folder, Route route, CancellationToken token)
        {
            string weatherDirectory = route.RouteFolder.WeatherFolder;

            if (Directory.Exists(weatherDirectory))
            {
                try
                {
                    var tasks = Directory.GetFiles(weatherDirectory, "*.weather-or").Select(weatherFile => FromFileNameAsync(weatherFile, token));
                    return (await Task.WhenAll(tasks).ConfigureAwait(false)).Where(w => w != null);
                }
                catch (OperationCanceledException) { }
            }
            return new WeatherFileInfo[0];
        }
    }
}

