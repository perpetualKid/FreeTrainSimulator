using System;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TestActivityModel: ActivityModelCore
    {

        [MemoryPackIgnore]
        public string DefaultSort { get; init; }
        public string Folder {  get; init; }
        public string Route { get; init; }
        public string Activity { get; init; }
        public bool Tested { get; init; }
        public bool Passed { get; init; }
        public string Errors { get; init; }
        public string Load { get; init; }
        public string FPS { get; init; }

        [MemoryPackConstructor]
        public TestActivityModel()
        {
        }

        public TestActivityModel(ActivityModelCore activityModel): base(activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));

            RouteModelCore routeModel = activityModel.Parent;
            FolderModel folderModel = routeModel.Parent;
            DefaultSort = $"{folderModel.Name} | {routeModel.Name} | {activityModel.Name}";
            Folder = folderModel.Name;
            Route = routeModel.Name;
            Activity = activityModel.Name;
        }
    }
}
