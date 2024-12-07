using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ProfileUserSettingsMdel : ModelBase<ProfileUserSettingsMdel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".usersettings";
        }

        public override ProfileModel Parent => (this as IFileResolve).Container as ProfileModel;

        public TraceSettings LogLevel { get; set; } = TraceSettings.Errors;
        public string LogFileName { get; set; } = "{Product} {Application} Log.txt";
        public string LogFilePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        public string Language { get; set; }

        public int MultiSamplingCount { get; set; } = 4;
    }
}
