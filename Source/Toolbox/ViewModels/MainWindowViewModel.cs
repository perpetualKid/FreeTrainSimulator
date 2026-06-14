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
        private TrackItemInfoToolWindowViewModel trackItemInfoTool;
        private TrackNodeInfoToolWindowViewModel trackNodeInfoTool;
        private HelpToolWindowViewModel helpTool;
        private SettingsToolWindowViewModel settingsTool;
        private TrainPathToolWindowViewModel trainPathTool;
        private RelayCommand toggleDebugToolCommand;
        private RelayCommand toggleLocationToolCommand;
        private RelayCommand toggleLogToolCommand;
        private RelayCommand toggleTrackItemInfoToolCommand;
        private RelayCommand toggleTrackNodeInfoToolCommand;
        private RelayCommand toggleHelpToolCommand;
        private RelayCommand toggleSettingsToolCommand;
        private RelayCommand toggleTrainPathToolCommand;
        private bool isDebugToolVisible;
        private bool isLocationToolVisible;
        private bool isLogToolVisible;
        private bool isTrackItemInfoToolVisible;
        private bool isTrackNodeInfoToolVisible;
        private bool isHelpToolVisible;
        private bool isSettingsToolVisible;
        private bool isTrainPathToolVisible;

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

        public TrackItemInfoToolWindowViewModel TrackItemInfoTool
        {
            get => trackItemInfoTool;
            set => SetProperty(ref trackItemInfoTool, value);
        }

        public TrackNodeInfoToolWindowViewModel TrackNodeInfoTool
        {
            get => trackNodeInfoTool;
            set => SetProperty(ref trackNodeInfoTool, value);
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

        public RelayCommand ToggleTrackItemInfoToolCommand
        {
            get => toggleTrackItemInfoToolCommand;
            set => SetProperty(ref toggleTrackItemInfoToolCommand, value);
        }

        public RelayCommand ToggleTrackNodeInfoToolCommand
        {
            get => toggleTrackNodeInfoToolCommand;
            set => SetProperty(ref toggleTrackNodeInfoToolCommand, value);
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

        public bool IsTrackItemInfoToolVisible
        {
            get => isTrackItemInfoToolVisible;
            set => SetProperty(ref isTrackItemInfoToolVisible, value);
        }

        public bool IsTrackNodeInfoToolVisible
        {
            get => isTrackNodeInfoToolVisible;
            set => SetProperty(ref isTrackNodeInfoToolVisible, value);
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
    }
}
