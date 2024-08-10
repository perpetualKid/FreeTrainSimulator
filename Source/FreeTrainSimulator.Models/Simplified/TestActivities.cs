using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader;

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

        private TestActivity(ContentFolderModel folder, string routeName, Activity activity)
        {
            DefaultSort = $"{folder.Name}/{routeName}/{activity.Name}";
            Route = routeName;
            Activity = activity.Name;
            ActivityFilePath = activity.FilePath;
        }

        public static async Task<IEnumerable<TestActivity>> GetTestActivities(FrozenSet<ContentFolderModel> contentFolders, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(contentFolders);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<TestActivity> result = new List<TestActivity>();

                TransformManyBlock<ContentFolderModel, (ContentFolderModel, string)> inputBlock = new TransformManyBlock<ContentFolderModel, (ContentFolderModel, string)>
                    (contentFolder =>
                    {
                        string routesDirectory = contentFolder.MstsContentFolder().RoutesFolder;
                        if (Directory.Exists(routesDirectory))
                        {
                            return Directory.EnumerateDirectories(routesDirectory).Select(r => (contentFolder, r));
                        }
                        else
                            return Array.Empty<(ContentFolderModel, string)>();
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                TransformManyBlock<(ContentFolderModel, string), (ContentFolderModel, string, string)> routeBlock = new TransformManyBlock<(ContentFolderModel folder, string routeDirectory), (ContentFolderModel, string, string)>
                    (routeFile =>
                    {
                        FolderStructure.ContentFolder.RouteFolder route = FolderStructure.Route(routeFile.routeDirectory);
                        if (route.Valid)
                        {
                            string activitiesDirectory = route.ActivitiesFolder;
                            if (Directory.Exists(activitiesDirectory))
                            {
                                return Directory.EnumerateFiles(activitiesDirectory, "*.act").Select(a => (routeFile.folder, route.RouteName /*Should be replace with ContentRouteModel.Name since RouteName refers to the directory Name*/, a));
                            }
                        }
                        return Array.Empty<(ContentFolderModel, string, string)>();

                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                TransformBlock<(ContentFolderModel, string, string), TestActivity> activityBlock = new TransformBlock<(ContentFolderModel folder, string routeName, string activity), TestActivity>
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

                foreach (ContentFolderModel folder in contentFolders)
                    await inputBlock.SendAsync(folder).ConfigureAwait(false);

                inputBlock.Complete();
                await actionBlock.Completion.ConfigureAwait(false);
                return result;
            }
        }
    }
}
