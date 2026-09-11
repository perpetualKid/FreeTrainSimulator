using System.Windows.Controls;

using FreeTrainSimulator.Toolbox.ViewModels;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Route Navigation dockable tool window. Its DataContext is supplied by the shell
    /// as a <see cref="ViewModels.RouteNavigationToolWindowViewModel"/>, so all bindings are relative to that
    /// view model. Combines station/platform/siding centering with the by-id track item and track node lookups.
    /// </summary>
    internal partial class RouteNavigationToolView : UserControl
    {
        public RouteNavigationToolView()
        {
            InitializeComponent();
        }

        // The station/platform/siding lists bind SelectedItem OneWay and forward user picks here; see
        // ToolWindowSelection for why a TwoWay binding is unreliable in these hosted tool windows.
        private void StationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RouteNavigationToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out RouteNavigationItemViewModel item))
                viewModel.UserSelectStation(item);
        }

        private void PlatformList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RouteNavigationToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out RouteNavigationItemViewModel item))
                viewModel.UserSelectPlatform(item);
        }

        private void SidingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RouteNavigationToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out RouteNavigationItemViewModel item))
                viewModel.UserSelectSiding(item);
        }
    }
}
