using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".dispatchersettings")]
    public sealed partial record ProfileDispatcherSettingsModel : ProfileSettingsModelBase
    {
        public override ProfileModel Parent => base.Parent as ProfileModel;

        public EnumArray<(int X, int Y), WindowSetting> WindowSettings { get; set; } = new EnumArray<(int, int), WindowSetting>(new (int, int)[]
        {
            new (50, 50), // % of the windows Screen
            new (75, 75), // % of screen size 
        });

        public int WindowScreen { get; set; }

        public EnumArray<(int X, int Y), DispatcherWindowType> PopupLocations { get; } = new EnumArray<(int X, int Y), DispatcherWindowType>((DispatcherWindowType windowType) => windowType switch
        {
            DispatcherWindowType.SignalState => (100, 100),
            DispatcherWindowType.HelpWindow => (50, 50),
            DispatcherWindowType.SignalChange => (0, 0),
            DispatcherWindowType.SwitchChange => (0, 0),
            DispatcherWindowType.DebugScreen => (0, 0),
            DispatcherWindowType.Settings => (0, 100),
            DispatcherWindowType.TrainInfo => (75, 75),
            _ => throw new System.NotImplementedException(),
        });

        public EnumArray<bool, DispatcherWindowType> PopupStatus { get; } = new EnumArray<bool, DispatcherWindowType>((DispatcherWindowType windowType) => windowType switch
        {
            DispatcherWindowType.SignalState => true,
            DispatcherWindowType.HelpWindow => true,
            DispatcherWindowType.SignalChange => false,
            DispatcherWindowType.SwitchChange => false,
            DispatcherWindowType.DebugScreen => false,
            DispatcherWindowType.Settings => false,
            DispatcherWindowType.TrainInfo => false,
            _ => throw new System.NotImplementedException(),
        });

        public EnumArray<bool, MapContentType> ContentTypeVisibility { get; } = new EnumArray<bool, MapContentType>((MapContentType contentType) => contentType switch
        {
            MapContentType.Tracks => true,
            MapContentType.EndNodes => true,
            MapContentType.JunctionNodes => true,
            MapContentType.LevelCrossings => true,
            MapContentType.Crossovers => true,
            MapContentType.Roads => false,
            MapContentType.RoadEndNodes => false,
            MapContentType.RoadCrossings => false,
            MapContentType.CarSpawners => false,
            MapContentType.Sidings => true,
            MapContentType.SidingNames => true,
            MapContentType.Platforms => true,
            MapContentType.PlatformNames => true,
            MapContentType.StationNames => true,
            MapContentType.SpeedPosts => true,
            MapContentType.MilePosts => true,
            MapContentType.Signals => true,
            MapContentType.OtherSignals => false,
            MapContentType.Hazards => false,
            MapContentType.Pickups => false,
            MapContentType.SoundRegions => false,
            MapContentType.Grid => false,
            MapContentType.Paths => false,
            MapContentType.TrainNames => true,
            MapContentType.Empty => false,
            _ => throw new System.NotImplementedException(),
        });

    }
}
