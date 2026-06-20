namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Groups hosted-mode bridges published by the MonoGame toolbox window to the WPF shell.
    /// </summary>
    internal sealed record HostedToolboxServices(
        HostedToolboxMenu Menu,
        DebugToolWindow DebugToolWindow,
        LocationToolWindow LocationToolWindow,
        LogToolWindow LogToolWindow,
        TrackItemInfoToolWindow TrackItemInfoToolWindow,
        TrackNodeInfoToolWindow TrackNodeInfoToolWindow,
        HelpToolWindow HelpToolWindow,
        SettingsToolWindow SettingsToolWindow,
        TrainPathToolWindow TrainPathToolWindow,
        StatusBarToolWindow StatusBarToolWindow);
}
