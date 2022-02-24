using System.ComponentModel;

namespace Orts.TrackViewer
{
    public enum UserCommand
    {
        [Description("Cancel or Close")] Cancel, //Escape Key
        [Description("Span another instance")] NewInstance,
        [Description("Change Screen Mode")] ChangeScreenMode,
        [Description("Quit")] QuitWindow,
        [Description("Move Left (East)")] MoveLeft,
        [Description("Move Right (West)")] MoveRight,
        [Description("Move Up (North)")] MoveUp,
        [Description("Move Down (South)")] MoveDown,
        [Description("Zoom In")] ZoomIn,
        [Description("Zoom Out")] ZoomOut,
        [Description("Reset Zoom and Center Location")] ResetZoomAndLocation,
        [Description("Screenshot")] PrintScreen,
        [Description("Debug Information (Tab)")] DisplayDebugScreen,
        [Description("Location Window (Tab)")] DisplayLocationWindow,
        [Description("Help Window (Tab)")] DisplayHelpWindow,
    }
}
