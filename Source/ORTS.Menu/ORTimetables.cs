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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GNU.Gettext;
using Orts.Formats.OR;

namespace ORTS.Menu
{
    public class TimetableInfo
    {
        public List<TimetableFileLite> ORTTList { get; private set; } = new List<TimetableFileLite>();
        public string Description { get; private set; }
        public string FileName { get; private set; }

        // items set for use as parameters, taken from main menu
        public int Day;
        public int Season;
        public int Weather;

        // note : file is read preliminary only, extracting description and train information
        // all other information is read only when activity is started

        GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");

        internal TimetableInfo(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    ORTTList.Add(new TimetableFileLite(filePath));
                    FileName = filePath;
                    Description = ORTTList[ORTTList.Count - 1].Description;
                }
                catch
                {
                    Description = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
            }
            else
            {
                Description = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
        }

        protected TimetableInfo(string filePath, string directory)
        {
            if (File.Exists(filePath))
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
                    Description = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
            }
            else
            {
                Description = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
        }

        public override string ToString()
        {
            return Description;
        }

        public static Task<List<TimetableInfo>> GetTimetableInfo(Folder folder, Route route, CancellationToken token)
        {
            TaskCompletionSource<List<TimetableInfo>> tcs = new TaskCompletionSource<List<TimetableInfo>>();
            string[] extensions = { "*.timetable_or", "*.timetable-or", "*.timetablelist_or", "*.timetablelist-or" };

            List<TimetableInfo> result = new List<TimetableInfo>();
            if (route != null)
            {
                string path = System.IO.Path.Combine(route.Path, "ACTIVITIES", "OPENRAILS");

                void AddFiles(string[] files)
                {
                    foreach (var timetableFile in files)
                    {
                        if (token.IsCancellationRequested)
                        {
                            tcs.SetCanceled();
                            return;
                        }
                        try
                        {
                            result.Add(new TimetableInfo(timetableFile));
                        }
                        catch { }
                    }
                }

                if (Directory.Exists(path))
                {
                    foreach (string extension in extensions)
                    {
                        if (token.IsCancellationRequested)
                        {
                            tcs.TrySetCanceled();
                            break;
                        }
                        AddFiles(Directory.GetFiles(path, extension));
                    }
                }
            }

            tcs.TrySetResult(result);
            return tcs.Task;
        }
    }
}

