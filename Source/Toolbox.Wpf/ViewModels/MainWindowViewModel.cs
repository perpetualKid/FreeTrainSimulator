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
        private RelayCommand toggleDebugToolCommand;

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

        public RelayCommand ToggleDebugToolCommand
        {
            get => toggleDebugToolCommand;
            set => SetProperty(ref toggleDebugToolCommand, value);
        }
    }
}
