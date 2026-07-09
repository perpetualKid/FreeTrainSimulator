using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Routes dockable tool window. Its DataContext is supplied
    /// by the shell as a <see cref="ViewModels.ToolboxMenuViewModel"/>, so all bindings are relative to that
    /// view model.
    /// </summary>
    internal partial class RouteToolView : UserControl
    {
        public RouteToolView()
        {
            InitializeComponent();
        }
    }
}
