namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Root view model for the WPF shell. Holds the hosted menu and dockable tool-window view models plus
    /// shell-level commands.
    /// </summary>
    internal sealed class MainWindowViewModel : ObservableObject
    {
        private ToolboxMenuViewModel menu;
        private DebugToolWindowViewModel debugTool;
        private LocationToolWindowViewModel locationTool;
        private LogToolWindowViewModel logTool;
        private RelayCommand toggleDebugToolCommand;
        private RelayCommand toggleLocationToolCommand;
        private RelayCommand toggleLogToolCommand;
        private bool isDebugToolVisible;
        private bool isLocationToolVisible;
        private bool isLogToolVisible;

        public ToolboxMenuViewModel Menu
        {
            get => menu;
            set => SetProperty(ref menu, value);
        }

        public DebugToolWindowViewModel DebugTool
        {
            get => debugTool;
            set => SetProperty(ref debugTool, value);
        }

        public LocationToolWindowViewModel LocationTool
        {
            get => locationTool;
            set => SetProperty(ref locationTool, value);
        }

        public LogToolWindowViewModel LogTool
        {
            get => logTool;
            set => SetProperty(ref logTool, value);
        }

        public RelayCommand ToggleDebugToolCommand
        {
            get => toggleDebugToolCommand;
            set => SetProperty(ref toggleDebugToolCommand, value);
        }

        public RelayCommand ToggleLocationToolCommand
        {
            get => toggleLocationToolCommand;
            set => SetProperty(ref toggleLocationToolCommand, value);
        }

        public RelayCommand ToggleLogToolCommand
        {
            get => toggleLogToolCommand;
            set => SetProperty(ref toggleLogToolCommand, value);
        }

        public bool IsDebugToolVisible
        {
            get => isDebugToolVisible;
            set => SetProperty(ref isDebugToolVisible, value);
        }

        public bool IsLocationToolVisible
        {
            get => isLocationToolVisible;
            set => SetProperty(ref isLocationToolVisible, value);
        }

        public bool IsLogToolVisible
        {
            get => isLogToolVisible;
            set => SetProperty(ref isLogToolVisible, value);
        }
    }
}
