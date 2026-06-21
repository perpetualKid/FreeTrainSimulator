using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Track Node Information dockable tool window. Its DataContext is supplied by the
    /// shell as a <see cref="ViewModels.TrackNodeInfoToolWindowViewModel"/>, so all bindings are relative to
    /// that view model.
    /// </summary>
    internal partial class TrackNodeInfoToolView : UserControl
    {
        public TrackNodeInfoToolView()
        {
            InitializeComponent();
        }
    }
}
