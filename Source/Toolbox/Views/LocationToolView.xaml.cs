using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Location dockable tool window. Its DataContext is supplied by the shell as a
    /// <see cref="ViewModels.LocationToolWindowViewModel"/>, so all bindings are relative to that view model.
    /// </summary>
    internal partial class LocationToolView : UserControl
    {
        public LocationToolView()
        {
            InitializeComponent();
        }
    }
}
