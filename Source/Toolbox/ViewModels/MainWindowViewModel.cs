namespace FreeTrainSimulator.Toolbox.ViewModels
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
        private RouteNavigationToolWindowViewModel routeNavigationTool;
        private HelpToolWindowViewModel helpTool;
        private SettingsToolWindowViewModel settingsTool;
        private TrainPathToolWindowViewModel trainPathTool;
        private StatusBarViewModel statusBar;
        private RelayCommand toggleDebugToolCommand;
        private RelayCommand toggleLocationToolCommand;
        private RelayCommand toggleLogToolCommand;
        private RelayCommand toggleRouteNavigationToolCommand;
        private RelayCommand toggleHelpToolCommand;
        private RelayCommand toggleSettingsToolCommand;
        private RelayCommand toggleTrainPathToolCommand;
        private RelayCommand toggleRouteToolCommand;
        private RelayCommand showRouteToolCommand;
        private RelayCommand resetSettingsCommand;
        private bool isDebugToolVisible;
        private bool isLocationToolVisible;
        private bool isLogToolVisible;
        private bool isRouteNavigationToolVisible;
        private bool isHelpToolVisible;
        private bool isSettingsToolVisible;
        private bool isTrainPathToolVisible;
        private bool isRouteToolVisible;

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

        public RouteNavigationToolWindowViewModel RouteNavigationTool
        {
            get => routeNavigationTool;
            set => SetProperty(ref routeNavigationTool, value);
        }

        public HelpToolWindowViewModel HelpTool
        {
            get => helpTool;
            set => SetProperty(ref helpTool, value);
        }

        public SettingsToolWindowViewModel SettingsTool
        {
            get => settingsTool;
            set => SetProperty(ref settingsTool, value);
        }

        public TrainPathToolWindowViewModel TrainPathTool
        {
            get => trainPathTool;
            set => SetProperty(ref trainPathTool, value);
        }

        public StatusBarViewModel StatusBar
        {
            get => statusBar;
            set => SetProperty(ref statusBar, value);
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

        public RelayCommand ToggleRouteNavigationToolCommand
        {
            get => toggleRouteNavigationToolCommand;
            set => SetProperty(ref toggleRouteNavigationToolCommand, value);
        }

        public RelayCommand ToggleHelpToolCommand
        {
            get => toggleHelpToolCommand;
            set => SetProperty(ref toggleHelpToolCommand, value);
        }

        public RelayCommand ToggleSettingsToolCommand
        {
            get => toggleSettingsToolCommand;
            set => SetProperty(ref toggleSettingsToolCommand, value);
        }

        public RelayCommand ToggleTrainPathToolCommand
        {
            get => toggleTrainPathToolCommand;
            set => SetProperty(ref toggleTrainPathToolCommand, value);
        }

        public RelayCommand ToggleRouteToolCommand
        {
            get => toggleRouteToolCommand;
            set => SetProperty(ref toggleRouteToolCommand, value);
        }

        public RelayCommand ShowRouteToolCommand
        {
            get => showRouteToolCommand;
            set => SetProperty(ref showRouteToolCommand, value);
        }

        public RelayCommand ResetSettingsCommand
        {
            get => resetSettingsCommand;
            set => SetProperty(ref resetSettingsCommand, value);
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

        public bool IsRouteNavigationToolVisible
        {
            get => isRouteNavigationToolVisible;
            set => SetProperty(ref isRouteNavigationToolVisible, value);
        }

        public bool IsHelpToolVisible
        {
            get => isHelpToolVisible;
            set => SetProperty(ref isHelpToolVisible, value);
        }

        public bool IsSettingsToolVisible
        {
            get => isSettingsToolVisible;
            set => SetProperty(ref isSettingsToolVisible, value);
        }

        public bool IsTrainPathToolVisible
        {
            get => isTrainPathToolVisible;
            set => SetProperty(ref isTrainPathToolVisible, value);
        }

        public bool IsRouteToolVisible
        {
            get => isRouteToolVisible;
            set => SetProperty(ref isRouteToolVisible, value);
        }
    }
}
