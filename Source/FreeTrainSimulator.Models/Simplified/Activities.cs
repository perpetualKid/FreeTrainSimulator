﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using FreeTrainSimulator.Common;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Simplified
{
    public class Activity : ContentBase
    {
        private static readonly DefaultExploreActivity DefaultExploreActivity = new DefaultExploreActivity();
        private static readonly ExploreThroughActivity ExploreThroughActivity = new ExploreThroughActivity();

        public string Name { get; private set; }
        public string ActivityID { get; private set; }
        public string Description { get; private set; }
        public string Briefing { get; private set; }
        public TimeSpan StartTime { get; protected set; } = new TimeSpan(10, 0, 0);
        public SeasonType Season { get; protected set; } = SeasonType.Summer;
        public WeatherType Weather { get; protected set; } = WeatherType.Clear;
        public Difficulty Difficulty { get; protected set; } = Difficulty.Easy;
        public TimeSpan Duration { get; protected set; } = new TimeSpan(1, 0, 0);
        public Consist Consist { get; protected set; } = Consist.Missing;
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
                Name = activityFile.Activity.Header.Name;
                if (activityFile.Activity.Header.Mode == ActivityMode.Introductory)
                    Name = "Introductory Train Ride";
                Description = activityFile.Activity.Header.Description;
                Briefing = activityFile.Activity.Header.Briefing;
                StartTime = activityFile.Activity.Header.StartTime;
                Season = activityFile.Activity.Header.Season;
                Weather = activityFile.Activity.Header.Weather;
                Difficulty = activityFile.Activity.Header.Difficulty;
                Duration = activityFile.Activity.Header.Duration;
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

        internal static Activity FromPath(string filePath, Folder folder, Orts.Formats.Msts.FolderStructure.ContentFolder.RouteFolder routeFolder)
        {
            Activity result;
            try
            {
                ActivityFile activityFile = new ActivityFile(filePath);
                ServiceFile srvFile = new ServiceFile(routeFolder.ServiceFile(activityFile.Activity.PlayerServices.Name));
                Consist consist = Consist.GetConsist(folder, srvFile.TrainConfig, false);
                Path path = new Path(routeFolder.PathFile(srvFile.PathId));
                if (!path.PlayerPath)
                {
                    return null;
                    // Not nice to throw an error now. Error was originally thrown by new Path(...);
                    throw new InvalidDataException("Not a player path");
                }
                //else if (!activityFile.Activity.Header.RouteID.Equals(routeFolder.RouteID, StringComparison.OrdinalIgnoreCase))
                //{
                //    //Activity and route have different RouteID.
                //    result = new Activity($"<{catalog.GetString("Not same route:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>", filePath, null, null, null);
                //}
                else
                    result = new Activity(string.Empty, filePath, activityFile, consist, path);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                result = new Activity($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>", filePath, null, null, null);
            }
            return result;
        }

        internal static Activity FromPathShallow(string filePath)
        {
            try
            {
                ActivityFile activityFile = new ActivityFile(filePath);

                return new Activity(string.Empty, filePath, activityFile, null, null);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return null;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public static async Task<IEnumerable<Activity>> GetActivities(Folder folder, Orts.Formats.Msts.FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(folder);
            ArgumentNullException.ThrowIfNull(routeFolder);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<Activity> result = new List<Activity>();
                string activitiesDirectory = routeFolder.ActivitiesFolder;
                result.Add(DefaultExploreActivity);
                result.Add(ExploreThroughActivity);

                if (Directory.Exists(activitiesDirectory))
                {
                    TransformBlock<string, Activity> inputBlock = new TransformBlock<string, Activity>
                        (activityFile =>
                        {
                            return FromPath(activityFile, folder, routeFolder);
                        },
                        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });


                    ActionBlock<Activity> actionBlock = new ActionBlock<Activity>
                        (async activity =>
                        {
                            if (activity == null)
                                return;
                            try
                            {
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(activity);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        });

                    inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                    foreach (string activityFile in Directory.EnumerateFiles(activitiesDirectory, "*.act"))
                        await inputBlock.SendAsync(activityFile).ConfigureAwait(false);

                    inputBlock.Complete();
                    await actionBlock.Completion.ConfigureAwait(false);
                }
                return result;
            }
        }
    }

    public abstract class ExploreActivity : Activity
    {
        internal ExploreActivity()
            : base(null, null, null, null, null)
        {
        }

        public void UpdateActivity(string startTime, SeasonType season, WeatherType weather, Consist consist, Path path)
        {
            if (!TimeSpan.TryParse(startTime, out TimeSpan result))
                result = new TimeSpan(12, 0, 0);
            StartTime = result;
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