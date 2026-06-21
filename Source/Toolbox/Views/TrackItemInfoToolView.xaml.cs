using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Track Item Information dockable tool window. Its DataContext is supplied by the
    /// shell as a <see cref="ViewModels.TrackItemInfoToolWindowViewModel"/>, so all bindings are relative to
    /// that view model.
    /// </summary>
    internal partial class TrackItemInfoToolView : UserControl
    {
        public TrackItemInfoToolView()
        {
            InitializeComponent();
        }
    }
}
