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

        public override string ToString()
        {
            return Description;
        }

        public static Task<List<TimetableInfo>> GetTimetableInfo(Folder folder, Route route, CancellationToken token)
        {
            string[] extensions = { "*.timetable_or", "*.timetable-or", "*.timetablelist_or", "*.timetablelist-or" };
            SemaphoreSlim addItem = new SemaphoreSlim(1);

            List<TimetableInfo> result = new List<TimetableInfo>();
            if (route != null)
            {
                string orActivitiesDirectory = route.RouteFolder.OrActivitiesFolder;

                if (Directory.Exists(orActivitiesDirectory))
                {
                    try
                    {
                        Parallel.ForEach(extensions,
                            new ParallelOptions() { CancellationToken = token },
                            (extension, state) =>
                        {
                            Parallel.ForEach(Directory.GetFiles(orActivitiesDirectory, extension),
                                new ParallelOptions() { CancellationToken = token },
                                (timetableFile, innerState) =>
                            {
                                try
                                {
                                    TimetableInfo timetableInfo = new TimetableInfo(timetableFile);
                                    addItem.Wait(token);
                                    result.Add(timetableInfo);
                                }
                                catch { }
                                finally { addItem.Release(); }
                            });
                        });
                    }
                    catch (OperationCanceledException) { }
                    if (token.IsCancellationRequested)
                        return Task.FromCanceled<List<TimetableInfo>>(token);
                }
            }
            return Task.FromResult(result);
        }
    }

    public class WeatherFileInfo
    {
        public FileInfo FileDetails;

        public WeatherFileInfo(string filename)
        {
            FileDetails = new FileInfo(filename);
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
        public static Task<List<WeatherFileInfo>> GetTimetableWeatherFiles(Folder folder, Route route, CancellationToken token)
        {
            SemaphoreSlim addItem = new SemaphoreSlim(1);
            List<WeatherFileInfo> result = new List<WeatherFileInfo>();

            if (route != null)
            {
                string weatherDirectory = route.RouteFolder.WeatherFolder;

                if (Directory.Exists(weatherDirectory))
                {
                    try
                    {
                        Parallel.ForEach(Directory.GetFiles(weatherDirectory, "*.weather-or"),
                            new ParallelOptions() { CancellationToken = token },
                            (weatherFile, state) =>
                            {
                                try
                                {
                                    WeatherFileInfo weatherFileInfo = new WeatherFileInfo(weatherFile);
                                    addItem.Wait(token);
                                    result.Add(weatherFileInfo);
                                }
                                catch { }
                                finally { addItem.Release(); }
                            });
                    }
                    catch (OperationCanceledException) { }
                    if (token.IsCancellationRequested)
                        return Task.FromCanceled<List<WeatherFileInfo>>(token);
                }
            }
            return Task.FromResult(result);
        }
    }
}

