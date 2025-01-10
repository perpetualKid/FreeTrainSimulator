using System;
using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".usersettings")]
    public sealed partial record ProfileUserSettingsModel : ProfileSettingsModelBase
    {
        public override ProfileModel Parent => base.Parent as ProfileModel;

        public TraceEventType TraceType { get; set; } = TraceEventType.Error;
        public string LogFileName { get; set; } = "{Product} {Application} Log.txt";
        public string LogFilePath { get; set; } = RuntimeInfo.LogFilesFolder;

        public string Language { get; set; }

        public MeasurementUnit MeasurementUnit { get; set; } = MeasurementUnit.Route;

        public int MultiSamplingCount { get; set; } = 4;
    }
}
