using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Settings;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox.Settings
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".toolboxsettings")]
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
            ColorSetting.SpeedPostItem => nameof(Color.Gold),
            ColorSetting.MilePostItem => nameof(Color.Black),
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
        /// Persisted settings for the individual toolbox tool windows.
        /// </summary>
        public ToolWindowSettingsStore ToolWindowSettings { get; set; } = new ToolWindowSettingsStore();

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
        /// Serialized AvalonDock layout JSON for the WPF toolbox shell.
        /// </summary>
        public string DockLayoutJson { get; set; }

        /// <summary>
        /// Downscale Track width for easier overview of track schema
        /// </summary>
        public bool LimitTrackWidth { get; set; } = true;

        /// <summary>
        /// Durable placement (restored bounds and maximized state) of the WPF toolbox shell window. Null when no
        /// placement has been saved yet, in which case the shell uses its default size and location.
        /// </summary>
        public WindowPlacementSettings WindowPlacements { get; set; }
    }
}