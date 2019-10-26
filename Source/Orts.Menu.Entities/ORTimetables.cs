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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Orts.Common.IO;
using Orts.Formats.OR;

namespace Orts.Menu.Entities
{
    public class TimetableInfo : ContentBase
    {
        public List<TimetableFileLite> ORTTList { get; private set; } = new List<TimetableFileLite>();
        public string Description { get; private set; }
        public string FileName { get; private set; }

        // items set for use as parameters, taken from main menu
        public int Day;
        public int Season;
        public int Weather;
        public string WeatherFile;

        // note : file is read preliminary only, extracting description and train information
        // all other information is read only when activity is started

        internal TimetableInfo(string filePath)
        {
            if (Common.IO.FileSystemCache.FileExists(filePath))
            {
                try
                {
                    TimetableFileLite timeTableLite = new TimetableFileLite(filePath);
                    ORTTList.Add(timeTableLite);
                    FileName = filePath;
                    Description = timeTableLite.Description;
                }
                catch
                {
                    Description = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
                }
            }
            else
            {
                Description = $"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
            }
        }

        protected TimetableInfo(string filePath, string directory)
        {
            if (FileSystemCache.FileExists(filePath))
            {
                try
                {
                    TimetableGroupFileLite multiInfo = new TimetableGroupFileLite(filePath, directory);
                    ORTTList = multiInfo.ORTTInfo;
                    FileName = filePath;
                    Description = multiInfo.Description;
                }
                catch
                {
                    Description = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
                }
            }
            else
            {
                Description = $"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
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
                string path = System.IO.Path.Combine(route.Path, "ACTIVITIES", "OPENRAILS");

                if (Directory.Exists(path))
                {
                    try
                    {
                        Parallel.ForEach(extensions,
                            new ParallelOptions() { CancellationToken = token },
                            (extension, state) =>
                        {
                            Parallel.ForEach(Directory.GetFiles(path, extension),
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
                var directory = System.IO.Path.Combine(route.Path, "WeatherFiles");

                if (Directory.Exists(directory))
                {
                    try
                    {
                        Parallel.ForEach(Directory.GetFiles(directory, "*.weather-or"),
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

