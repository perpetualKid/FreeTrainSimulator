using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TestActivityModel: ActivityModelCore
    {

        [MemoryPackIgnore]
        public string DefaultSort { get; init; }
        public string Route { get; init; }
        public string Activity { get; init; }
        public string ActivityFilePath { get; init; }
        public bool ToTest { get; init; }
        public bool Tested { get; init; }
        public bool Passed { get; init; }
        public string Errors { get; init; }
        public string Load { get; init; }
        public string FPS { get; init; }

        [MemoryPackConstructor]
        public TestActivityModel()
        {
        }

        public TestActivityModel(ActivityModelCore activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));

            RouteModelCore routeModel = (activityModel as IFileResolve).Container as RouteModelCore;
            FolderModel folderModel = (routeModel as IFileResolve).Container as FolderModel;
            DefaultSort = $"{folderModel.Name}/{routeModel.Name}/{activityModel.Name}";
        }
    }
}
