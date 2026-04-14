using System;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Extended activity model used for automated testing of activities.
    /// Captures test results (pass/fail, errors, performance metrics) alongside the activity header data.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TestActivityModel: ActivityModelHeader
    {

        /// <summary>Default sort key composed of folder, route, and activity names.</summary>
        [MemoryPackIgnore]
        public string DefaultSort { get; init; }
        /// <summary>Name of the content folder containing this activity.</summary>
        public string Folder {  get; init; }
        /// <summary>Name of the route for this activity.</summary>
        public string Route { get; init; }
        /// <summary>Name of the activity being tested.</summary>
        public string Activity { get; init; }
        /// <summary>Indicates whether the activity has been tested.</summary>
        public bool Tested { get; init; }
        /// <summary>Indicates whether the activity test passed.</summary>
        public bool Passed { get; init; }
        /// <summary>Error messages captured during the test run.</summary>
        public string Errors { get; init; }
        /// <summary>Load time metric for the activity test.</summary>
        public string Load { get; init; }
        /// <summary>Frames-per-second metric captured during the test run.</summary>
        public string FPS { get; init; }

        [MemoryPackConstructor]
        public TestActivityModel()
        {
        }

        public TestActivityModel(ActivityModelHeader activityModel): base(activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));

            RouteModelHeader routeModel = activityModel.Parent;
            FolderModel folderModel = routeModel.Parent;
            DefaultSort = $"{folderModel.Name} | {routeModel.Name} | {activityModel.Name}";
            Folder = folderModel.Name;
            Route = routeModel.Name;
            Activity = activityModel.Name;
        }
    }
}
