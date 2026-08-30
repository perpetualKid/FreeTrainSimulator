using System.ComponentModel;

namespace FreeTrainSimulator.Toolbox
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
        [Description("Settings Window (Tab)")] DisplaySettingsWindow,
        [Description("Log Window (Tab)")] DisplayLogWindow,
        [Description("Train Path Window (Tab)")] DisplayTrainPathWindow,
        [Description("Path Editor Undo")] PathEditorUndo,
        [Description("Path Editor Redo")] PathEditorRedo,           
        [Description("Path Editor Alternate Redo")] PathEditorAlternateRedo,
        [Description("Remove Selected Via Point")] RemoveSelectedViaPoint,
        [Description("Commit Path Placement")] CommitPathPlacement,
        [Description("Next Route Candidate")] NextRouteCandidate,
        [Description("Previous Route Candidate")] PreviousRouteCandidate,
        [Description("Accept Route Candidate")] AcceptRouteCandidate,
    }
}
    