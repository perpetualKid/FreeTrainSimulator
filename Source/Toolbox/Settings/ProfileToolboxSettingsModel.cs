using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox.PopupWindows;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".toolboxsettings")]
    public sealed partial record ProfileToolboxSettingsModel : ProfileSettingsModelBase
    {
        public EnumArray<Point, WindowSetting> WindowSettings { get; set; } = new EnumArray<Point, WindowSetting>(new Point[]
        {
            new Point(50, 50), // % of the windows Screen
            new Point(75, 75), // % of screen size 
        });
        public int WindowScreen { get; set; }
        public PointD ContentPosition { get; set; }
        public double ContentScale { get; set; }
        public EnumArray<string, ColorSetting> ColorSettings { get; private set; } = new EnumArray<string, ColorSetting>((ColorSetting colorSetting) => colorSetting switch
        {
            ColorSetting.Background => nameof(Color.DarkGray),
            ColorSetting.RailTrack => nameof(Color.Blue),
            ColorSetting.RailTrackEnd => nameof(Color.BlueViolet),
            ColorSetting.RailTrackJunction => nameof(Color.DarkMagenta),
            ColorSetting.RailTrackCrossing => nameof(Color.Firebrick),
            ColorSetting.RailLevelCrossing => nameof(Color.Crimson),
            ColorSetting.RoadTrack => nameof(Color.Olive),
            ColorSetting.RoadTrackEnd => nameof(Color.ForestGreen),
            ColorSetting.RoadLevelCrossing => nameof(Color.DeepPink),
            ColorSetting.PathTrack => nameof(Color.Gold),
            ColorSetting.RoadCarSpawner => nameof(Color.White),
            ColorSetting.SignalItem => nameof(Color.White),
            ColorSetting.StationItem => nameof(Color.Firebrick),
            ColorSetting.PlatformItem => nameof(Color.Navy),
            ColorSetting.SidingItem => nameof(Color.ForestGreen),
            ColorSetting.SpeedPostItem => nameof(Color.RoyalBlue),
            ColorSetting.HazardItem => nameof(Color.White),
            ColorSetting.PickupItem => nameof(Color.White),
            ColorSetting.SoundRegionItem => nameof(Color.White),
            ColorSetting.LevelCrossingItem => nameof(Color.White),
            _ => throw new System.InvalidCastException(),
        });
        public EnumArray<bool, MapContentType> ViewSettings { get; private set; } = new EnumArray<bool, MapContentType>(true);
        public EnumArray<Point, ToolboxWindowType> PopupLocations { get; private set; } = new EnumArray<Point, ToolboxWindowType>((ToolboxWindowType toolboxWindowType) => toolboxWindowType switch
        {
            ToolboxWindowType.QuitWindow => new Point(50, 50),
            ToolboxWindowType.AboutWindow => new Point(50, 50),
            ToolboxWindowType.StatusWindow => new Point(50, 50),
            ToolboxWindowType.DebugScreen => new Point(0, 0),
            ToolboxWindowType.LocationWindow => new Point(100, 100),
            ToolboxWindowType.HelpWindow => new Point(10, 90),
            ToolboxWindowType.TrackNodeInfoWindow => new Point(10, 70),
            ToolboxWindowType.TrackItemInfoWindow => new Point(30, 70),
            ToolboxWindowType.SettingsWindow => new Point(70, 70),
            ToolboxWindowType.LogWindow => new Point(30, 70),
            ToolboxWindowType.TrainPathWindow => new Point(10, 40),
            _ => throw new System.NotImplementedException(),
        });
        public EnumArray<bool, ToolboxWindowType> PopupStatus { get; private set; } = new EnumArray<bool, ToolboxWindowType>((ToolboxWindowType toolboxWindowType) => toolboxWindowType switch
        {
            ToolboxWindowType.QuitWindow => false,
            ToolboxWindowType.AboutWindow => false,
            ToolboxWindowType.StatusWindow => false,
            ToolboxWindowType.DebugScreen => false,
            ToolboxWindowType.LocationWindow => true,
            ToolboxWindowType.HelpWindow => true,
            ToolboxWindowType.TrackNodeInfoWindow => false,
            ToolboxWindowType.TrackItemInfoWindow => false,
            ToolboxWindowType.SettingsWindow => true,
            ToolboxWindowType.LogWindow => false,
            ToolboxWindowType.TrainPathWindow => false,
            _ => throw new System.NotImplementedException(),
        });
        public EnumArray<string, ToolboxWindowType> PopupSettings { get; private set; } = new EnumArray<string, ToolboxWindowType>();
        public bool RestoreLastView { get; set; } = true;
        public bool FontOutline { get; set; } = true;
        // Route selections
        public string Folder { get; set; }
        public string RouteId { get; set; }
        public string PathId { get; set; }

    }
}