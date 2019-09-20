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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Menu.Entities
{
    public class Activity: ContentBase
    {
        public string Name { get; private set; }
        public string ActivityID { get; private set; }
        public string Description { get; private set; }
        public string Briefing { get; private set; }
        public StartTime StartTime { get; protected set; } = new StartTime(10, 0, 0);
        public SeasonType Season { get; protected set; } = SeasonType.Summer;
        public WeatherType Weather { get; protected set; } = WeatherType.Clear;
        public Difficulty Difficulty { get; protected set; } = Difficulty.Easy;
        public Duration Duration { get; protected set; } = new Duration(1, 0);
        public Consist Consist { get; protected set; } = Consist.GetConsist("unknown", null);
        public Path Path { get; protected set; } = new Path("unknown");
        public string FilePath { get; private set; }

        protected Activity(string name, string filePath, ActivityFile activityFile, Consist consist, Path path)
        {
            if (filePath == null && this is DefaultExploreActivity)
            {
                Name = catalog.GetString("- Explore Route -");
            }
            else if (filePath == null && this is ExploreThroughActivity)
            {
                Name = catalog.GetString("+ Explore in Activity Mode +");
            }
            else if (null != activityFile)
            {
                // ITR activities are excluded.
                Name = activityFile.Tr_Activity.Tr_Activity_Header.Name.Trim();
                if (activityFile.Tr_Activity.Tr_Activity_Header.Mode == ActivityMode.IntroductoryTrainRide)
                    Name = "Introductory Train Ride";
                Description = activityFile.Tr_Activity.Tr_Activity_Header.Description;
                Briefing = activityFile.Tr_Activity.Tr_Activity_Header.Briefing;
                StartTime = activityFile.Tr_Activity.Tr_Activity_Header.StartTime;
                Season = activityFile.Tr_Activity.Tr_Activity_Header.Season;
                Weather = activityFile.Tr_Activity.Tr_Activity_Header.Weather;
                Difficulty = activityFile.Tr_Activity.Tr_Activity_Header.Difficulty;
                Duration = activityFile.Tr_Activity.Tr_Activity_Header.Duration;
                Consist = consist;
                Path = path;
            }
            else
            {
                Name = name;
            }
            if (string.IsNullOrEmpty(Name))
                Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
            if (string.IsNullOrEmpty(Description))
                Description = null;
            if (string.IsNullOrEmpty(Briefing))
                Briefing = null;
            FilePath = filePath;
        }

        internal static Activity GetActivity(string filePath, Folder folder, Route route)
        {
            Activity result;

            if (File.Exists(filePath))
            {
                try
                {
                    ActivityFile activityFile = new ActivityFile(filePath);
                    ServiceFile srvFile = new ServiceFile(System.IO.Path.Combine(route.Path, "SERVICES", activityFile.Tr_Activity.Tr_Activity_File.Player_Service_Definition.Name + ".srv"));
                    Consist consist = Consist.GetConsist(folder, srvFile.TrainConfig);
                    Path path = new Path(System.IO.Path.Combine(route.Path, "PATHS", srvFile.PathId + ".pat"));
                    if (!path.IsPlayerPath)
                    {
                        return null;
                        // Not nice to throw an error now. Error was originally thrown by new Path(...);
                        throw new InvalidDataException("Not a player path");
                    }
                    else if (activityFile.Tr_Activity.Tr_Activity_Header.RouteID.ToUpper() != route.RouteID.ToUpper())
                    {
                        //Activity and route have different RouteID.
                        result = new Activity($"<{catalog.GetString("Not same route:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>", filePath, null, null, null);
                    }
                    else
                    result = new Activity(string.Empty, filePath, activityFile, consist, path);
                }
                catch
                {
                    result = new Activity($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>", filePath, null, null, null);
                }
            }
            else
            {
                result = new Activity($"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>", filePath, null, null, null);
            }

            return result;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Task<List<Activity>> GetActivities(Folder folder, Route route, CancellationToken token)
        {
            SemaphoreSlim addItem = new SemaphoreSlim(1);
            var activities = new List<Activity>();
            if (route != null)
            {
                activities.Add(new DefaultExploreActivity());
                activities.Add(new ExploreThroughActivity());
                var directory = System.IO.Path.Combine(route.Path, "ACTIVITIES");
                if (Directory.Exists(directory))
                {
                    try
                    {
                        Parallel.ForEach(Directory.GetFiles(directory, "*.act"),
                            new ParallelOptions() { CancellationToken = token },
                            (activityFile, state) =>
                            {
                                try
                                {
                                    Activity activity = GetActivity(activityFile, folder, route);
                                    if (null != activityFile)
                                    {
                                        addItem.Wait(token);
                                        activities.Add(activity);
                                    }
                                }
                                catch { }
                                finally { addItem.Release(); }
                            });
                    }
                    catch (OperationCanceledException) { }
                    if (token.IsCancellationRequested)
                        return Task.FromCanceled<List<Activity>>(token);
                }
            }
            return Task.FromResult(activities);
        }
    }

    public class ExploreActivity : Activity
    {
        internal ExploreActivity()
            : base(null, null, null, null, null)
        {
        }

        public void UpdateActivity(string startTime, SeasonType season, WeatherType weather, Consist consist, Path path)
        {
            var time = startTime.Split(':');
            if (!int.TryParse(time[0], out int hour))
                hour = 12;
            if (time.Length < 2 || !int.TryParse(time[1], out int minute))
                minute = 0;
            if (time.Length < 3 || !int.TryParse(time[2], out int second))
                second = 0;
            StartTime = new StartTime(hour, minute, second);
            Season = season;
            Weather = weather;
            Consist = consist;
            Path = path;
        }
    }

    public class DefaultExploreActivity : ExploreActivity
    { }

    public class ExploreThroughActivity : ExploreActivity
    { }
}
