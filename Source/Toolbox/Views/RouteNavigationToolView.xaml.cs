using System.Windows.Controls;

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
    }
}
