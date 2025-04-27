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
        /// <summary>
        /// Location and Size of the window in screen %
        /// </summary>
        public EnumArray<(int X, int Y), WindowSetting> WindowSettings { get; set; } = new EnumArray<(int X, int Y), WindowSetting>(new (int X, int Y)[]
        {
            (50, 50), // % of the windows Screen
            (75, 75), // % of screen size 
        });

        /// <summary>
        /// Window Screen to be used
        /// </summary>
        public int WindowScreen { get; set; }
        
        /// <summary>
        /// Last used center point of most recent route
        /// </summary>
        public PointD ContentPosition { get; set; }
        
        /// <summary>
        /// Most recent scale factor
        /// </summary>
        public double ContentScale { get; set; }
        
        /// <summary>
        /// Track Item Color settings
        /// </summary>
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

        /// <summary>
        /// Content item visibility settings
        /// </summary>
        public EnumArray<bool, MapContentType> ViewSettings { get; private set; } = new EnumArray<bool, MapContentType>(true);

        /// <summary>
        /// Locations (in Window %) of popup windows
        /// </summary>
        public EnumArray<(int X, int Y), ToolboxWindowType> PopupLocations { get; private set; } = new EnumArray<(int X, int Y), ToolboxWindowType>((ToolboxWindowType toolboxWindowType) => toolboxWindowType switch
        {
            ToolboxWindowType.QuitWindow => (50, 50),
            ToolboxWindowType.AboutWindow => (50, 50),
            ToolboxWindowType.StatusWindow => (50, 50),
            ToolboxWindowType.DebugScreen => (0, 0),
            ToolboxWindowType.LocationWindow => (100, 100),
            ToolboxWindowType.HelpWindow => (10, 10),
            ToolboxWindowType.TrackNodeInfoWindow => (10, 10),
            ToolboxWindowType.TrackItemInfoWindow => (30, 30),
            ToolboxWindowType.SettingsWindow => (70, 70),
            ToolboxWindowType.LogWindow => (30, 30),
            ToolboxWindowType.TrainPathWindow => (10, 10),
            ToolboxWindowType.TrainPathSaveWindow => (50, 50),
            _ => throw new System.NotImplementedException(),
        });

        /// <summary>
        /// Status (Visibility) of popup windows
        /// </summary>
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
            ToolboxWindowType.TrainPathSaveWindow => false,
            _ => throw new System.NotImplementedException(),
        });

        /// <summary>
        /// Arbitrary settings for each popup window, managed by the popup window, stored/serialized as string
        /// </summary>
        public EnumArray<string, ToolboxWindowType> PopupSettings { get; private set; } = new EnumArray<string, ToolboxWindowType>();

        /// <summary>
        /// Re-open to last view
        /// </summary>
        public bool RestoreLastView { get; set; } = true;

        /// <summary>
        /// Use Outline fone
        /// </summary>
        public bool FontOutline { get; set; } = true;

        // Route selections
        
        public string Folder { get; set; }

        public string RouteId { get; set; }

        public string PathId { get; set; }

        /// <summary>
        /// Factor to narrow tracks (and roads), 1 for original width
        /// </summary>
        public int TrackWidthRatio { get; set; } = 8;
    }
}