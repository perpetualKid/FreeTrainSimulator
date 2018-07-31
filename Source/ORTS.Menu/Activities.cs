// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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
using Orts.Formats.Msts;

namespace ORTS.Menu
{
    public class Activity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Briefing { get; private set; }
        public StartTime StartTime { get; private set; } = new StartTime(10, 0, 0);
        public SeasonType Season { get; private set; } = SeasonType.Summer;
        public WeatherType Weather { get; private set; } = WeatherType.Clear;
        public Difficulty Difficulty { get; private set; } = Difficulty.Easy;
        public Duration Duration { get; private set; } = new Duration(1, 0);
        public Consist Consist { get; private set; } = new Consist("unknown", null);
        public Path Path { get; private set; } = new Path("unknown");
        public string FilePath { get; private set; }

        private GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");

        internal Activity(string filePath, Folder folder, Route route)
        {
            if (filePath == null && this is DefaultExploreActivity)
            {
                Name = catalog.GetString("- Explore Route -");
            }
            else if (filePath == null && this is ExploreThroughActivity)
            {
                Name = catalog.GetString("+ Explore in Activity Mode +");
            }
            else if (File.Exists(filePath))
            {
                bool showInList = true;
                try
                {
                    ActivityFile actFile = new ActivityFile(filePath);
                    ServiceFile srvFile = new ServiceFile(System.IO.Path.Combine(route.Path, "SERVICES", actFile.Tr_Activity.Tr_Activity_File.Player_Service_Definition.Name + ".srv"));
                    // ITR activities are excluded.
                    Name = actFile.Tr_Activity.Tr_Activity_Header.Name.Trim();
                    if (actFile.Tr_Activity.Tr_Activity_Header.Mode == ActivityMode.IntroductoryTrainRide)
                        Name = "Introductory Train Ride";
                    Description = actFile.Tr_Activity.Tr_Activity_Header.Description;
                    Briefing = actFile.Tr_Activity.Tr_Activity_Header.Briefing;
                    StartTime = actFile.Tr_Activity.Tr_Activity_Header.StartTime;
                    Season = actFile.Tr_Activity.Tr_Activity_Header.Season;
                    Weather = actFile.Tr_Activity.Tr_Activity_Header.Weather;
                    Difficulty = actFile.Tr_Activity.Tr_Activity_Header.Difficulty;
                    Duration = actFile.Tr_Activity.Tr_Activity_Header.Duration;
                    Consist = new Consist(System.IO.Path.Combine(folder.Path, "TRAINS", "CONSISTS", srvFile.Train_Config + ".con"), folder);
                    Path = new Path(System.IO.Path.Combine(route.Path, "PATHS", srvFile.PathID + ".pat"));
                    if (!Path.IsPlayerPath)
                    {
                        // Not nice to throw an error now. Error was originally thrown by new Path(...);
                        throw new InvalidDataException("Not a player path");
                    }
                }
                catch
                {
                    Name = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
                if (!showInList)
                    throw new InvalidDataException(catalog.GetStringFmt("Activity '{0}' is excluded.", filePath));
                if (string.IsNullOrEmpty(Name))
                    Name = "<" + catalog.GetString("unnamed:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                if (string.IsNullOrEmpty(Description))
                    Description = null;
                if (string.IsNullOrEmpty(Briefing))
                    Briefing = null;
            }
            else
            {
                Name = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
            FilePath = filePath;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Task<List<Activity>> GetActivities(Folder folder, Route route, CancellationToken token)
        {
            TaskCompletionSource<List<Activity>> tcs = new TaskCompletionSource<List<Activity>>();
            var activities = new List<Activity>();
            if (route != null)
            {
                activities.Add(new DefaultExploreActivity());
                activities.Add(new ExploreThroughActivity());
                var directory = System.IO.Path.Combine(route.Path, "ACTIVITIES");
                if (Directory.Exists(directory))
                {
                    foreach (var activityFile in Directory.GetFiles(directory, "*.act"))
                    {
                        if (token.IsCancellationRequested)
                        {
                            tcs.SetCanceled();
                            break;
                        }
                        try
                        {
                            activities.Add(new Activity(activityFile, folder, route));
                        }
                        catch { }
                    }
                }
            }
            tcs.TrySetResult(activities);
            return tcs.Task;
        }
    }

    public class ExploreActivity : Activity
    {
        public new string StartTime;
        public new SeasonType Season = SeasonType.Summer;
        public new WeatherType Weather = WeatherType.Clear;
        public new Consist Consist = new Consist("unknown", null);
        public new Path Path = new Path("unknown");

        internal ExploreActivity()
            : base(null, null, null)
        {
        }
    }

    public class DefaultExploreActivity : ExploreActivity
    { }

    public class ExploreThroughActivity : ExploreActivity
    { }
}
