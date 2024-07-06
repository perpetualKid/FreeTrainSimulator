namespace Orts.Graphics
{
    public enum HorizontalAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public enum VerticalAlignment
    {
        Top = 0,
        Center = 1,
        Bottom = 2,
    }

    public enum ScreenMode
    {
        Windowed,
        WindowedFullscreen,
        BorderlessFullscreen,
    }

    public enum ColorSetting
    {
        Background,
        RailTrack,
        RailTrackEnd,
        RailTrackJunction,
        RailTrackCrossing,
        RailLevelCrossing,
        RoadTrack,
        RoadTrackEnd,
        RoadLevelCrossing,
        PathTrack,
        RoadCarSpawner,
        SignalItem,
        StationItem,
        PlatformItem,
        SidingItem,
        SpeedPostItem,
        HazardItem,
        PickupItem,
        SoundRegionItem,
        LevelCrossingItem,
    }

    public enum ColorVariation
    { 
        None, 
        Highlight,
        Complement, 
        ComplementHighlight,
    }

    public enum ShaderEffect
    {
        PopupWindow,
        Diagram,
    }
}
