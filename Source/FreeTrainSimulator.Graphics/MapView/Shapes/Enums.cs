namespace FreeTrainSimulator.Graphics.MapView.Shapes
{
    public enum BasicTextureType
    {
        BlankPixel,
        // next ones are used to map Resources, hence names should match exactly (case insensitive) the resource file name
        Circle,
        Disc,
        Ring,
        RingCrossed,
        ActiveBrokenNode,
        ActiveNode,
        ActiveNormalNode,
        ActiveTrackNode,
        CarSpawner,
        Hazard,
        PathEnd,
        PathNormal,
        PathReverse,
        PathStart,
        PathWait,
        Pickup,
        Platform,
        Signal,
        SignalGreen,
        SignalRed,
        SignalYellow,
        SignalSmall,
        SignalSmallGreen,
        SignalSmallRed,
        SignalSmallYellow,
        SignalDotGreen,
        SignalDotRed,
        SignalDotYellow,
        Sound,
        PlayerTrain,
        LevelCrossing,
        // Extended
        PauseTexture,
    }
}
