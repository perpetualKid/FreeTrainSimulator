using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using FreeTrainSimulator.Models.Independent.Environment;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Simplified
{
    public class TestActivity
    {
        public string DefaultSort { get; set; }
        public string Route { get; set; }
        public string Activity { get; set; }
        public string ActivityFilePath { get; set; }
        public bool ToTest { get; set; }
        public bool Tested { get; set; }
        public bool Passed { get; set; }
        public string Errors { get; set; }
        public string Load { get; set; }
        public string FPS { get; set; }

        private TestActivity(Folder folder, string routeName, Activity activity)
        {
            DefaultSort = $"{folder.Name}/{routeName}/{activity.Name}";
            Route = routeName;
            Activity = activity.Name;
            ActivityFilePath = activity.FilePath;
        }

        public static async Task<IEnumerable<TestActivity>> GetTestActivities(Dictionary<string, string> folders, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(folders);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<TestActivity> result = new List<TestActivity>();

                TransformManyBlock<KeyValuePair<string, string>, (Folder, string)> inputBlock = new TransformManyBlock<KeyValuePair<string, string>, (Folder, string)>
                    (folderName =>
                    {
                        Folder folder = new Folder(folderName.Key, folderName.Value);
                        string routesDirectory = folder.ContentFolder.RoutesFolder;
                        if (Directory.Exists(routesDirectory))
                        {
                            return Directory.EnumerateDirectories(routesDirectory).Select(r => (folder, r));
                        }
                        else
                            return Array.Empty<(Folder, string)>();
                    },
                        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                TransformManyBlock<(Folder, string), (Folder, string, string)> routeBlock = new TransformManyBlock<(Folder folder, string routeDirectory), (Folder, string, string)>
                    (routeFile =>
                    {
                        if (FolderStructure.Route(routeFile.routeDirectory).Valid)
                        {
                            RouteModel routeModel = RouteLoader.LoadRoute(routeFile.routeDirectory, CancellationToken.None).Result;
                            string activitiesDirectory = FolderStructure.Route(routeModel.Path).ActivitiesFolder;
                            if (Directory.Exists(activitiesDirectory))
                            {
                                return Directory.EnumerateFiles(activitiesDirectory, "*.act").Select(a => (routeFile.folder, routeModel.Name, a));
                            }
                        }
                        return Array.Empty<(Folder, string, string)>();

                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                TransformBlock<(Folder, string, string), TestActivity> activityBlock = new TransformBlock<(Folder folder, string routeName, string activity), TestActivity>
                    (activityInput =>
                    {
                        Activity activity = Simplified.Activity.FromPathShallow(activityInput.activity);
                        return activity != null ? new TestActivity(activityInput.folder, activityInput.routeName, activity) : null;
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                ActionBlock<TestActivity> actionBlock = new ActionBlock<TestActivity>
                        (async activity =>
                        {
                            if (null == activity)
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

                inputBlock.LinkTo(routeBlock, new DataflowLinkOptions { PropagateCompletion = true });
                routeBlock.LinkTo(activityBlock, new DataflowLinkOptions { PropagateCompletion = true });
                activityBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                foreach (KeyValuePair<string, string> folder in folders)
                    await inputBlock.SendAsync(folder).ConfigureAwait(false);

                inputBlock.Complete();
                await actionBlock.Completion.ConfigureAwait(false);
                return result;
            }
        }
    }
}
