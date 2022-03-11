using System.ComponentModel;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    public enum UserCommand
    {
        [Description("Change Screen Mode")] ChangeScreenMode,
        [Description("Move Left (East)")] MoveLeft,
        [Description("Move Right (West)")] MoveRight,
        [Description("Move Up (North)")] MoveUp,
        [Description("Move Down (South)")] MoveDown,
        [Description("Zoom In")] ZoomIn,
        [Description("Zoom Out")] ZoomOut,
        [Description("Reset Zoom and Center Location")] ResetZoomAndLocation,
        [Description("Zoom and Follow Player Train")] FollowTrain,
        [Description("Debug Information (Tab)")] DisplayDebugScreen,
        [Description("Signal State Window")] DisplaySignalStateWindow,
        [Description("Help Window (Tab)")] DisplayHelpWindow,
        [Description("Debug Stepwise")] DebugStep,
    }
}
