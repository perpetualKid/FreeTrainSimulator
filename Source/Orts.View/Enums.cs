namespace Orts.View
{
    public enum TextHorizontalAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public enum TextVerticalAlignment
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
        RoadLevelCrossing,
        RoadCarSpawner,
        SignalItem,
        PlatformItem,
        SidingItem,
        SpeedPostItem,
        HazardItem,
        PickupItem,
        SoundRegionItem,
        LevelCrossing,
    }

    public enum ColorVariation
    { 
        None, 
        Highlight,
        Complement, 
        ComplementHighlight,
    }
}
