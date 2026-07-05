namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Groups hosted-mode bridges published by the MonoGame toolbox window to the WPF shell.
    /// </summary>
    internal sealed record HostedToolboxServices
    {
        /// <summary>The hosted toolbox menu bridge.</summary>
        public HostedToolboxMenu Menu { get; init; }

        /// <summary>The debug tool window bridge.</summary>
        public DebugToolWindow DebugToolWindow { get; init; }

        /// <summary>The location tool window bridge.</summary>
        public LocationToolWindow LocationToolWindow { get; init; }

        /// <summary>The log tool window bridge.</summary>
        public LogToolWindow LogToolWindow { get; init; }

        /// <summary>The help tool window bridge.</summary>
        public HelpToolWindow HelpToolWindow { get; init; }

        /// <summary>The settings tool window bridge.</summary>
        public SettingsToolWindow SettingsToolWindow { get; init; }

        /// <summary>The train-path tool window bridge.</summary>
        public TrainPathToolWindow TrainPathToolWindow { get; init; }

        /// <summary>The status-bar tool window bridge.</summary>
        public StatusBarToolWindow StatusBarToolWindow { get; init; }

        /// <summary>The route-navigation tool window bridge.</summary>
        public RouteNavigationToolWindow RouteNavigationToolWindow { get; init; }
    }
}
